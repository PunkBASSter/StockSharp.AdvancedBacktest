using Microsoft.Extensions.Options;
using StockSharp.AdvancedBacktest.Strategies.Modules;
using StockSharp.AdvancedBacktest.Strategies.Modules.Factories;
using StockSharp.AdvancedBacktest.Strategies.Modules.PositionSizing;
using StockSharp.BusinessEntities;

namespace StockSharp.AdvancedBacktest.Tests.Strategies.Modules.Factories;

public class RiskAwarePositionSizerFactoryTests
{
    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new RiskAwarePositionSizerFactory(null!));
    }

    [Fact]
    public void Create_ReturnsFixedRiskPositionSizer()
    {
        var options = Options.Create(new StrategyOptions
        {
            RiskPercentPerTrade = 2m,
            MinPositionSize = 5m,
            MaxPositionSize = 500m
        });
        var factory = new RiskAwarePositionSizerFactory(options);

        var sizer = factory.Create();

        Assert.NotNull(sizer);
        Assert.IsType<FixedRiskPositionSizer>(sizer);
    }

    [Fact]
    public void Create_UsesOptionsValues()
    {
        // Test that factory passes correct options to sizer
        // (10000 * 0.02) / |100-95| = 200 / 5 = 40
        var options = Options.Create(new StrategyOptions
        {
            RiskPercentPerTrade = 2m,
            MinPositionSize = 1m,
            MaxPositionSize = 1000m
        });
        var factory = new RiskAwarePositionSizerFactory(options);
        var portfolio = new Portfolio
        {
            CurrentValue = 10000m,
            BeginValue = 10000m
        };

        var sizer = factory.Create();
        var result = sizer.Calculate(100m, 95m, portfolio);

        Assert.Equal(40m, result);
    }

    [Fact]
    public void Create_AppliesMinPositionSize()
    {
        var options = Options.Create(new StrategyOptions
        {
            RiskPercentPerTrade = 0.1m,
            MinPositionSize = 50m,
            MaxPositionSize = 1000m
        });
        var factory = new RiskAwarePositionSizerFactory(options);
        var portfolio = new Portfolio { CurrentValue = 1000m, BeginValue = 1000m };

        var sizer = factory.Create();
        // (1000 * 0.001) / |100 - 1| = 1/99 ~ 0.01 -> should clamp to 50
        var result = sizer.Calculate(100m, 1m, portfolio);

        Assert.Equal(50m, result);
    }

    [Fact]
    public void Create_AppliesMaxPositionSize()
    {
        var options = Options.Create(new StrategyOptions
        {
            RiskPercentPerTrade = 10m,
            MinPositionSize = 1m,
            MaxPositionSize = 25m
        });
        var factory = new RiskAwarePositionSizerFactory(options);
        var portfolio = new Portfolio { CurrentValue = 100000m, BeginValue = 100000m };

        var sizer = factory.Create();
        // Large position calculated but clamped to 25
        var result = sizer.Calculate(100m, 99m, portfolio);

        Assert.Equal(25m, result);
    }
}
