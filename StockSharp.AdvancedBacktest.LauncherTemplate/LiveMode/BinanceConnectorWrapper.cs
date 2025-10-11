using System.Text.Json;
using Ecng.Common;
using Microsoft.Extensions.Logging;
using StockSharp.Algo;
using StockSharp.Binance;
using StockSharp.BusinessEntities;
using StockSharp.Messages;

namespace StockSharp.AdvancedBacktest.LauncherTemplate.LiveMode;

/// <summary>
/// Wrapper for StockSharp Binance connector integration
/// </summary>
public class BinanceConnectorWrapper : IDisposable
{
    private readonly ILogger<BinanceConnectorWrapper> _logger;
    private Connector? _connector;
    private readonly TaskCompletionSource<bool> _connectionTcs = new();
    private bool _disposed;

    /// <summary>
    /// Indicates whether the connector is currently connected
    /// </summary>
    public bool IsConnected { get; private set; }

    /// <summary>
    /// The underlying StockSharp Connector instance
    /// </summary>
    public Connector? Connector => _connector;

    // Events for connection lifecycle
    public event Action? Connected;
    public event Action<Exception>? ConnectionError;
    public event Action? Disconnected;

    // Events for market data
    public event Action<Security>? SecurityReceived;
    public event Action<Portfolio>? PortfolioReceived;
    public event Action<Position>? PositionChanged;
    public event Action<Order>? OrderReceived;
    public event Action<MyTrade>? TradeReceived;
    public event Action<IOrderBookMessage>? OrderBookReceived;

