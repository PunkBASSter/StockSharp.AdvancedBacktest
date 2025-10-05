import { WalkForwardWindowData } from '@/types';
import { useState } from 'react';

export interface WFTimelineProps {
  windows: WalkForwardWindowData[];
}

/**
 * WFTimeline component displays a timeline visualization showing all walk-forward windows
 * with visual representation of training/testing periods overlap.
 * Each window is shown as a horizontal bar with training period in blue and testing in orange.
 */
export default function WFTimeline({ windows }: WFTimelineProps) {
  const [hoveredWindow, setHoveredWindow] = useState<number | null>(null);

  if (!windows || windows.length === 0) {
    return (
      <div className="wf-timeline bg-white p-6 rounded-lg shadow hover:shadow-md transition-shadow">
        <h3 className="text-xl font-semibold mb-4">Walk-Forward Timeline</h3>
        <div className="text-center py-8 text-gray-500">
          No walk-forward windows available
        </div>
      </div>
    );
  }

  // Calculate min and max dates across all windows
  const minDate = Math.min(...windows.map(w => w.trainingStart));
  const maxDate = Math.max(...windows.map(w => w.testingEnd));

  // SVG dimensions
  const totalWidth = 800;
  const labelWidth = 60;
  const timelineWidth = totalWidth - labelWidth;
  const rowHeight = 50;
  const barHeight = 15;
  const legendHeight = 60;
  const svgHeight = windows.length * rowHeight + legendHeight;

  /**
   * Scale a Unix timestamp to SVG X coordinate
   * @param timestamp Unix timestamp in seconds
   * @returns X coordinate in pixels
   */
  const scaleDate = (timestamp: number): number => {
    return labelWidth + ((timestamp - minDate) / (maxDate - minDate)) * timelineWidth;
  };

  /**
   * Format Unix timestamp as readable date
   * @param timestamp Unix timestamp in seconds
   * @returns Formatted date string
   */
  const formatDate = (timestamp: number): string => {
    return new Date(timestamp * 1000).toLocaleDateString();
  };

  /**
   * Format Unix timestamp range as readable date range
   * @param start Unix timestamp in seconds
   * @param end Unix timestamp in seconds
   * @returns Formatted date range string
   */
  const formatDateRange = (start: number, end: number): string => {
    return `${formatDate(start)} - ${formatDate(end)}`;
  };

  /**
   * Determine if a window is anchored or rolling
   * Anchored: training start stays the same
   * Rolling: training start moves forward
   * @param index Window index
   * @returns true if window is anchored
   */
  const isAnchored = (index: number): boolean => {
    if (index === 0) return true;
    return windows[index].trainingStart === windows[0].trainingStart;
  };

  return (
    <div className="wf-timeline bg-white p-6 rounded-lg shadow hover:shadow-md transition-shadow">
      <h3 className="text-xl font-semibold mb-4">Walk-Forward Timeline</h3>

      <div className="overflow-x-auto">
        <svg
          width="100%"
          height={svgHeight}
          className="min-w-[800px]"
          viewBox={`0 0 ${totalWidth} ${svgHeight}`}
        >
          {/* Timeline axis */}
          <line
            x1={labelWidth}
            y1="10"
            x2={totalWidth}
            y2="10"
            stroke="#ccc"
            strokeWidth="2"
          />

          {/* Date markers on timeline axis */}
          {[0, 0.25, 0.5, 0.75, 1].map((ratio, idx) => {
            const timestamp = minDate + (maxDate - minDate) * ratio;
            const x = scaleDate(timestamp);
            return (
              <g key={idx}>
                <line x1={x} y1="10" x2={x} y2="15" stroke="#999" strokeWidth="1" />
                <text
                  x={x}
                  y="25"
                  fontSize="10"
                  fill="#666"
                  textAnchor="middle"
                >
                  {formatDate(timestamp)}
                </text>
              </g>
            );
          })}

          {/* Walk-forward windows */}
          {windows.map((w, idx) => {
            const trainStart = scaleDate(w.trainingStart);
            const trainEnd = scaleDate(w.trainingEnd);
            const testStart = scaleDate(w.testingStart);
            const testEnd = scaleDate(w.testingEnd);
            const y = idx * rowHeight + 40;
            const anchored = isAnchored(idx);
            const isHovered = hoveredWindow === w.windowNumber;

            return (
              <g
                key={w.windowNumber}
                onMouseEnter={() => setHoveredWindow(w.windowNumber)}
                onMouseLeave={() => setHoveredWindow(null)}
                style={{ cursor: 'pointer' }}
              >
                {/* Window label */}
                <text
                  x="5"
                  y={y + 5}
                  fontSize="12"
                  fill="#666"
                  fontWeight={isHovered ? 'bold' : 'normal'}
                >
                  W{w.windowNumber}
                </text>

                {/* Anchored/Rolling indicator */}
                {anchored ? (
                  <circle cx="45" cy={y} r="3" fill="#4CAF50" />
                ) : (
                  <circle cx="45" cy={y} r="3" fill="#9C27B0" />
                )}

                {/* Training period bar */}
                <rect
                  x={trainStart}
                  y={y - barHeight / 2}
                  width={trainEnd - trainStart}
                  height={barHeight}
                  fill="#2196F3"
                  opacity={isHovered ? 0.9 : 0.7}
                  stroke={isHovered ? '#1976D2' : 'none'}
                  strokeWidth={isHovered ? 2 : 0}
                >
                  <title>
                    Training: {formatDateRange(w.trainingStart, w.trainingEnd)}
                    {'\n'}Return: {(w.trainingMetrics.totalReturn * 100).toFixed(2)}%
                    {'\n'}Sharpe: {w.trainingMetrics.sharpeRatio.toFixed(2)}
                    {'\n'}Trades: {w.trainingMetrics.totalTrades}
                  </title>
                </rect>

                {/* Testing period bar */}
                <rect
                  x={testStart}
                  y={y - barHeight / 2}
                  width={testEnd - testStart}
                  height={barHeight}
                  fill="#FF9800"
                  opacity={isHovered ? 0.9 : 0.7}
                  stroke={isHovered ? '#F57C00' : 'none'}
                  strokeWidth={isHovered ? 2 : 0}
                >
                  <title>
                    Testing: {formatDateRange(w.testingStart, w.testingEnd)}
                    {'\n'}Return: {(w.testingMetrics.totalReturn * 100).toFixed(2)}%
                    {'\n'}Sharpe: {w.testingMetrics.sharpeRatio.toFixed(2)}
                    {'\n'}Trades: {w.testingMetrics.totalTrades}
                    {'\n'}Degradation: {(w.performanceDegradation * 100).toFixed(2)}%
                  </title>
                </rect>
              </g>
            );
          })}

          {/* Legend */}
          <g transform={`translate(${labelWidth}, ${windows.length * rowHeight + 40})`}>
            {/* Training Legend */}
            <rect x="0" y="0" width="20" height="15" fill="#2196F3" opacity="0.7" />
            <text x="25" y="12" fontSize="12" fill="#666">Training</text>

            {/* Testing Legend */}
            <rect x="100" y="0" width="20" height="15" fill="#FF9800" opacity="0.7" />
            <text x="125" y="12" fontSize="12" fill="#666">Testing</text>

            {/* Anchored Window Indicator */}
            <circle cx="230" cy="7" r="3" fill="#4CAF50" />
            <text x="240" y="12" fontSize="12" fill="#666">Anchored</text>

            {/* Rolling Window Indicator */}
            <circle cx="330" cy="7" r="3" fill="#9C27B0" />
            <text x="340" y="12" fontSize="12" fill="#666">Rolling</text>
          </g>
        </svg>
      </div>

      {/* Hover tooltip details */}
      {hoveredWindow !== null && (
        <div className="mt-4 p-4 bg-gray-50 rounded border border-gray-200">
          {(() => {
            const w = windows.find(win => win.windowNumber === hoveredWindow);
            if (!w) return null;

            return (
              <div className="grid grid-cols-2 gap-4 text-sm">
                <div>
                  <h4 className="font-semibold mb-2">Window #{w.windowNumber}</h4>
                  <p className="text-gray-600">
                    Training: {formatDateRange(w.trainingStart, w.trainingEnd)}
                  </p>
                  <p className="text-gray-600">
                    Testing: {formatDateRange(w.testingStart, w.testingEnd)}
                  </p>
                </div>
                <div>
                  <h4 className="font-semibold mb-2">Performance</h4>
                  <p className={w.trainingMetrics.totalReturn >= 0 ? 'text-green-600' : 'text-red-600'}>
                    Train Return: {(w.trainingMetrics.totalReturn * 100).toFixed(2)}%
                  </p>
                  <p className={w.testingMetrics.totalReturn >= 0 ? 'text-green-600' : 'text-red-600'}>
                    Test Return: {(w.testingMetrics.totalReturn * 100).toFixed(2)}%
                  </p>
                  <p className={w.performanceDegradation > -10 ? 'text-green-600' : w.performanceDegradation > -20 ? 'text-yellow-600' : 'text-red-600'}>
                    Degradation: {(w.performanceDegradation * 100).toFixed(2)}%
                  </p>
                </div>
              </div>
            );
          })()}
        </div>
      )}
    </div>
  );
}
