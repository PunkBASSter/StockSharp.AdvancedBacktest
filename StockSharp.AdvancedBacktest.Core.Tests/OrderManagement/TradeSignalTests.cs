using StockSharp.AdvancedBacktest.OrderManagement;
using StockSharp.Messages;

namespace StockSharp.AdvancedBacktest.Tests.OrderManagement;

public class TradeSignalTests
{
    [Theory]
    [InlineData(0, 1)]    // zero entry price
    [InlineData(100, -1)] // negative volume
    [InlineData(100, 0)]  // zero volume
    public void Validate_ThrowsOnInvalidValues(decimal entryPrice, decimal volume)
    {
        var signal = new OrderRequest
        {
            Direction = Sides.Buy,
            Price = entryPrice,
            Volume = volume
        };

        Assert.Throws<ArgumentException>(() => signal.Validate());
    }

    [Fact]
    public void Validate_ThrowsOnBuyWithStopAboveEntry()
    {
        var signal = new OrderRequest
        {
            Direction = Sides.Buy,
            Price = 100m,
            Volume = 1m,
            StopLoss = 110m
        };

        Assert.Throws<ArgumentException>(() => signal.Validate());
    }

    [Fact]
    public void Validate_ThrowsOnBuyWithTakeProfitBelowEntry()
    {
        var signal = new OrderRequest
        {
            Direction = Sides.Buy,
            Price = 100m,
            Volume = 1m,
            TakeProfit = 90m
        };

        Assert.Throws<ArgumentException>(() => signal.Validate());
    }

    [Fact]
    public void Validate_ThrowsOnSellWithStopBelowEntry()
    {
        var signal = new OrderRequest
        {
            Direction = Sides.Sell,
            Price = 100m,
            Volume = 1m,
            StopLoss = 90m
        };

        Assert.Throws<ArgumentException>(() => signal.Validate());
    }

    [Fact]
    public void Validate_ThrowsOnSellWithTakeProfitAboveEntry()
    {
        var signal = new OrderRequest
        {
            Direction = Sides.Sell,
            Price = 100m,
            Volume = 1m,
            TakeProfit = 110m
        };

        Assert.Throws<ArgumentException>(() => signal.Validate());
    }

    [Theory]
    [InlineData(Sides.Buy, 100, 90, 110)]   // valid buy
    [InlineData(Sides.Sell, 100, 110, 90)]  // valid sell
    public void Validate_SucceedsOnValidSignal(Sides direction, decimal entry, decimal stop, decimal takeProfit)
    {
        var signal = new OrderRequest
        {
            Direction = direction,
            Price = entry,
            Volume = 1m,
            StopLoss = stop,
            TakeProfit = takeProfit
        };

        signal.Validate(); // should not throw
    }
}
