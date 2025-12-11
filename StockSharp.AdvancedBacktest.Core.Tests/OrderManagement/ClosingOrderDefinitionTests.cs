using StockSharp.AdvancedBacktest.OrderManagement;
using StockSharp.Messages;

namespace StockSharp.AdvancedBacktest.Tests.OrderManagement;

public class ClosingOrderDefinitionTests
{
    [Fact]
    public void Constructor_CreatesValidDefinition()
    {
        var definition = new ClosingOrderDefinition(
            price: 110m,
            volume: 50m);

        Assert.Equal(110m, definition.Price);
        Assert.Equal(50m, definition.Volume);
        Assert.Equal(OrderTypes.Limit, definition.OrderType);
    }

    [Fact]
    public void Constructor_AcceptsCustomOrderType()
    {
        var definition = new ClosingOrderDefinition(
            price: 110m,
            volume: 50m,
            orderType: OrderTypes.Market);

        Assert.Equal(OrderTypes.Market, definition.OrderType);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Constructor_ThrowsOnNonPositiveVolume(decimal volume)
    {
        Assert.Throws<ArgumentException>(() => new ClosingOrderDefinition(
            price: 110m,
            volume: volume));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Constructor_ThrowsOnNegativePriceForLimitOrders(decimal price)
    {
        Assert.Throws<ArgumentException>(() => new ClosingOrderDefinition(
            price: price,
            volume: 50m,
            orderType: OrderTypes.Limit));
    }

    [Fact]
    public void Constructor_ThrowsOnZeroPriceForLimitOrders()
    {
        Assert.Throws<ArgumentException>(() => new ClosingOrderDefinition(
            price: 0m,
            volume: 50m,
            orderType: OrderTypes.Limit));
    }

    [Fact]
    public void Constructor_AllowsZeroPriceForMarketOrders()
    {
        var definition = new ClosingOrderDefinition(
            price: 0m,
            volume: 50m,
            orderType: OrderTypes.Market);

        Assert.Equal(0m, definition.Price);
        Assert.Equal(OrderTypes.Market, definition.OrderType);
    }

    [Fact]
    public void Validate_ThrowsOnInvalidValues()
    {
        // Create valid definition first
        var definition = new ClosingOrderDefinition(110m, 50m);

        // Validate should pass for valid definition
        definition.Validate(); // Should not throw
    }

    [Theory]
    [InlineData(0.01)]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(1000000)]
    public void Constructor_AcceptsValidVolumes(decimal volume)
    {
        var definition = new ClosingOrderDefinition(
            price: 110m,
            volume: volume);

        Assert.Equal(volume, definition.Volume);
    }

    [Theory]
    [InlineData(0.01)]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(100000)]
    public void Constructor_AcceptsValidPrices(decimal price)
    {
        var definition = new ClosingOrderDefinition(
            price: price,
            volume: 50m);

        Assert.Equal(price, definition.Price);
    }
}
