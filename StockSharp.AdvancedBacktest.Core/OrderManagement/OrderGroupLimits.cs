namespace StockSharp.AdvancedBacktest.OrderManagement;

public sealed class OrderGroupLimits
{
    public int MaxGroupsPerSecurity { get; init; }
    public decimal MaxRiskPercentPerGroup { get; init; }
    public bool ThrowIfNotMatchingVolume { get; init; }

    public OrderGroupLimits(
        int maxGroupsPerSecurity = 10,
        decimal maxRiskPercentPerGroup = 2.0m,
        bool throwIfNotMatchingVolume = true)
    {
        if (maxGroupsPerSecurity < 1)
            throw new ArgumentException("MaxGroupsPerSecurity must be at least 1", nameof(maxGroupsPerSecurity));
        if (maxRiskPercentPerGroup <= 0)
            throw new ArgumentException("MaxRiskPercentPerGroup must be positive", nameof(maxRiskPercentPerGroup));
        if (maxRiskPercentPerGroup > 100)
            throw new ArgumentException("MaxRiskPercentPerGroup cannot exceed 100", nameof(maxRiskPercentPerGroup));

        MaxGroupsPerSecurity = maxGroupsPerSecurity;
        MaxRiskPercentPerGroup = maxRiskPercentPerGroup;
        ThrowIfNotMatchingVolume = throwIfNotMatchingVolume;
    }

    public void Validate()
    {
        if (MaxGroupsPerSecurity < 1)
            throw new ArgumentException("MaxGroupsPerSecurity must be at least 1", nameof(MaxGroupsPerSecurity));
        if (MaxRiskPercentPerGroup <= 0)
            throw new ArgumentException("MaxRiskPercentPerGroup must be positive", nameof(MaxRiskPercentPerGroup));
        if (MaxRiskPercentPerGroup > 100)
            throw new ArgumentException("MaxRiskPercentPerGroup cannot exceed 100", nameof(MaxRiskPercentPerGroup));
    }
}
