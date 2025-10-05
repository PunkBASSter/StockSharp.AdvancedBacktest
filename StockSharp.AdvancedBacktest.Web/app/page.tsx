import Header from '@/components/layout/Header';
import Container from '@/components/layout/Container';

export default function Home() {
  return (
    <div className="flex min-h-screen flex-col">
      <Header />
      <main className="flex-1">
        <Container>
          <div className="flex flex-col gap-8">
            <div className="rounded-lg border border-gray-200 bg-white p-6 shadow-sm dark:border-gray-800 dark:bg-gray-900">
              <h2 className="mb-4 text-xl font-semibold text-gray-900 dark:text-white">
                Welcome
              </h2>
              <p className="text-gray-600 dark:text-gray-400">
                This visualization tool displays backtesting results from StockSharp Advanced Backtest.
                Chart components will be integrated in upcoming releases.
              </p>
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
