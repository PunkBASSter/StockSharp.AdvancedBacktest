using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ecng.Configuration;
using Ecng.Logging;
using StockSharp.Algo;
using StockSharp.Algo.Strategies;
using StockSharp.BusinessEntities;
using StockSharp.Messages;
using StockSharp.AdvancedBacktest.Parameters;
using StockSharp.AdvancedBacktest.Strategies;

namespace StockSharp.AdvancedBacktest.Optimization;

public abstract class LauncherBase<TStrategy> : IDisposable
	where TStrategy : CustomStrategyBase, new()
{
	protected virtual CustomParamsContainer ParamsContainer { get; set; } = new();
	protected virtual Portfolio Portfolio { get; set; }
	protected virtual LogManager LogManager { get; set; } = new();
	public virtual CancellationTokenSource CancellationTokenSrc { get; set; } = new();
	protected List<Func<IDictionary<string, ICustomParam>, bool>> ParamValidationRules = new();

	public Task? StrategyTask { get; private set; } = null!;

	public LauncherBase()
	{
		//Strategy = Activator.CreateInstance<TStrategy>();
		ConfigManager.RegisterService<IExchangeInfoProvider>(new InMemoryExchangeInfoProvider());
		Portfolio = new Portfolio { Name = "Default", BeginValue = 5000 };
		//InitSecurities(securities);
	}

	/// <summary>
	/// Launches the strategy in a separate task.
	/// </summary>
	/// <returns></returns>
	public virtual LauncherBase<TStrategy> Launch()
	{
		StrategyTask = Task.Factory.StartNew(
			() => LaunchStrategy(CancellationTokenSrc.Token),
			CancellationTokenSrc.Token,
			TaskCreationOptions.LongRunning,
			TaskScheduler.Default);
		return this;
	}

	public virtual LauncherBase<TStrategy> WithStrategyParams(params ICustomParam[] parameters)
	{
		if (parameters == null || !parameters.Any())
			throw new ArgumentException("Parameters cannot be null or empty.", nameof(parameters));

		ParamsContainer.CustomParams.AddRange(parameters);
		return this;
	}

	public virtual LauncherBase<TStrategy> WithParamValidation(Func<IDictionary<string, ICustomParam>, bool> filter)
	{
		if (filter == null)
			throw new ArgumentNullException(nameof(filter));

		ParamValidationRules.Add(filter);
		return this;
	}

	public virtual LauncherBase<TStrategy> WithPortfolio(Portfolio portfolio)
	{
		if (portfolio == null)
			throw new ArgumentNullException(nameof(portfolio));

		Portfolio = portfolio;
		return this;
	}

	protected virtual void LaunchStrategy(CancellationToken ct)
	{
		//The init code below is relevant for backtesting and maybe live trading, not optimization.
		//DataConnector.Connect();
		//DataConnector.Start(); // was used for backtests
		//ConnectorSubscribeToSecurities();
		//Strategy.Start();

		while (!ct.IsCancellationRequested && !IsExitRequired())
		{
			Thread.Sleep(100);
		}
	}

	protected abstract bool IsExitRequired();

	//private void ConnectorSubscribeToSecurities()
	//{
	//	foreach (var security in Strategy.Securities.Keys)
	//	{
	//		if (security == null)
	//			throw new ArgumentNullException(nameof(security));
	//		foreach (var timeFrame in Strategy.Securities[security])
	//		{
	//			var subscription = new Subscription(timeFrame.TimeFrame(), security);
	//			SetCandleSubscriptionDetails(subscription);
	//			//DataConnector.Subscribe(subscription);
	//		}
	//	}
	//}

	#region Disposable Support

	private bool _disposed;
	private readonly object _disposeLock = new();

	/// <summary>
	/// Releases all resources used by the object.
	/// </summary>
	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Releases the unmanaged resources used by the object and optionally releases the managed resources.
	/// </summary>
	/// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
	private void Dispose(bool disposing)
	{
		lock (_disposeLock)
		{
			if (_disposed)
				return;

			if (disposing)
			{
				DisposeInherited();
				//DataConnector?.Disconnect();
				LogManager.Dispose();
				//Strategy?.Dispose();
				//Strategy = null;
				//Strategy.Securities.Clear();
			}

			_disposed = true;
		}
	}

	protected abstract void DisposeInherited();

	~LauncherBase()
	{
		Dispose(false);
	}

	#endregion
}
