import { WalkForwardMetricsData } from '@/types/chart-data';
import { camelCaseToTitleCase } from '@/lib/format-metrics';
import MetricCard from './MetricCard';

export interface MetricsGridProps {
  metrics: WalkForwardMetricsData;
  isLoading?: boolean;
  className?: string;
}

/**
 * MetricsGrid component displays all performance metrics in a responsive grid layout
 * - 4 columns on desktop (lg breakpoint)
 * - 2 columns on tablet (md breakpoint)
 * - 1 column on mobile
 */
export default function MetricsGrid({ metrics, isLoading = false, className = '' }: MetricsGridProps) {
  // Show loading skeleton if data is loading
  if (isLoading) {
    return <MetricsGridSkeleton className={className} />;
  }

  return (
    <div className={`grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 ${className}`}>
      <MetricCard
        label={camelCaseToTitleCase('sharpeRatio')}
        value={metrics.sharpeRatio}
        type="ratio"
      />
      <MetricCard
        label={camelCaseToTitleCase('sortinoRatio')}
        value={metrics.sortinoRatio}
        type="ratio"
      />
      <MetricCard
        label={camelCaseToTitleCase('totalReturn')}
        value={metrics.totalReturn}
        type="percentage"
      />
      <MetricCard
        label={camelCaseToTitleCase('maxDrawdown')}
        value={metrics.maxDrawdown}
        type="percentage"
      />
      <MetricCard
        label={camelCaseToTitleCase('winRate')}
        value={metrics.winRate}
        type="percentage"
      />
      <MetricCard
        label={camelCaseToTitleCase('profitFactor')}
        value={metrics.profitFactor}
        type="ratio"
      />
      <MetricCard
        label={camelCaseToTitleCase('totalTrades')}
        value={metrics.totalTrades}
        type="count"
      />
    </div>
  );
}

/**
 * Loading skeleton component displayed while metrics data is being loaded
 */
function MetricsGridSkeleton({ className = '' }: { className?: string }) {
  // Create 7 skeleton cards (matching the number of metrics)
  const skeletonCards = Array.from({ length: 7 }, (_, i) => i);

  return (
    <div className={`grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 ${className}`}>
      {skeletonCards.map((index) => (
        <div
          key={index}
          className="metric-card bg-white p-4 rounded-lg shadow animate-pulse"
        >
          <div className="h-4 bg-gray-200 rounded w-3/4 mb-2"></div>
          <div className="h-8 bg-gray-200 rounded w-1/2"></div>
        </div>
      ))}
    </div>
  );
}
