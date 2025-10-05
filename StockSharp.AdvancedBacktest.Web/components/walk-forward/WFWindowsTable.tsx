import { WalkForwardWindowData } from '@/types';

export interface WFWindowsTableProps {
  windows: WalkForwardWindowData[];
}

interface WindowRow {
  windowNumber: number;
  trainingPeriod: string;
  testingPeriod: string;
  trainingReturn: number;
  testingReturn: number;
  degradation: number;
}

/**
 * WFWindowsTable component displays all walk-forward windows with training/testing periods
 * and performance comparison in an interactive table format
 */
export default function WFWindowsTable({ windows }: WFWindowsTableProps) {
  /**
   * Format Unix timestamp range as readable date strings
   * @param start Unix timestamp in seconds
   * @param end Unix timestamp in seconds
   * @returns Formatted date range string (e.g., "1/15/2023 - 3/15/2023")
   */
  const formatDateRange = (start: number, end: number): string => {
    const startDate = new Date(start * 1000).toLocaleDateString();
    const endDate = new Date(end * 1000).toLocaleDateString();
    return `${startDate} - ${endDate}`;
  };

  /**
   * Determine color class for degradation value
   * Green: > -10% (minimal degradation)
   * Yellow: -10% to -20% (moderate degradation)
   * Red: < -20% (severe degradation)
   */
  const getDegradationColor = (deg: number): string => {
    if (deg > -10) return 'text-green-600';
    if (deg > -20) return 'text-yellow-600';
    return 'text-red-600';
  };

  /**
   * Determine color class for return values
   * Green for positive returns, red for negative
   */
  const getReturnColor = (returnValue: number): string => {
    return returnValue >= 0 ? 'text-green-600' : 'text-red-600';
  };

  // Transform window data into table rows
  const rows: WindowRow[] = windows.map(w => ({
    windowNumber: w.windowNumber,
    trainingPeriod: formatDateRange(w.trainingStart, w.trainingEnd),
    testingPeriod: formatDateRange(w.testingStart, w.testingEnd),
    trainingReturn: w.trainingMetrics.totalReturn,
    testingReturn: w.testingMetrics.totalReturn,
    degradation: w.performanceDegradation
  }));

  return (
    <div className="wf-windows-table bg-white p-6 rounded-lg shadow hover:shadow-md transition-shadow">
      <h3 className="text-xl font-semibold mb-4">Walk-Forward Windows</h3>

      {/* Scrollable container for responsive table */}
      <div className="overflow-x-auto">
        <table className="min-w-full bg-white">
          <thead className="bg-gray-100">
            <tr>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-700 uppercase tracking-wider">
                Window
              </th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-700 uppercase tracking-wider">
                Training Period
              </th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-700 uppercase tracking-wider">
                Testing Period
              </th>
              <th className="px-4 py-3 text-right text-xs font-medium text-gray-700 uppercase tracking-wider">
                Train Return
              </th>
              <th className="px-4 py-3 text-right text-xs font-medium text-gray-700 uppercase tracking-wider">
                Test Return
              </th>
              <th className="px-4 py-3 text-right text-xs font-medium text-gray-700 uppercase tracking-wider">
                Degradation
              </th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-200">
            {rows.map(row => (
              <tr
                key={row.windowNumber}
                className="hover:bg-gray-50 transition-colors"
              >
                {/* Window Number */}
                <td className="px-4 py-3 whitespace-nowrap text-sm font-medium text-gray-900">
                  #{row.windowNumber}
                </td>

                {/* Training Period */}
                <td className="px-4 py-3 whitespace-nowrap text-sm text-gray-600">
                  {row.trainingPeriod}
                </td>

                {/* Testing Period */}
                <td className="px-4 py-3 whitespace-nowrap text-sm text-gray-600">
                  {row.testingPeriod}
                </td>

                {/* Training Return */}
                <td className={`px-4 py-3 whitespace-nowrap text-sm font-semibold text-right ${getReturnColor(row.trainingReturn)}`}>
                  {(row.trainingReturn * 100).toFixed(2)}%
                </td>

                {/* Testing Return */}
                <td className={`px-4 py-3 whitespace-nowrap text-sm font-semibold text-right ${getReturnColor(row.testingReturn)}`}>
                  {(row.testingReturn * 100).toFixed(2)}%
                </td>

                {/* Degradation */}
                <td className={`px-4 py-3 whitespace-nowrap text-sm font-semibold text-right ${getDegradationColor(row.degradation)}`}>
                  {(row.degradation * 100).toFixed(2)}%
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Empty state */}
      {rows.length === 0 && (
        <div className="text-center py-8 text-gray-500">
          No walk-forward windows available
        </div>
      )}
    </div>
  );
}
