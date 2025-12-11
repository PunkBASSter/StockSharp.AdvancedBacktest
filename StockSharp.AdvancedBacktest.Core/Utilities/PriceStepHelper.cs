using StockSharp.BusinessEntities;

namespace StockSharp.AdvancedBacktest.Utilities;

public static class PriceStepHelper
{
    /// <summary>
    /// Gets the price step for a security with defensive fallback strategy.
    /// </summary>
    /// <param name="security">The security to get price step for</param>
    /// <returns>Price step value</returns>
    /// <exception cref="ArgumentNullException">If security is null</exception>
    /// <exception cref="ArgumentException">If price step cannot be determined</exception>
    public static decimal GetPriceStep(Security security)
    {
        if (security == null)
            throw new ArgumentNullException(nameof(security));

        // Option 1: Use explicit PriceStep if available
        if (security.PriceStep.HasValue && security.PriceStep.Value > 0)
            return security.PriceStep.Value;

        // Option 2: Infer from Decimals if available
        if (security.Decimals.HasValue)
            return (decimal)Math.Pow(10, -security.Decimals.Value);

        // Option 3: Fail fast - no silent defaults
        throw new ArgumentException(
            $"Cannot determine price step for {security.Id}: " +
            $"Both PriceStep and Decimals are null. " +
            $"Please ensure security metadata is properly loaded.",
            nameof(security));
    }

    /// <summary>
    /// Calculates an appropriate default delta/threshold for indicators when no historical
    /// swing data is available. Uses a multiple of the price step as a reasonable minimum.
    /// </summary>
    /// <param name="security">The security to calculate delta for</param>
    /// <param name="multiplier">How many price steps to use (default: 10)</param>
    /// <returns>Default delta value suitable for initial indicator calculations</returns>
    public static decimal GetDefaultDelta(Security security, int multiplier = 10)
    {
        if (security == null)
            throw new ArgumentNullException(nameof(security));

        if (multiplier <= 0)
            throw new ArgumentOutOfRangeException(nameof(multiplier), "Multiplier must be positive");

        var priceStep = GetPriceStep(security);
        return priceStep * multiplier;
    }
}
