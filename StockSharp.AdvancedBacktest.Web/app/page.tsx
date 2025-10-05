'use client';
import CandlestickChart from '@/components/charts/CandlestickChart';
import EquityCurveChart from '@/components/charts/EquityCurveChart';
import Container from '@/components/layout/Container';
import Header from '@/components/layout/Header';
import LoadingState from '@/components/LoadingState';
import { loadChartData } from '@/lib/data-loader';
import { ChartDataModel, WalkForwardWindowData } from '@/types/chart-data';
import dynamic from 'next/dynamic';
import { useEffect, useState } from 'react';

// Lazy load Walk-Forward components for better performance
const WFComparisonChart = dynamic(
    () => import('@/components/walk-forward/WFComparisonChart'),
    {
        loading: () => <LoadingState />,
        ssr: false,
    }
);

const WFTimeline = dynamic(
    () => import('@/components/walk-forward/WFTimeline'),
    {
        loading: () => <LoadingState />,
        ssr: false,
    }
);

export default function Home() {
    const [chartData, setChartData] = useState<ChartDataModel | null>(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    const fetchData = async () => {
        try {
            setLoading(true);
            setError(null);
            const data = await loadChartData('/mock-data.json');
            setChartData(data);
        } catch (err) {
            setError(err instanceof Error ? err.message : 'Failed to load chart data');
            console.error('Failed to load chart data:', err);
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        fetchData();
    }, []);

    // Sample walk-forward data for testing WFComparisonChart
    const sampleWalkForwardData: WalkForwardWindowData[] = [
        {
            windowNumber: 1,
            trainingStart: 1609459200,
            trainingEnd: 1612137600,
            testingStart: 1612137600,
            testingEnd: 1614556800,
            trainingMetrics: {
                startTime: 1609459200, endTime: 1612137600, totalReturn: 0.12, sharpeRatio: 1.5,
                maxDrawdown: -0.05, winRate: 0.6, profitFactor: 2.1, totalTrades: 45, winningTrades: 27,
                losingTrades: 18, annualizedReturn: 0.36, sortinoRatio: 1.8, averageWin: 150, averageLoss: -75,
                grossProfit: 4050, grossLoss: -1350, netProfit: 2700, initialCapital: 10000, finalValue: 12700,
                tradingPeriodDays: 31, averageTradesPerDay: 1.45
            },
            testingMetrics: {
                startTime: 1612137600, endTime: 1614556800, totalReturn: 0.08, sharpeRatio: 1.2,
                maxDrawdown: -0.07, winRate: 0.55, profitFactor: 1.8, totalTrades: 20, winningTrades: 11,
                losingTrades: 9, annualizedReturn: 0.24, sortinoRatio: 1.4, averageWin: 140, averageLoss: -80,
                grossProfit: 1540, grossLoss: -720, netProfit: 820, initialCapital: 10000, finalValue: 10820,
                tradingPeriodDays: 28, averageTradesPerDay: 0.71
            },
            performanceDegradation: 0.33
        },
        {
            windowNumber: 2,
            trainingStart: 1612137600,
            trainingEnd: 1614556800,
            testingStart: 1614556800,
            testingEnd: 1617235200,
            trainingMetrics: {
                startTime: 1612137600, endTime: 1614556800, totalReturn: 0.15, sharpeRatio: 1.8,
                maxDrawdown: -0.04, winRate: 0.65, profitFactor: 2.5, totalTrades: 50, winningTrades: 33,
                losingTrades: 17, annualizedReturn: 0.45, sortinoRatio: 2.2, averageWin: 160, averageLoss: -65,
                grossProfit: 5280, grossLoss: -1105, netProfit: 4175, initialCapital: 10000, finalValue: 14175,
                tradingPeriodDays: 28, averageTradesPerDay: 1.79
            },
            testingMetrics: {
                startTime: 1614556800, endTime: 1617235200, totalReturn: 0.11, sharpeRatio: 1.4,
                maxDrawdown: -0.06, winRate: 0.58, profitFactor: 2.0, totalTrades: 22, winningTrades: 13,
                losingTrades: 9, annualizedReturn: 0.33, sortinoRatio: 1.7, averageWin: 145, averageLoss: -75,
                grossProfit: 1885, grossLoss: -675, netProfit: 1210, initialCapital: 10000, finalValue: 11210,
                tradingPeriodDays: 31, averageTradesPerDay: 0.71
            },
            performanceDegradation: 0.27
        },
        {
            windowNumber: 3,
            trainingStart: 1614556800,
            trainingEnd: 1617235200,
            testingStart: 1617235200,
            testingEnd: 1619827200,
            trainingMetrics: {
                startTime: 1614556800, endTime: 1617235200, totalReturn: 0.10, sharpeRatio: 1.3,
                maxDrawdown: -0.06, winRate: 0.58, profitFactor: 1.9, totalTrades: 42, winningTrades: 24,
                losingTrades: 18, annualizedReturn: 0.30, sortinoRatio: 1.6, averageWin: 135, averageLoss: -72,
                grossProfit: 3240, grossLoss: -1296, netProfit: 1944, initialCapital: 10000, finalValue: 11944,
                tradingPeriodDays: 31, averageTradesPerDay: 1.35
            },
            testingMetrics: {
                startTime: 1617235200, endTime: 1619827200, totalReturn: 0.07, sharpeRatio: 1.1,
                maxDrawdown: -0.08, winRate: 0.52, profitFactor: 1.6, totalTrades: 18, winningTrades: 9,
                losingTrades: 9, annualizedReturn: 0.21, sortinoRatio: 1.3, averageWin: 130, averageLoss: -78,
                grossProfit: 1170, grossLoss: -702, netProfit: 468, initialCapital: 10000, finalValue: 10468,
                tradingPeriodDays: 30, averageTradesPerDay: 0.60
            },
            performanceDegradation: 0.30
        },
        {
            windowNumber: 4,
            trainingStart: 1617235200,
            trainingEnd: 1619827200,
            testingStart: 1619827200,
            testingEnd: 1622505600,
            trainingMetrics: {
                startTime: 1617235200, endTime: 1619827200, totalReturn: 0.18, sharpeRatio: 2.0,
                maxDrawdown: -0.03, winRate: 0.68, profitFactor: 2.8, totalTrades: 55, winningTrades: 37,
                losingTrades: 18, annualizedReturn: 0.54, sortinoRatio: 2.5, averageWin: 170, averageLoss: -62,
                grossProfit: 6290, grossLoss: -1116, netProfit: 5174, initialCapital: 10000, finalValue: 15174,
                tradingPeriodDays: 30, averageTradesPerDay: 1.83
            },
            testingMetrics: {
                startTime: 1619827200, endTime: 1622505600, totalReturn: 0.14, sharpeRatio: 1.6,
                maxDrawdown: -0.05, winRate: 0.62, profitFactor: 2.2, totalTrades: 25, winningTrades: 16,
                losingTrades: 9, annualizedReturn: 0.42, sortinoRatio: 2.0, averageWin: 155, averageLoss: -68,
                grossProfit: 2480, grossLoss: -612, netProfit: 1868, initialCapital: 10000, finalValue: 11868,
                tradingPeriodDays: 31, averageTradesPerDay: 0.81
            },
            performanceDegradation: 0.22
        },
    ];

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

                            {loading && <LoadingState />}

                            {error && (
                                <div className="rounded-md bg-red-50 p-4 dark:bg-red-900/20">
                                    <div className="flex flex-col">
                                        <div className="ml-3">
                                            <h3 className="text-sm font-medium text-red-800 dark:text-red-400">
                                                Error loading chart data
                                            </h3>
                                            <div className="mt-2 text-sm text-red-700 dark:text-red-300">
                                                {error}
                                            </div>
                                        </div>
                                        <button
                                            onClick={fetchData}
                                            className="mt-4 px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700 transition-colors self-start ml-3"
                                        >
                                            Retry
                                        </button>
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

                                    <div>
                                        <h3 className="mb-2 text-lg font-medium text-gray-800 dark:text-gray-200">
                                            Walk-Forward Analysis
                                        </h3>
                                        <WFComparisonChart windows={sampleWalkForwardData} />
                                    </div>

                                    <div>
                                        <h3 className="mb-2 text-lg font-medium text-gray-800 dark:text-gray-200">
                                            Walk-Forward Timeline
                                        </h3>
                                        <WFTimeline windows={sampleWalkForwardData} />
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
