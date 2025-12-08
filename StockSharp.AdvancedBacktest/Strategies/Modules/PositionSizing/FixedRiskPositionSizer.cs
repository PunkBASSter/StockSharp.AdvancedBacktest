using StockSharp.BusinessEntities;

namespace StockSharp.AdvancedBacktest.Strategies.Modules.PositionSizing;

public class FixedRiskPositionSizer : IRiskAwarePositionSizer
{
    private readonly decimal _riskPercent;
    private readonly decimal _minPositionSize;
    private readonly decimal _maxPositionSize;

    public FixedRiskPositionSizer(
        decimal riskPercent = 1m,
        decimal minPositionSize = 1m,
        decimal maxPositionSize = 1000m)
    {
        if (riskPercent <= 0 || riskPercent > 100)
            throw new ArgumentException("Risk percent must be between 0 (exclusive) and 100 (inclusive)", nameof(riskPercent));

        if (minPositionSize <= 0)
            throw new ArgumentException("Minimum position size must be greater than zero", nameof(minPositionSize));

        if (maxPositionSize <= 0)
            throw new ArgumentException("Maximum position size must be greater than zero", nameof(maxPositionSize));

        if (minPositionSize > maxPositionSize)
            throw new ArgumentException("Minimum position size cannot exceed maximum position size");

        _riskPercent = riskPercent;
        _minPositionSize = minPositionSize;
        _maxPositionSize = maxPositionSize;
    }

    public decimal Calculate(decimal entryPrice, decimal stopLoss, Portfolio portfolio, Security? security = null)
    {
        if (entryPrice <= 0)
            throw new ArgumentException("Entry price must be greater than zero", nameof(entryPrice));

        if (stopLoss <= 0)
            throw new ArgumentException("Stop loss must be greater than zero", nameof(stopLoss));

        ArgumentNullException.ThrowIfNull(portfolio);

        var equity = portfolio.CurrentValue ?? portfolio.BeginValue ?? 0;

        if (equity <= 0)
            throw new InvalidOperationException("Portfolio equity must be greater than zero for FixedRisk sizing");

        var riskDistance = Math.Abs(entryPrice - stopLoss);

        // Edge case: SL equals entry price (division by zero)
        if (riskDistance == 0)
            return GetEffectiveMin(security);

        var riskAmount = equity * (_riskPercent / 100m);
        var positionSize = riskAmount / riskDistance;

        // Apply security-specific volume step rounding
        if (security?.VolumeStep is > 0)
            positionSize = Math.Floor(positionSize / security.VolumeStep.Value) * security.VolumeStep.Value;

        // Apply limits (security limits take precedence if available)
        var effectiveMin = GetEffectiveMin(security);
        var effectiveMax = GetEffectiveMax(security);

        return Math.Clamp(positionSize, effectiveMin, effectiveMax);
    }

    private decimal GetEffectiveMin(Security? security) =>
        security?.MinVolume ?? _minPositionSize;

    private decimal GetEffectiveMax(Security? security) =>
        security?.MaxVolume ?? _maxPositionSize;
}
