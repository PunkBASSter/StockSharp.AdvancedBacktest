using StockSharp.AdvancedBacktest.OrderManagement;
using StockSharp.Messages;

namespace StockSharp.AdvancedBacktest.Tests.OrderManagement;

public class ExtendedTradeSignalTests
{
    private static ClosingOrderDefinition CreateClosingDefinition(decimal price, decimal volume) =>
        new(price, volume);

    [Fact]
    public void Constructor_CreatesValidSignal()
    {
        var closingOrders = new List<ClosingOrderDefinition>
        {
            CreateClosingDefinition(110m, 50m),
            CreateClosingDefinition(120m, 50m)
        };

        var signal = new ExtendedTradeSignal(
            direction: Sides.Buy,
            entryPrice: 100m,
            entryVolume: 100m,
            closingOrders: closingOrders);

        Assert.Equal(Sides.Buy, signal.Direction);
        Assert.Equal(100m, signal.EntryPrice);
        Assert.Equal(100m, signal.EntryVolume);
        Assert.Equal(2, signal.ClosingOrders.Count);
        Assert.Equal(OrderTypes.Limit, signal.EntryOrderType);
        Assert.Null(signal.StopLossPrice);
        Assert.Null(signal.GroupId);
        Assert.Null(signal.ExpiryTime);
    }

