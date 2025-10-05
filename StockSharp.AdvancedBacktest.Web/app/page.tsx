export default function Home() {
  return (
    <div className="grid grid-rows-[20px_1fr_20px] items-center justify-items-center min-h-screen p-8 pb-20 gap-16 sm:p-20 font-[family-name:var(--font-geist-sans)]">
      <main className="flex flex-col gap-8 row-start-2 items-center sm:items-start">
        <h1 className="text-4xl font-bold">StockSharp Advanced Backtest</h1>
        <p className="text-sm text-center sm:text-left">
          Visualization for StockSharp backtesting results
        </p>
      </main>
      <footer className="row-start-3 flex gap-6 flex-wrap items-center justify-center">
        <p className="text-sm text-gray-500">
          Powered by Next.js and TradingView Charts
        </p>
      </footer>
    </div>
  );
}
