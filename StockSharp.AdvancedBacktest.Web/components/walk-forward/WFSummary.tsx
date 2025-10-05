import { WalkForwardDataModel } from '@/types';

export interface WFSummaryProps {
  walkForward: WalkForwardDataModel;
}

/**
 * WFSummary component displays aggregate walk-forward validation metrics
 * Shows efficiency, consistency, and total windows count
 * Only displayed when walkForward data is present
 */
export default function WFSummary({ walkForward }: WFSummaryProps) {
  // Color-code efficiency: >0.5 green, 0.3-0.5 yellow, <0.3 red
  const efficiencyColor =
    walkForward.walkForwardEfficiency >= 0.5 ? 'text-green-600' :
    walkForward.walkForwardEfficiency >= 0.3 ? 'text-yellow-600' : 'text-red-600';

  // Determine status label based on efficiency
  const efficiencyStatus =
    walkForward.walkForwardEfficiency >= 0.5 ? 'Robust' :
    walkForward.walkForwardEfficiency >= 0.3 ? 'Marginal' : 'Overfit';

  return (
    <div className="wf-summary bg-white p-6 rounded-lg shadow hover:shadow-md transition-shadow">
      <h3 className="text-xl font-semibold mb-4">Walk-Forward Validation</h3>
      <div className="grid grid-cols-3 gap-4">
        {/* Walk-Forward Efficiency */}
        <div>
          <div
            className="text-sm text-gray-600 mb-1"
            title="Ratio of out-of-sample (testing) performance to in-sample (training) performance. Values >0.5 indicate robust strategies."
          >
            WF Efficiency
          </div>
          <div className={`text-3xl font-bold ${efficiencyColor}`}>
            {walkForward.walkForwardEfficiency.toFixed(2)}
          </div>
          <div className="text-xs text-gray-500 mt-1">
            {efficiencyStatus}
          </div>
        </div>

        {/* Consistency */}
        <div>
          <div
            className="text-sm text-gray-600 mb-1"
            title="Standard deviation of performance across windows. Lower values indicate more consistent strategy performance."
          >
            Consistency
          </div>
          <div className="text-3xl font-bold text-gray-900">
            {walkForward.consistency.toFixed(2)}
          </div>
          <div className="text-xs text-gray-500 mt-1">
            Std Dev
          </div>
        </div>

        {/* Total Windows */}
        <div>
          <div
            className="text-sm text-gray-600 mb-1"
            title="Total number of walk-forward validation windows. More windows provide more reliable validation results."
          >
            Total Windows
          </div>
          <div className="text-3xl font-bold text-gray-900">
            {walkForward.totalWindows}
          </div>
          <div className="text-xs text-gray-500 mt-1">
            Windows
          </div>
        </div>
      </div>
    </div>
  );
}
