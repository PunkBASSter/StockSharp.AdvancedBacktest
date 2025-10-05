'use client';
import { useEffect, useRef } from 'react';
import {
  createChart,
  ColorType,
  IChartApi,
  LineSeries,
  AreaSeries,
  LineStyle,
  UTCTimestamp,
} from 'lightweight-charts';
import { TradeDataPoint } from '@/types/chart-data';

interface Props {
  trades: TradeDataPoint[];
}

interface EquityDataPoint {
  time: UTCTimestamp;
  value: number;
}

export default function EquityCurveChart({ trades }: Props) {
  const chartContainerRef = useRef<HTMLDivElement>(null);
  const chartRef = useRef<IChartApi | null>(null);

  useEffect(() => {
    if (!chartContainerRef.current) return;

    // Create chart instance
    const chart = createChart(chartContainerRef.current, {
      width: chartContainerRef.current.clientWidth,
      height: 300,
      layout: {
        textColor: '#333',
        background: { type: ColorType.Solid, color: '#ffffff' },
      },
      grid: {
        vertLines: { color: '#e1e1e1' },
        horzLines: { color: '#e1e1e1' },
      },
      rightPriceScale: {
        borderColor: '#D1D4DC',
        // Currency formatting
        mode: 0, // Normal mode
      },
      timeScale: {
        borderColor: '#D1D4DC',
        timeVisible: true,
        secondsVisible: false,
      },
      crosshair: {
        mode: 1, // Magnet mode for better hover experience
      },
    });

    chartRef.current = chart;

    // Calculate cumulative P&L from trades
    let cumulativePnL = 0;
    const equityData: EquityDataPoint[] = trades.map((trade) => {
      cumulativePnL += trade.pnL;
      return {
        time: trade.time as UTCTimestamp,
        value: cumulativePnL,
      };
    });

    // Add line series for equity curve
    const lineSeries = chart.addSeries(LineSeries, {
      color: '#2196F3',
      lineWidth: 2,
      title: 'Equity',
      priceFormat: {
        type: 'price',
        precision: 2,
        minMove: 0.01,
      },
    });

    lineSeries.setData(equityData);

    // Calculate drawdown data
    let peak = 0;
    const drawdownData: EquityDataPoint[] = [];

    equityData.forEach((point) => {
      peak = Math.max(peak, point.value);
      const drawdown = point.value - peak;

      // Only add drawdown points when there's an actual drawdown
      if (drawdown < 0) {
        drawdownData.push({
          time: point.time,
          value: point.value,
        });
      } else {
        // When equity recovers to peak, close the drawdown area
        if (drawdownData.length > 0) {
          drawdownData.push({
            time: point.time,
            value: peak,
          });
        }
      }
    });

    // Add area series for drawdown visualization
    if (drawdownData.length > 0) {
      const areaSeries = chart.addSeries(AreaSeries, {
        topColor: 'rgba(244, 67, 54, 0.3)',
        bottomColor: 'rgba(244, 67, 54, 0.1)',
        lineColor: 'rgba(244, 67, 54, 0.8)',
        lineWidth: 1,
        lineStyle: LineStyle.Solid,
        priceFormat: {
          type: 'price',
          precision: 2,
          minMove: 0.01,
        },
        title: 'Drawdown',
      });

      // Create drawdown area data by connecting peak values
      const drawdownAreaData: EquityDataPoint[] = [];
      let currentPeak = peak;

      equityData.forEach((point) => {
        currentPeak = Math.max(currentPeak, point.value);
        const drawdown = point.value - currentPeak;

        if (drawdown < 0) {
          // During drawdown, show the actual equity value
          drawdownAreaData.push({
            time: point.time,
            value: point.value,
          });
        } else {
          // At peak, show peak value to create filled area effect
          drawdownAreaData.push({
            time: point.time,
            value: currentPeak,
          });
        }
      });

      areaSeries.setData(drawdownAreaData);
    }

    // Fit content to show all data
    chart.timeScale().fitContent();

    // Handle window resize
    const handleResize = () => {
      if (chartContainerRef.current && chartRef.current) {
        chartRef.current.applyOptions({
          width: chartContainerRef.current.clientWidth,
        });
      }
    };

    window.addEventListener('resize', handleResize);

    // Cleanup
    return () => {
      window.removeEventListener('resize', handleResize);
      chart.remove();
      chartRef.current = null;
    };
  }, [trades]);

  return (
    <div className="relative w-full">
      <div ref={chartContainerRef} className="w-full h-[300px]" />

      {/* Chart Legend */}
      <div className="absolute top-4 left-4 bg-white/90 backdrop-blur-sm border border-gray-200 rounded-lg shadow-md p-3 space-y-2">
        <div className="flex items-center gap-2">
          <div className="w-6 h-0.5 bg-[#2196F3]" />
          <span className="text-sm font-medium text-gray-700">Equity Curve</span>
        </div>
        <div className="flex items-center gap-2">
          <div className="w-6 h-3 bg-[rgba(244,67,54,0.3)] border-t border-[rgba(244,67,54,0.8)]" />
          <span className="text-sm font-medium text-gray-700">Drawdown</span>
        </div>
      </div>
    </div>
  );
}
