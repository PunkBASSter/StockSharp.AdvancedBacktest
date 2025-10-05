/**
 * Utility functions for formatting metric values in the dashboard
 */

export type MetricType = 'ratio' | 'percentage' | 'currency' | 'count';

/**
 * Formats a metric value based on its type
 * @param value - The numeric value to format
 * @param type - The type of metric (ratio, percentage, currency, count)
 * @returns Formatted string representation of the value
 */
export function formatMetricValue(
  value: number | string | null | undefined,
  type: MetricType
): string {
  // Handle null/undefined values
  if (value === null || value === undefined) {
    return 'N/A';
  }

  // Convert to number if string
  const numValue = typeof value === 'string' ? parseFloat(value) : value;

  // Handle invalid numbers
  if (isNaN(numValue)) {
    return 'N/A';
  }

  switch (type) {
    case 'ratio':
      // Format ratios to 2 decimal places (e.g., Sharpe Ratio, Sortino Ratio)
      return numValue.toFixed(2);

    case 'percentage':
      // Format percentages with 2 decimal places and % symbol
      return `${numValue.toFixed(2)}%`;

    case 'currency':
      // Format currency with $ symbol and commas
      return new Intl.NumberFormat('en-US', {
        style: 'currency',
        currency: 'USD',
        minimumFractionDigits: 2,
        maximumFractionDigits: 2,
      }).format(numValue);

    case 'count':
      // Format counts as whole numbers with commas
      return new Intl.NumberFormat('en-US', {
        minimumFractionDigits: 0,
        maximumFractionDigits: 0,
      }).format(numValue);

    default:
      return numValue.toString();
  }
}

/**
 * Converts camelCase property names to Title Case labels
 * @param camelCase - The camelCase string to convert
 * @returns Title Case string
 */
export function camelCaseToTitleCase(camelCase: string): string {
  // Handle empty strings
  if (!camelCase) {
    return '';
  }

  // Insert spaces before capital letters
  const withSpaces = camelCase.replace(/([A-Z])/g, ' $1');

  // Trim any leading/trailing spaces and capitalize first letter
  const trimmed = withSpaces.trim();
  return trimmed.charAt(0).toUpperCase() + trimmed.slice(1);
}

/**
 * Determines if a metric value is positive, negative, or neutral
 * @param value - The numeric value to check
 * @returns 'positive' | 'negative' | 'neutral'
 */
export function getMetricSentiment(
  value: number | string | null | undefined
): 'positive' | 'negative' | 'neutral' {
  // Handle null/undefined values
  if (value === null || value === undefined) {
    return 'neutral';
  }

  // Convert to number if string
  const numValue = typeof value === 'string' ? parseFloat(value) : value;

  // Handle invalid numbers
  if (isNaN(numValue)) {
    return 'neutral';
  }

  if (numValue > 0) {
    return 'positive';
  } else if (numValue < 0) {
    return 'negative';
  } else {
    return 'neutral';
  }
}
