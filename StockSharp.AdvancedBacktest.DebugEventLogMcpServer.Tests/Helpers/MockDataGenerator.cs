using System.Text.Json;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Models;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Storage;

namespace StockSharp.AdvancedBacktest.DebugEventLogMcpServer.Tests.Helpers;

public sealed class MockDataGenerator(SqliteEventRepository repository)
{
    private readonly SqliteEventRepository _repository = repository;
    private readonly Random _random = new(42); // Fixed seed for reproducibility

    public async Task PopulateAsync(string runId, MockDataProfile profile)
    {
        var timestamp = profile.BaseTime;
        var orderIdCounter = 1;

        // Create trade executions
        for (int i = 0; i < profile.TradeCount; i++)
        {
            var security = profile.Securities[i % profile.Securities.Length];
            var orderId = $"order-{orderIdCounter++}";
            var price = GeneratePrice(security);
            var quantity = GenerateQuantity();

            await WriteTradeExecutionAsync(runId, orderId, security, price, quantity, timestamp);
            timestamp = timestamp.AddMinutes(5 + _random.Next(10));
        }

        // Create position updates
        timestamp = profile.BaseTime.AddMinutes(2);
        for (int i = 0; i < profile.PositionUpdateCount; i++)
        {
            var security = profile.Securities[i % profile.Securities.Length];
            var quantity = GenerateQuantity() * (i + 1);
            var avgPrice = GeneratePrice(security);

            await WritePositionUpdateAsync(runId, security, quantity, avgPrice, timestamp);
            timestamp = timestamp.AddMinutes(10 + _random.Next(5));
        }

        // Create indicator calculations
        timestamp = profile.BaseTime;
        for (int i = 0; i < profile.IndicatorCalculationCount; i++)
        {
            var security = profile.Securities[i % profile.Securities.Length];
            var indicator = profile.IndicatorNames[i % profile.IndicatorNames.Length];
            var value = GenerateIndicatorValue(indicator);

            await WriteIndicatorCalculationAsync(runId, indicator, security, value, timestamp);
            timestamp = timestamp.AddMinutes(1);
        }
    }

    private async Task WriteTradeExecutionAsync(
        string runId,
        string orderId,
        string security,
        decimal price,
        decimal quantity,
        DateTime timestamp)
    {
        var properties = new
        {
            OrderId = orderId,
            SecuritySymbol = security,
            Price = price,
            Quantity = quantity,
            Side = _random.Next(2) == 0 ? "Buy" : "Sell",
            Commission = Math.Round(price * quantity * 0.001m, 2)
        };

        await _repository.WriteEventAsync(new EventEntity
        {
            EventId = Guid.NewGuid().ToString(),
            RunId = runId,
            Timestamp = timestamp,
            EventType = EventType.TradeExecution,
            Severity = EventSeverity.Info,
            Category = EventCategory.Execution,
            Properties = JsonSerializer.Serialize(properties)
        });
    }

    private async Task WritePositionUpdateAsync(
        string runId,
        string security,
        decimal quantity,
        decimal avgPrice,
        DateTime timestamp)
    {
        var unrealizedPnl = (GeneratePrice(security) - avgPrice) * quantity;

        var properties = new
        {
            SecuritySymbol = security,
            Quantity = quantity,
            AveragePrice = avgPrice,
            UnrealizedPnL = Math.Round(unrealizedPnl, 2),
            RealizedPnL = 0m
        };

        await _repository.WriteEventAsync(new EventEntity
        {
            EventId = Guid.NewGuid().ToString(),
            RunId = runId,
            Timestamp = timestamp,
            EventType = EventType.PositionUpdate,
            Severity = EventSeverity.Info,
            Category = EventCategory.Performance,
            Properties = JsonSerializer.Serialize(properties)
        });
    }

    private async Task WriteIndicatorCalculationAsync(
        string runId,
        string indicatorName,
        string security,
        decimal value,
        DateTime timestamp)
    {
        var properties = new
        {
            IndicatorName = indicatorName,
            SecuritySymbol = security,
            Value = value,
            Parameters = GetIndicatorParameters(indicatorName)
        };

        await _repository.WriteEventAsync(new EventEntity
        {
            EventId = Guid.NewGuid().ToString(),
            RunId = runId,
            Timestamp = timestamp,
            EventType = EventType.IndicatorCalculation,
            Severity = EventSeverity.Debug,
            Category = EventCategory.Indicators,
            Properties = JsonSerializer.Serialize(properties)
        });
    }

    private decimal GeneratePrice(string security) => security switch
    {
        "AAPL" => 175m + (decimal)(_random.NextDouble() * 10 - 5),
        "GOOGL" => 140m + (decimal)(_random.NextDouble() * 8 - 4),
        "MSFT" => 400m + (decimal)(_random.NextDouble() * 15 - 7.5),
        _ => 100m + (decimal)(_random.NextDouble() * 10 - 5)
    };

    private decimal GenerateQuantity() => 10 + _random.Next(90);

    private decimal GenerateIndicatorValue(string indicator) => indicator switch
    {
        "RSI_14" => 30m + (decimal)(_random.NextDouble() * 40),
        "SMA_10" or "SMA_20" => 150m + (decimal)(_random.NextDouble() * 30),
        "EMA_10" or "EMA_20" => 150m + (decimal)(_random.NextDouble() * 30),
        _ => (decimal)(_random.NextDouble() * 100)
    };

    private static object GetIndicatorParameters(string indicator) => indicator switch
    {
        "RSI_14" => new { Period = 14 },
        "SMA_10" => new { Period = 10 },
        "SMA_20" => new { Period = 20 },
        "EMA_10" => new { Period = 10 },
        "EMA_20" => new { Period = 20 },
        _ => new { Period = 10 }
    };
}
