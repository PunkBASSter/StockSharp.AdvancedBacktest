import { formatMetricValue, getMetricSentiment, type MetricType } from '@/lib/format-metrics';

export interface MetricCardProps {
  label: string;
  value: number | string | null | undefined;
  type: MetricType;
  className?: string;
}

/**
 * MetricCard component displays a single performance metric with proper formatting
 * and color-coding based on whether the value is positive or negative
 */
export default function MetricCard({ label, value, type, className = '' }: MetricCardProps) {
  const formattedValue = formatMetricValue(value, type);
  const sentiment = getMetricSentiment(value);

  // Determine text color based on sentiment
  const valueColorClass =
    sentiment === 'positive'
      ? 'text-green-600'
      : sentiment === 'negative'
      ? 'text-red-600'
      : 'text-gray-900';

  return (
    <div className={`metric-card bg-white p-4 rounded-lg shadow hover:shadow-md transition-shadow ${className}`}>
      <div className="metric-label text-sm text-gray-600 mb-2">{label}</div>
      <div className={`metric-value text-2xl font-semibold ${valueColorClass}`}>
        {formattedValue}
      </div>
    </div>
  );
}