    public BinanceConnectorWrapper(ILogger<BinanceConnectorWrapper> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> ConnectAsync(string connectorFilePath, CancellationToken cancellationToken = default)
    {
        if (IsConnected)
        {
            _logger.LogWarning("Already connected to Binance");
            return true;
        }

        try
        {
            _logger.LogInformation("Loading Binance configuration from {Path}", connectorFilePath);

            if (!File.Exists(connectorFilePath))
            {
                var error = new FileNotFoundException($"Connector configuration file not found: {connectorFilePath}");
                _logger.LogError(error, "Configuration file not found");
                ConnectionError?.Invoke(error);
                return false;
            }

            var config = LoadConfiguration(connectorFilePath);
            if (config == null)
            {
                var error = new InvalidOperationException("Failed to load connector configuration");
                _logger.LogError(error, "Configuration loading failed");
                ConnectionError?.Invoke(error);
                return false;
            }

            _connector = new Connector();

            var binanceAdapter = new BinanceMessageAdapter(_connector.TransactionIdGenerator)
            {
                Key = config.Key.Secure(),
                Secret = config.Secret.Secure(),
                IsDemo = config.IsDemo
            };

            _connector.Adapter.InnerAdapters.Add(binanceAdapter);

            SubscribeToEvents();

            _logger.LogInformation("Connecting to Binance (Demo={IsDemo})", config.IsDemo);

            _connector.Connect();

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            try
            {
                linkedCts.Token.Register(() => _connectionTcs.TrySetCanceled());
                var connected = await _connectionTcs.Task;
                return connected;
            }
            catch (OperationCanceledException)
            {
                _logger.LogError("Connection timeout or cancelled");
                var error = new TimeoutException("Connection to Binance timed out after 30 seconds");
                ConnectionError?.Invoke(error);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to Binance");
            ConnectionError?.Invoke(ex);
            return false;
        }
    }

    public async Task DisconnectAsync()
    {
        if (_connector == null || !IsConnected)
        {
            _logger.LogWarning("Not connected to Binance");
            return;
        }

        try
        {
            _logger.LogInformation("Disconnecting from Binance");

            _connector.Disconnect();

            // Wait a bit for graceful disconnection
            await Task.Delay(1000);

            IsConnected = false;
            _logger.LogInformation("Disconnected from Binance");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during disconnect");
            throw;
        }
    }

    private BinanceConfig? LoadConfiguration(string connectorFilePath)
    {
        try
        {
            var json = File.ReadAllText(connectorFilePath);
            var document = JsonDocument.Parse(json);

            // Extract Binance adapter settings
            var adapter = document.RootElement.GetProperty("Adapter");
            var innerAdapters = adapter.GetProperty("InnerAdapters");

            JsonElement? binanceAdapter = null;
            foreach (var innerAdapter in innerAdapters.EnumerateArray())
            {
                var adapterType = innerAdapter.GetProperty("AdapterType").GetString();
                if (adapterType?.Contains("BinanceMessageAdapter") == true)
                {
                    binanceAdapter = innerAdapter;
                    break;
                }
            }

            if (binanceAdapter == null)
            {
                _logger.LogError("Binance adapter not found in configuration");
                return null;
            }

            var adapterSettings = binanceAdapter.Value.GetProperty("AdapterSettings");

            var key = adapterSettings.GetProperty("Key").GetString();
            var secret = adapterSettings.GetProperty("Secret").GetString();
            var isDemo = adapterSettings.TryGetProperty("IsDemo", out var isDemoElement) && isDemoElement.GetBoolean();
            var sections = adapterSettings.GetProperty("Sections").GetString();

            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(secret))
            {
                _logger.LogError("API Key or Secret is missing in configuration");
                return null;
            }

            return new BinanceConfig
            {
                Key = key,
                Secret = secret,
                IsDemo = isDemo,
                Sections = sections ?? "Spot"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse connector configuration");
            return null;
        }
    }

    private void SubscribeToEvents()
    {
        if (_connector == null)
            return;

        // Connection events
        _connector.Connected += OnConnected;
        _connector.Disconnected += OnDisconnected;
        _connector.ConnectionError += OnConnectionError;
        _connector.Error += OnError;

        // Market data events
        _connector.SecurityReceived += OnSecurityReceived;
        _connector.PortfolioReceived += OnPortfolioReceived;
        _connector.PositionReceived += OnPositionReceived;

        // Trading events
        _connector.OrderReceived += OnOrderReceived;
        _connector.OwnTradeReceived += OnOwnTradeReceived;
        _connector.OrderBookReceived += OnOrderBookReceived;

        _logger.LogDebug("Subscribed to connector events");
    }

    private void UnsubscribeFromEvents()
    {
        if (_connector == null)
            return;

        _connector.Connected -= OnConnected;
        _connector.Disconnected -= OnDisconnected;
        _connector.ConnectionError -= OnConnectionError;
        _connector.Error -= OnError;

        _connector.SecurityReceived -= OnSecurityReceived;
        _connector.PortfolioReceived -= OnPortfolioReceived;
        _connector.PositionReceived -= OnPositionReceived;

        _connector.OrderReceived -= OnOrderReceived;
        _connector.OwnTradeReceived -= OnOwnTradeReceived;
        _connector.OrderBookReceived -= OnOrderBookReceived;

        _logger.LogDebug("Unsubscribed from connector events");
    }

    // Event handlers
    private void OnConnected()
    {
        IsConnected = true;
        _logger.LogInformation("Successfully connected to Binance");
        _connectionTcs.TrySetResult(true);
        Connected?.Invoke();
    }

    private void OnDisconnected()
    {
        IsConnected = false;
        _logger.LogInformation("Disconnected from Binance");
        Disconnected?.Invoke();
    }

    private void OnConnectionError(Exception exception)
    {
        _logger.LogError(exception, "Connection error occurred");

        // Provide specific error messages based on exception type
        if (exception.Message.Contains("401") || exception.Message.Contains("Unauthorized"))
        {
            _logger.LogError("Invalid API key or secret. Please check your credentials.");
        }
        else if (exception.Message.Contains("403") || exception.Message.Contains("Forbidden"))
        {
            _logger.LogError("Access forbidden. Check IP whitelist or account permissions.");
        }
        else if (exception.Message.Contains("timeout") || exception.Message.Contains("network"))
        {
            _logger.LogError("Network connectivity issue. Check your internet connection.");
        }

        _connectionTcs.TrySetResult(false);
        ConnectionError?.Invoke(exception);
    }

    private void OnError(Exception exception)
    {
        _logger.LogError(exception, "General connector error");
    }

    private void OnSecurityReceived(Subscription subscription, Security security)
    {
        _logger.LogDebug("Security received: {SecurityId}", security.Id);
        SecurityReceived?.Invoke(security);
    }

    private void OnPortfolioReceived(Subscription subscription, Portfolio portfolio)
    {
        _logger.LogInformation("Portfolio received: {PortfolioName}, Balance: {Balance}",
            portfolio.Name, portfolio.CurrentValue);
        PortfolioReceived?.Invoke(portfolio);
    }

    private void OnPositionReceived(Subscription subscription, Position position)
    {
        _logger.LogInformation("Position received: {SecurityId}, Volume: {Volume}",
            position.Security?.Id, position.CurrentValue);
        PositionChanged?.Invoke(position);
    }

    private void OnOrderReceived(Subscription subscription, Order order)
    {
        _logger.LogInformation("Order received: {OrderId}, Side: {Side}, Price: {Price}, Volume: {Volume}, State: {State}",
            order.Id, order.Side, order.Price, order.Volume, order.State);
        OrderReceived?.Invoke(order);
    }

    private void OnOwnTradeReceived(Subscription subscription, MyTrade trade)
    {
        _logger.LogInformation("Trade received: {TradeId}, Price: {Price}, Volume: {Volume}",
            trade.Trade.Id, trade.Trade.Price, trade.Trade.Volume);
        TradeReceived?.Invoke(trade);
    }

    private void OnOrderBookReceived(Subscription subscription, IOrderBookMessage orderBook)
    {
        _logger.LogTrace("Order book received for {SecurityId}", orderBook.SecurityId);
        OrderBookReceived?.Invoke(orderBook);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        try
        {
            UnsubscribeFromEvents();

            if (_connector != null)
            {
                if (IsConnected)
                {
                    _connector.Disconnect();
                }
                _connector.Dispose();
                _connector = null;
            }

            _disposed = true;
            _logger.LogInformation("BinanceConnectorWrapper disposed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during dispose");
        }
    }

    private class BinanceConfig
    {
        public required string Key { get; init; }
        public required string Secret { get; init; }
        public bool IsDemo { get; init; }
        public required string Sections { get; init; }
    }
}
