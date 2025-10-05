export default function Header() {
  return (
    <header className="w-full border-b border-gray-200 dark:border-gray-800">
      <div className="mx-auto max-w-7xl px-4 py-6 sm:px-6 lg:px-8">
        <div className="flex flex-col gap-2">
          <h1 className="text-3xl font-bold tracking-tight text-gray-900 dark:text-white sm:text-4xl">
            StockSharp Advanced Backtest
          </h1>
          <p className="text-sm text-gray-600 dark:text-gray-400 sm:text-base">
            Visualization for backtesting results
          </p>
        </div>
      </div>
    </header>
  );
}
