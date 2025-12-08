using StockSharp.AdvancedBacktest.Strategies.Modules.PositionSizing;
using StockSharp.BusinessEntities;

namespace StockSharp.AdvancedBacktest.Tests.Strategies.Modules.PositionSizing;

public class FixedRiskPositionSizerTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        var sizer = new FixedRiskPositionSizer(1m, 1m, 1000m);

        Assert.NotNull(sizer);
    }

    [Fact]
    public void Constructor_WithDefaultParameters_UsesDefaults()
    {
        var sizer = new FixedRiskPositionSizer();

        Assert.NotNull(sizer);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-0.01)]
    public void Constructor_WithInvalidRiskPercent_ThrowsArgumentException(decimal riskPercent)
    {
        Assert.Throws<ArgumentException>(() => new FixedRiskPositionSizer(riskPercent));
    }

    [Theory]
    [InlineData(101)]
    [InlineData(200)]
    public void Constructor_WithRiskPercentOver100_ThrowsArgumentException(decimal riskPercent)
    {
        Assert.Throws<ArgumentException>(() => new FixedRiskPositionSizer(riskPercent));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WithInvalidMinPositionSize_ThrowsArgumentException(decimal minSize)
    {
        Assert.Throws<ArgumentException>(() => new FixedRiskPositionSizer(1m, minSize, 1000m));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WithInvalidMaxPositionSize_ThrowsArgumentException(decimal maxSize)
    {
        Assert.Throws<ArgumentException>(() => new FixedRiskPositionSizer(1m, 1m, maxSize));
    }

    [Fact]
    public void Constructor_WithMinGreaterThanMax_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new FixedRiskPositionSizer(1m, 100m, 10m));
    }

    #endregion

    #region Calculate Tests - Standard Scenarios

    [Fact]
    public void Calculate_WithValidInputs_ReturnsCorrectPositionSize()
    {
        // Formula: (equity * riskPercent%) / |entryPrice - stopLoss|
        // (10000 * 0.01) / |100 - 95| = 100 / 5 = 20
        var sizer = new FixedRiskPositionSizer(riskPercent: 1m, minPositionSize: 1m, maxPositionSize: 1000m);
        var portfolio = CreatePortfolio(10000m);

        var result = sizer.Calculate(entryPrice: 100m, stopLoss: 95m, portfolio);

        Assert.Equal(20m, result);
    }

    [Fact]
    public void Calculate_WithShortPosition_ReturnsCorrectPositionSize()
    {
        // Short position has stopLoss > entryPrice
        // (50000 * 0.02) / |200 - 210| = 1000 / 10 = 100
        var sizer = new FixedRiskPositionSizer(riskPercent: 2m, minPositionSize: 1m, maxPositionSize: 1000m);
        var portfolio = CreatePortfolio(50000m);

        var result = sizer.Calculate(entryPrice: 200m, stopLoss: 210m, portfolio);

        Assert.Equal(100m, result);
    }

    [Theory]
    [InlineData(10000, 1, 100, 98, 50)]     // (10000*0.01)/(|100-98|) = 100/2 = 50
    [InlineData(25000, 2, 50, 45, 100)]     // (25000*0.02)/(|50-45|) = 500/5 = 100
    [InlineData(100000, 0.5, 1000, 980, 25)] // (100000*0.005)/(|1000-980|) = 500/20 = 25
    public void Calculate_WithVariousInputs_ReturnsExpectedPositionSize(
        decimal equity, decimal riskPercent, decimal entryPrice, decimal stopLoss, decimal expected)
    {
        var sizer = new FixedRiskPositionSizer(riskPercent, minPositionSize: 1m, maxPositionSize: 10000m);
        var portfolio = CreatePortfolio(equity);

        var result = sizer.Calculate(entryPrice, stopLoss, portfolio);

        Assert.Equal(expected, result);
    }

    #endregion

    #region Calculate Tests - Edge Cases

    [Fact]
    public void Calculate_WhenStopLossEqualsEntry_ReturnsMinPositionSize()
    {
        var minSize = 5m;
        var sizer = new FixedRiskPositionSizer(riskPercent: 1m, minPositionSize: minSize, maxPositionSize: 1000m);
        var portfolio = CreatePortfolio(10000m);

        var result = sizer.Calculate(entryPrice: 100m, stopLoss: 100m, portfolio);

        Assert.Equal(minSize, result);
    }

    [Fact]
    public void Calculate_WhenResultBelowMin_ReturnsMinPositionSize()
    {
        // Large stop distance results in tiny position
        // (1000 * 0.001) / |100 - 1| = 1 / 99 = ~0.01 -> clamped to 10
        var minSize = 10m;
        var sizer = new FixedRiskPositionSizer(riskPercent: 0.1m, minPositionSize: minSize, maxPositionSize: 1000m);
        var portfolio = CreatePortfolio(1000m);

        var result = sizer.Calculate(entryPrice: 100m, stopLoss: 1m, portfolio);

        Assert.Equal(minSize, result);
    }

    [Fact]
    public void Calculate_WhenResultAboveMax_ReturnsMaxPositionSize()
    {
        // Tiny stop distance results in huge position
        // (100000 * 0.10) / |100 - 99.99| = 10000 / 0.01 = 1,000,000 -> clamped to 50
        var maxSize = 50m;
        var sizer = new FixedRiskPositionSizer(riskPercent: 10m, minPositionSize: 1m, maxPositionSize: maxSize);
        var portfolio = CreatePortfolio(100000m);

        var result = sizer.Calculate(entryPrice: 100m, stopLoss: 99.99m, portfolio);

        Assert.Equal(maxSize, result);
    }

    #endregion

    #region Calculate Tests - Input Validation

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Calculate_WithInvalidEntryPrice_ThrowsArgumentException(decimal entryPrice)
    {
        var sizer = new FixedRiskPositionSizer();
        var portfolio = CreatePortfolio(10000m);

        Assert.Throws<ArgumentException>(() => sizer.Calculate(entryPrice, stopLoss: 95m, portfolio));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Calculate_WithInvalidStopLoss_ThrowsArgumentException(decimal stopLoss)
    {
        var sizer = new FixedRiskPositionSizer();
        var portfolio = CreatePortfolio(10000m);

        Assert.Throws<ArgumentException>(() => sizer.Calculate(entryPrice: 100m, stopLoss, portfolio));
    }

    [Fact]
    public void Calculate_WithNullPortfolio_ThrowsArgumentNullException()
    {
        var sizer = new FixedRiskPositionSizer();

        Assert.Throws<ArgumentNullException>(() => sizer.Calculate(100m, 95m, null!));
    }

    [Fact]
    public void Calculate_WithZeroEquity_ThrowsInvalidOperationException()
    {
        var sizer = new FixedRiskPositionSizer();
        var portfolio = CreatePortfolio(0m);

        Assert.Throws<InvalidOperationException>(() => sizer.Calculate(100m, 95m, portfolio));
    }

    [Fact]
    public void Calculate_WithNegativeEquity_ThrowsInvalidOperationException()
    {
        var sizer = new FixedRiskPositionSizer();
        var portfolio = new Portfolio { CurrentValue = -1000m, BeginValue = 10000m };

        Assert.Throws<InvalidOperationException>(() => sizer.Calculate(100m, 95m, portfolio));
    }

    #endregion

    #region Calculate Tests - Portfolio Equity Resolution

    [Fact]
    public void Calculate_UsesCurrentValueWhenAvailable()
    {
        // Should use CurrentValue (20000), not BeginValue (10000)
        // (20000 * 0.01) / |100 - 95| = 200 / 5 = 40
        var sizer = new FixedRiskPositionSizer(riskPercent: 1m, minPositionSize: 1m, maxPositionSize: 1000m);
        var portfolio = new Portfolio { CurrentValue = 20000m, BeginValue = 10000m };

        var result = sizer.Calculate(100m, 95m, portfolio);

        Assert.Equal(40m, result);
    }

    [Fact]
    public void Calculate_FallsBackToBeginValueWhenCurrentValueIsNull()
    {
        // Should use BeginValue (10000)
        // (10000 * 0.01) / |100 - 95| = 100 / 5 = 20
        var sizer = new FixedRiskPositionSizer(riskPercent: 1m, minPositionSize: 1m, maxPositionSize: 1000m);
        var portfolio = new Portfolio { CurrentValue = null, BeginValue = 10000m };

        var result = sizer.Calculate(100m, 95m, portfolio);

        Assert.Equal(20m, result);
    }

    #endregion

    #region Calculate Tests - Security Integration (Phase 2)

    [Fact]
    public void Calculate_WithSecurity_RoundsToVolumeStep()
    {
        // Without rounding: (10000 * 0.01) / |100 - 95| = 20
        // With VolumeStep = 3: floor(20 / 3) * 3 = 18
        var sizer = new FixedRiskPositionSizer(riskPercent: 1m, minPositionSize: 1m, maxPositionSize: 1000m);
        var portfolio = CreatePortfolio(10000m);
        var security = new Security { VolumeStep = 3m };

        var result = sizer.Calculate(100m, 95m, portfolio, security);

        Assert.Equal(18m, result);
    }

    [Fact]
    public void Calculate_WithSecurity_UsesSecurityMinVolume()
    {
        // Calculated: (1000 * 0.01) / |100 - 1| = 10/99 = ~0.1
        // Security.MinVolume = 5, should clamp to 5
        var sizer = new FixedRiskPositionSizer(riskPercent: 1m, minPositionSize: 1m, maxPositionSize: 1000m);
        var portfolio = CreatePortfolio(1000m);
        var security = new Security { MinVolume = 5m };

        var result = sizer.Calculate(100m, 1m, portfolio, security);

        Assert.Equal(5m, result);
    }

    [Fact]
    public void Calculate_WithSecurity_UsesSecurityMaxVolume()
    {
        // Large position calculated, but Security.MaxVolume = 25
        var sizer = new FixedRiskPositionSizer(riskPercent: 10m, minPositionSize: 1m, maxPositionSize: 1000m);
        var portfolio = CreatePortfolio(100000m);
        var security = new Security { MaxVolume = 25m };

        var result = sizer.Calculate(100m, 99m, portfolio, security);

        Assert.Equal(25m, result);
    }

    [Fact]
    public void Calculate_WithNullSecurity_UsesConstructorLimits()
    {
        // Should use constructor min/max, not throw
        var sizer = new FixedRiskPositionSizer(riskPercent: 1m, minPositionSize: 1m, maxPositionSize: 1000m);
        var portfolio = CreatePortfolio(10000m);

        var result = sizer.Calculate(100m, 95m, portfolio, security: null);

        Assert.Equal(20m, result);
    }

    #endregion

    #region Financial Precision Tests

    [Fact]
    public void Calculate_MaintainsDecimalPrecision()
    {
        // (12345.67 * 0.015) / |123.456 - 120.123| = 185.18505 / 3.333 = ~55.5611...
        var sizer = new FixedRiskPositionSizer(riskPercent: 1.5m, minPositionSize: 0.001m, maxPositionSize: 100000m);
        var portfolio = CreatePortfolio(12345.67m);

        var result = sizer.Calculate(123.456m, 120.123m, portfolio);

        Assert.True(result > 55m && result < 56m);
    }

    #endregion

    #region Helper Methods

    private static Portfolio CreatePortfolio(decimal equity) => new()
    {
        Name = "TestPortfolio",
        CurrentValue = equity,
        BeginValue = equity
    };

    #endregion
}
