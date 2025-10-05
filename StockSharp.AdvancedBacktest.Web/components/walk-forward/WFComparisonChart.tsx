'use client';

import { useState } from 'react';
import { Bar } from 'react-chartjs-2';
import {
  Chart as ChartJS,
  CategoryScale,
  LinearScale,
  BarElement,
  Title,
  Tooltip,
  Legend,
  ChartOptions,
} from 'chart.js';
import { WalkForwardWindowData } from '@/types';

// Register Chart.js components
ChartJS.register(
  CategoryScale,
  LinearScale,
  BarElement,
  Title,
  Tooltip,
  Legend
);

export interface WFComparisonChartProps {
  windows: WalkForwardWindowData[];
}

type MetricType = 'totalReturn' | 'sharpeRatio';

/**
 * WFComparisonChart component displays a bar chart comparing training vs testing metrics
 * across all walk-forward windows. Supports toggling between Total Return and Sharpe Ratio.
 */
export default function WFComparisonChart({ windows }: WFComparisonChartProps) {
  const [metricType, setMetricType] = useState<MetricType>('totalReturn');

  // Calculate chart data based on selected metric
  const getChartData = () => {
    const labels = windows.map((w) => `Window ${w.windowNumber}`);

    if (metricType === 'totalReturn') {
      return {
        labels,
        datasets: [
          {
            label: 'Training Return (%)',
            data: windows.map((w) => w.trainingMetrics.totalReturn * 100),
            backgroundColor: 'rgba(33, 150, 243, 0.6)',
            borderColor: 'rgb(33, 150, 243)',
            borderWidth: 1,
          },
          {
            label: 'Testing Return (%)',
            data: windows.map((w) => w.testingMetrics.totalReturn * 100),
            backgroundColor: 'rgba(255, 152, 0, 0.6)',
            borderColor: 'rgb(255, 152, 0)',
            borderWidth: 1,
          },
        ],
      };
    } else {
      return {
        labels,
        datasets: [
          {
            label: 'Training Sharpe Ratio',
            data: windows.map((w) => w.trainingMetrics.sharpeRatio),
            backgroundColor: 'rgba(33, 150, 243, 0.6)',
            borderColor: 'rgb(33, 150, 243)',
            borderWidth: 1,
          },
          {
            label: 'Testing Sharpe Ratio',
            data: windows.map((w) => w.testingMetrics.sharpeRatio),
            backgroundColor: 'rgba(255, 152, 0, 0.6)',
            borderColor: 'rgb(255, 152, 0)',
            borderWidth: 1,
          },
        ],
      };
    }
  };

  const options: ChartOptions<'bar'> = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: {
        position: 'top' as const,
      },
      title: {
        display: true,
        text: 'Walk-Forward Performance Comparison',
        font: {
          size: 16,
          weight: 'bold',
        },
      },
      tooltip: {
        callbacks: {
          label: (context) => {
            const label = context.dataset.label || '';
            const value = context.parsed.y;
            if (metricType === 'totalReturn') {
              return `${label}: ${value.toFixed(2)}%`;
            } else {
              return `${label}: ${value.toFixed(2)}`;
            }
          },
        },
      },
    },
    scales: {
      y: {
        ticks: {
          callback: (value) => {
            if (metricType === 'totalReturn') {
              return `${value}%`;
            }
            return value.toString();
          },
        },
        title: {
          display: true,
          text: metricType === 'totalReturn' ? 'Total Return (%)' : 'Sharpe Ratio',
        },
      },
      x: {
        title: {
          display: true,
          text: 'Window',
        },
      },
    },
  };

  return (
    <div className="wf-comparison-chart bg-white p-6 rounded-lg shadow hover:shadow-md transition-shadow">
      {/* Metric Toggle */}
      <div className="flex justify-between items-center mb-4">
        <h3 className="text-xl font-semibold">Performance Comparison</h3>
        <div className="flex gap-2">
          <button
            onClick={() => setMetricType('totalReturn')}
            className={`px-4 py-2 rounded-lg font-medium transition-colors ${
              metricType === 'totalReturn'
                ? 'bg-blue-500 text-white'
                : 'bg-gray-200 text-gray-700 hover:bg-gray-300'
            }`}
          >
            Total Return
          </button>
          <button
            onClick={() => setMetricType('sharpeRatio')}
            className={`px-4 py-2 rounded-lg font-medium transition-colors ${
              metricType === 'sharpeRatio'
                ? 'bg-blue-500 text-white'
                : 'bg-gray-200 text-gray-700 hover:bg-gray-300'
            }`}
          >
            Sharpe Ratio
          </button>
        </div>
      </div>

      {/* Chart Container */}
      <div className="h-96">
        <Bar data={getChartData()} options={options} />
      </div>

      {/* Legend with color indicators */}
      <div className="mt-4 flex justify-center gap-6 text-sm">
        <div className="flex items-center gap-2">
          <div className="w-4 h-4 bg-blue-500 rounded"></div>
          <span>Training</span>
        </div>
        <div className="flex items-center gap-2">
          <div className="w-4 h-4 bg-orange-500 rounded"></div>
          <span>Testing</span>
        </div>
      </div>

      {/* Information Note */}
      <div className="mt-4 p-3 bg-blue-50 rounded-lg">
        <p className="text-sm text-gray-700">
          {metricType === 'totalReturn' ? (
            <>
              <strong>Total Return:</strong> Compare in-sample (training) vs out-of-sample
              (testing) returns. Testing performance close to training indicates robust strategy.
            </>
          ) : (
            <>
              <strong>Sharpe Ratio:</strong> Risk-adjusted returns comparison. Values &gt; 1
              are good, &gt; 2 are excellent. Consistent values indicate stable strategy.
            </>
          )}
        </p>
      </div>
    </div>
  );
}
