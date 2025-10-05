'use client';
import { useState, useEffect } from 'react';
import Header from '@/components/layout/Header';
import Container from '@/components/layout/Container';
import CandlestickChart from '@/components/charts/CandlestickChart';
import EquityCurveChart from '@/components/charts/EquityCurveChart';
import { loadChartData } from '@/lib/data-loader';
import { ChartDataModel } from '@/types/chart-data';

export default function Home() {
  const [chartData, setChartData] = useState<ChartDataModel | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    async function fetchData() {
      try {
        setLoading(true);
        const data = await loadChartData('/mock-data.json');
        setChartData(data);
        setError(null);
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Failed to load chart data');
        console.error('Failed to load chart data:', err);
      } finally {
        setLoading(false);
      }
    }

    fetchData();
  }, []);

  return (
    <div className="flex min-h-screen flex-col">
      <Header />
      <main className="flex-1">
        <Container>
          <div className="flex flex-col gap-8">
            <div className="rounded-lg border border-gray-200 bg-white p-6 shadow-sm dark:border-gray-800 dark:bg-gray-900">
              <h2 className="mb-4 text-xl font-semibold text-gray-900 dark:text-white">
                Backtest Results
              </h2>

              {loading && (
                <div className="flex items-center justify-center py-12">
                  <div className="text-gray-600 dark:text-gray-400">
                    Loading chart data...
                  </div>
                </div>
              )}

              {error && (
                <div className="rounded-md bg-red-50 p-4 dark:bg-red-900/20">
                  <div className="flex">
                    <div className="ml-3">
                      <h3 className="text-sm font-medium text-red-800 dark:text-red-400">
                        Error loading chart data
                      </h3>
                      <div className="mt-2 text-sm text-red-700 dark:text-red-300">
                        {error}
                      </div>
                    </div>
                  </div>
                </div>
              )}

              {!loading && !error && chartData && (
                <div className="mt-4 space-y-6">
                  <div>
                    <h3 className="mb-2 text-lg font-medium text-gray-800 dark:text-gray-200">
                      Price Chart
                    </h3>
                    <CandlestickChart data={chartData} />
                  </div>

                  <div>
                    <h3 className="mb-2 text-lg font-medium text-gray-800 dark:text-gray-200">
                      Equity Curve
                    </h3>
                    <EquityCurveChart trades={chartData.trades} />
                  </div>
                </div>
              )}
            </div>
          </div>
        </Container>
      </main>
      <footer className="border-t border-gray-200 dark:border-gray-800">
        <Container className="py-6">
          <p className="text-center text-sm text-gray-500 dark:text-gray-400">
            Powered by Next.js and TradingView Charts
          </p>
        </Container>
      </footer>
    </div>
  );
}
