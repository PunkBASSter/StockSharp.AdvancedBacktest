using StockSharp.Algo.Storages;
using StockSharp.AdvancedBacktest.LauncherTemplate.Utilities;
using StockSharp.Messages;

namespace StockSharp.AdvancedBacktest.LauncherTemplate.BacktestMode;

public class HistoryDataValidator
{
    private readonly string _historyPath;
    private LocalMarketDataDrive? _drive;
    private StorageRegistry? _registry;

    public HistoryDataValidator(string historyPath)
    {
        if (string.IsNullOrWhiteSpace(historyPath))
            throw new ArgumentException("History path cannot be empty", nameof(historyPath));

        _historyPath = historyPath;
    }

    public ValidationReport Validate(List<string> securitiesToCheck, List<TimeSpan> timeFrames)
    {
        var report = new ValidationReport
        {
            HistoryPath = _historyPath,
            ValidationTime = DateTimeOffset.UtcNow
        };

        try
        {
            if (!Directory.Exists(_historyPath))
            {
                report.AddError($"History path does not exist: {_historyPath}");
                report.IsSuccess = false;
                return report;
            }

            _drive = new LocalMarketDataDrive(_historyPath);
            _registry = new StorageRegistry { DefaultDrive = _drive };

            report.AddInfo($"Successfully initialized storage at: {_historyPath}");

            var availableSecurities = GetAvailableSecurities();
            report.AvailableSecurities = availableSecurities;
            report.AddInfo($"Found {availableSecurities.Count} securities in storage");

            if (availableSecurities.Count == 0)
            {
                report.AddWarning("No securities found in history storage");
            }

            foreach (var securityId in securitiesToCheck)
            {
                ValidateSecurity(securityId, timeFrames, report);
            }

            report.IsSuccess = report.Errors.Count == 0;
        }
        catch (Exception ex)
        {
            report.AddError($"Validation failed: {ex.Message}");
            report.IsSuccess = false;
        }
        finally
        {
            _registry?.Dispose();
            _drive?.Dispose();
        }

        return report;
    }

    private List<string> GetAvailableSecurities()
    {
        var securities = new List<string>();

        try
        {
            var availableSecurities = _drive!.AvailableSecurities;
            securities.AddRange(availableSecurities.Select(s => s.ToStringId()));
        }
        catch (Exception ex)
        {
            ConsoleLogger.LogWarning($"Could not enumerate securities: {ex.Message}");
        }

        return securities;
    }

    private void ValidateSecurity(string securityIdStr, List<TimeSpan> timeFrames, ValidationReport report)
    {
        try
        {
            var securityId = securityIdStr.ToSecurityId();
            var securityReport = new SecurityValidationResult
            {
                SecurityId = securityIdStr
            };

            report.AddInfo($"\nValidating security: {securityIdStr}");

            foreach (var timeFrame in timeFrames)
            {
                var timeFrameReport = ValidateTimeFrame(securityId, timeFrame);
                securityReport.TimeFrameResults.Add(timeFrameReport);

                if (timeFrameReport.IsAvailable)
                {
                    report.AddInfo($"  ✓ TimeFrame {timeFrame}: {timeFrameReport.DateCount} dates available ({timeFrameReport.FirstDate:yyyy-MM-dd} to {timeFrameReport.LastDate:yyyy-MM-dd})");
                }
                else
                {
                    report.AddWarning($"  ✗ TimeFrame {timeFrame}: No data available");
                }
            }

            report.SecurityResults.Add(securityReport);
        }
        catch (Exception ex)
        {
            report.AddError($"Failed to validate security {securityIdStr}: {ex.Message}");
        }
    }

    private TimeFrameValidationResult ValidateTimeFrame(SecurityId securityId, TimeSpan timeFrame)
    {
        var result = new TimeFrameValidationResult
        {
            TimeFrame = timeFrame
        };

        try
        {
            var candleStorage = _registry!.GetCandleMessageStorage(
                typeof(TimeFrameCandleMessage),
                securityId,
                timeFrame,
                format: StorageFormats.Binary);

            var dates = candleStorage.Dates.ToArray();
            result.DateCount = dates.Length;
            result.IsAvailable = dates.Length > 0;

            if (dates.Length > 0)
            {
                result.FirstDate = dates.First();
                result.LastDate = dates.Last();
            }
        }
        catch (Exception ex)
        {
            result.IsAvailable = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    public class ValidationReport
    {
        public string HistoryPath { get; set; } = string.Empty;
        public DateTimeOffset ValidationTime { get; set; }
        public bool IsSuccess { get; set; }
        public List<string> Infos { get; } = new();
        public List<string> Warnings { get; } = new();
        public List<string> Errors { get; } = new();
        public List<string> AvailableSecurities { get; set; } = new();
        public List<SecurityValidationResult> SecurityResults { get; } = new();

        public void AddInfo(string message) => Infos.Add(message);
        public void AddWarning(string message) => Warnings.Add(message);
        public void AddError(string message) => Errors.Add(message);

        public void PrintToConsole()
        {
            ConsoleLogger.LogSection("History Data Validation Report");
            ConsoleLogger.LogInfo($"Path: {HistoryPath}");
            ConsoleLogger.LogInfo($"Time: {ValidationTime:yyyy-MM-dd HH:mm:ss}");
            ConsoleLogger.LogInfo($"Status: {(IsSuccess ? "✓ SUCCESS" : "✗ FAILED")}");

            if (Infos.Count > 0)
            {
                ConsoleLogger.LogInfo("\n=== Information ===");
                foreach (var info in Infos)
                {
                    ConsoleLogger.LogInfo(info);
                }
            }

            if (Warnings.Count > 0)
            {
                ConsoleLogger.LogInfo("\n=== Warnings ===");
                foreach (var warning in Warnings)
                {
                    ConsoleLogger.LogWarning(warning);
                }
            }

            if (Errors.Count > 0)
            {
                ConsoleLogger.LogInfo("\n=== Errors ===");
                foreach (var error in Errors)
                {
                    ConsoleLogger.LogError(error);
                }
            }

            if (AvailableSecurities.Count > 0)
            {
                ConsoleLogger.LogInfo("\n=== Available Securities ===");
                foreach (var security in AvailableSecurities.Take(20))
                {
                    ConsoleLogger.LogInfo($"  - {security}");
                }
                if (AvailableSecurities.Count > 20)
                {
                    ConsoleLogger.LogInfo($"  ... and {AvailableSecurities.Count - 20} more");
                }
            }
        }
    }

    public class SecurityValidationResult
    {
        public string SecurityId { get; set; } = string.Empty;
        public List<TimeFrameValidationResult> TimeFrameResults { get; } = new();
    }

    public class TimeFrameValidationResult
    {
        public TimeSpan TimeFrame { get; set; }
        public bool IsAvailable { get; set; }
        public int DateCount { get; set; }
        public DateTime FirstDate { get; set; }
        public DateTime LastDate { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