    [Fact]
    public void Constructor_AcceptsOptionalParameters()
    {
        var closingOrders = new List<ClosingOrderDefinition>
        {
            CreateClosingDefinition(110m, 100m)
        };
        var expiryTime = DateTime.UtcNow.AddHours(1);

        var signal = new ExtendedTradeSignal(
            direction: Sides.Buy,
            entryPrice: 100m,
            entryVolume: 100m,
            closingOrders: closingOrders,
            entryOrderType: OrderTypes.Market,
            stopLossPrice: 95m,
            groupId: "custom-group",
            expiryTime: expiryTime);

        Assert.Equal(OrderTypes.Market, signal.EntryOrderType);
        Assert.Equal(95m, signal.StopLossPrice);
        Assert.Equal("custom-group", signal.GroupId);
        Assert.Equal(expiryTime, signal.ExpiryTime);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Validate_ThrowsOnNonPositiveEntryPrice(decimal entryPrice)
    {
        var closingOrders = new List<ClosingOrderDefinition>
        {
            CreateClosingDefinition(110m, 100m)
        };

        var signal = new ExtendedTradeSignal(
            direction: Sides.Buy,
            entryPrice: entryPrice,
            entryVolume: 100m,
            closingOrders: closingOrders,
            skipValidation: true);

        Assert.Throws<ArgumentException>(() => signal.Validate());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Validate_ThrowsOnNonPositiveEntryVolume(decimal entryVolume)
    {
        var closingOrders = new List<ClosingOrderDefinition>
        {
            CreateClosingDefinition(110m, 100m)
        };

        var signal = new ExtendedTradeSignal(
            direction: Sides.Buy,
            entryPrice: 100m,
            entryVolume: entryVolume,
            closingOrders: closingOrders,
            skipValidation: true);

        Assert.Throws<ArgumentException>(() => signal.Validate());
    }

    [Fact]
    public void Validate_ThrowsOnEmptyClosingOrders()
    {
        var signal = new ExtendedTradeSignal(
            direction: Sides.Buy,
            entryPrice: 100m,
            entryVolume: 100m,
            closingOrders: new List<ClosingOrderDefinition>(),
            skipValidation: true);

        Assert.Throws<ArgumentException>(() => signal.Validate());
    }

    [Fact]
    public void Validate_ThrowsOnBuyWithStopLossAboveEntry()
    {
        var closingOrders = new List<ClosingOrderDefinition>
        {
            CreateClosingDefinition(110m, 100m)
        };

        var signal = new ExtendedTradeSignal(
            direction: Sides.Buy,
            entryPrice: 100m,
            entryVolume: 100m,
            closingOrders: closingOrders,
            stopLossPrice: 105m,
            skipValidation: true);

        Assert.Throws<ArgumentException>(() => signal.Validate());
    }

    [Fact]
    public void Validate_ThrowsOnSellWithStopLossBelowEntry()
    {
        var closingOrders = new List<ClosingOrderDefinition>
        {
            CreateClosingDefinition(90m, 100m)
        };

        var signal = new ExtendedTradeSignal(
            direction: Sides.Sell,
            entryPrice: 100m,
            entryVolume: 100m,
            closingOrders: closingOrders,
            stopLossPrice: 95m,
            skipValidation: true);

        Assert.Throws<ArgumentException>(() => signal.Validate());
    }

    [Fact]
    public void Validate_SucceedsOnValidBuySignal()
    {
        var closingOrders = new List<ClosingOrderDefinition>
        {
            CreateClosingDefinition(110m, 50m),
            CreateClosingDefinition(120m, 50m)
        };

        var signal = new ExtendedTradeSignal(
            direction: Sides.Buy,
            entryPrice: 100m,
            entryVolume: 100m,
            closingOrders: closingOrders,
            stopLossPrice: 95m);

        signal.Validate(); // Should not throw
    }

    [Fact]
    public void Validate_SucceedsOnValidSellSignal()
    {
        var closingOrders = new List<ClosingOrderDefinition>
        {
            CreateClosingDefinition(90m, 50m),
            CreateClosingDefinition(80m, 50m)
        };

        var signal = new ExtendedTradeSignal(
            direction: Sides.Sell,
            entryPrice: 100m,
            entryVolume: 100m,
            closingOrders: closingOrders,
            stopLossPrice: 105m);

        signal.Validate(); // Should not throw
    }

    [Fact]
    public void TotalClosingVolume_SumsAllClosingOrderVolumes()
    {
        var closingOrders = new List<ClosingOrderDefinition>
        {
            CreateClosingDefinition(110m, 30m),
            CreateClosingDefinition(120m, 30m),
            CreateClosingDefinition(130m, 40m)
        };

        var signal = new ExtendedTradeSignal(
            direction: Sides.Buy,
            entryPrice: 100m,
            entryVolume: 100m,
            closingOrders: closingOrders);

        Assert.Equal(100m, signal.TotalClosingVolume);
    }

    [Fact]
    public void IsVolumeMatched_ReturnsTrueWhenVolumesMatch()
    {
        var closingOrders = new List<ClosingOrderDefinition>
        {
            CreateClosingDefinition(110m, 50m),
            CreateClosingDefinition(120m, 50m)
        };

        var signal = new ExtendedTradeSignal(
            direction: Sides.Buy,
            entryPrice: 100m,
            entryVolume: 100m,
            closingOrders: closingOrders);

        Assert.True(signal.IsVolumeMatched);
    }

    [Fact]
    public void IsVolumeMatched_ReturnsFalseWhenVolumesMismatch()
    {
        var closingOrders = new List<ClosingOrderDefinition>
        {
            CreateClosingDefinition(110m, 40m),
            CreateClosingDefinition(120m, 40m)
        };

        var signal = new ExtendedTradeSignal(
            direction: Sides.Buy,
            entryPrice: 100m,
            entryVolume: 100m,
            closingOrders: closingOrders,
            skipValidation: true);

        Assert.False(signal.IsVolumeMatched);
    }

    [Fact]
    public void Validate_ThrowsWhenVolumeMismatchAndStrict()
    {
        var closingOrders = new List<ClosingOrderDefinition>
        {
            CreateClosingDefinition(110m, 40m),
            CreateClosingDefinition(120m, 40m)
        };

        var signal = new ExtendedTradeSignal(
            direction: Sides.Buy,
            entryPrice: 100m,
            entryVolume: 100m,
            closingOrders: closingOrders,
            skipValidation: true);

        Assert.Throws<ArgumentException>(() => signal.Validate(throwIfNotMatchingVolume: true));
    }

    [Fact]
    public void Validate_SucceedsOnVolumeMismatchWhenNotStrict()
    {
        var closingOrders = new List<ClosingOrderDefinition>
        {
            CreateClosingDefinition(110m, 40m),
            CreateClosingDefinition(120m, 40m)
        };

        var signal = new ExtendedTradeSignal(
            direction: Sides.Buy,
            entryPrice: 100m,
            entryVolume: 100m,
            closingOrders: closingOrders,
            skipValidation: true);

        signal.Validate(throwIfNotMatchingVolume: false); // Should not throw
    }
}
