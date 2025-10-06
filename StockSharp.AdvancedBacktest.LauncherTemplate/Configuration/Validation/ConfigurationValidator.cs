using StockSharp.AdvancedBacktest.LauncherTemplate.Configuration.Models;

namespace StockSharp.AdvancedBacktest.LauncherTemplate.Configuration.Validation;

public class ConfigurationValidator
{
    public ValidationResult ValidateBacktestConfiguration(BacktestConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config, nameof(config));

        var result = new ValidationResult();

        ValidateRequiredFields(config, result);

        ValidateNumericRanges(config, result);

        ValidateDateRanges(config, result);

        ValidateDataFileReferences(config, result);

        ValidateBacktestParameters(config, result);

        AddBacktestWarnings(config, result);

        return result;
    }

    public ValidationResult ValidateStrategyParametersConfig(StrategyParametersConfig config)
    {
        ArgumentNullException.ThrowIfNull(config, nameof(config));

        var result = new ValidationResult();

        ValidateRequiredStrategyParameters(config, result);

        ValidateStrategyParameterRanges(config, result);

        ValidateStrategyLogicalConsistency(config, result);

        AddStrategyWarnings(config, result);

        return result;
    }

    public ValidationResult ValidateLiveTradingConfiguration(LiveTradingConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config, nameof(config));

        var result = new ValidationResult();

        ValidateLiveConfigPaths(config, result);

        ValidateRiskLimits(config, result);

        ValidateTradingSessions(config, result);

        ValidateMonitoringSettings(config, result);

        AddLiveTradingWarnings(config, result);

        return result;
    }

    private void ValidateRequiredFields(BacktestConfiguration config, ValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(config.StrategyName))
        {
            result.AddError("Strategy name is required.", nameof(config.StrategyName));
        }
        else if (config.StrategyName.Length > 100)
        {
            result.AddError("Strategy name cannot exceed 100 characters.", nameof(config.StrategyName));
        }

        if (string.IsNullOrWhiteSpace(config.StrategyVersion))
        {
            result.AddError("Strategy version is required.", nameof(config.StrategyVersion));
        }

        if (config.Securities == null || config.Securities.Count == 0)
        {
            result.AddError("At least one security must be specified.", nameof(config.Securities));
        }
        else
        {

            for (int i = 0; i < config.Securities.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(config.Securities[i]))
                {
                    result.AddError($"Security at index {i} is null or empty.", nameof(config.Securities));
                }
            }

            var duplicates = config.Securities
                .GroupBy(s => s)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicates.Count > 0)
            {
                result.AddWarning($"Duplicate securities found: {string.Join(", ", duplicates)}", nameof(config.Securities));
            }
        }

        if (string.IsNullOrWhiteSpace(config.HistoryPath))
        {
            result.AddError("History path is required.", nameof(config.HistoryPath));
        }
    }

    private void ValidateNumericRanges(BacktestConfiguration config, ValidationResult result)
    {
        if (config.InitialCapital <= 0)
        {
            result.AddError("Initial capital must be greater than 0.", nameof(config.InitialCapital));
        }
        else if (config.InitialCapital < 100)
        {
            result.AddWarning("Initial capital is very low (< 100). This may not be realistic for testing.", nameof(config.InitialCapital));
        }

        if (config.TradeVolume <= 0)
        {
            result.AddError("Trade volume must be greater than 0.", nameof(config.TradeVolume));
        }

        if (config.CommissionPercentage < 0)
        {
            result.AddError("Commission percentage cannot be negative.", nameof(config.CommissionPercentage));
        }
        else if (config.CommissionPercentage > 100)
        {
            result.AddError("Commission percentage cannot exceed 100%.", nameof(config.CommissionPercentage));
        }
        else if (config.CommissionPercentage > 10)
        {
            result.AddWarning("Commission percentage is unusually high (> 10%). Please verify this is correct.", nameof(config.CommissionPercentage));
        }

        if (config.ParallelWorkers < 1)
        {
            result.AddError("Parallel workers must be at least 1.", nameof(config.ParallelWorkers));
        }
        else if (config.ParallelWorkers > Environment.ProcessorCount * 2)
        {
            result.AddWarning($"Parallel workers ({config.ParallelWorkers}) exceeds 2x processor count ({Environment.ProcessorCount}). This may not improve performance.", nameof(config.ParallelWorkers));
        }
    }

    private void ValidateDateRanges(BacktestConfiguration config, ValidationResult result)
    {

        if (config.TrainingEndDate <= config.TrainingStartDate)
        {
            result.AddError("Training end date must be after training start date.", nameof(config.TrainingEndDate));
        }
        else
        {
            var trainingDuration = config.TrainingEndDate - config.TrainingStartDate;
            if (trainingDuration.TotalDays < 7)
            {
                result.AddWarning("Training period is less than 7 days. This may not provide sufficient data for optimization.", nameof(config.TrainingStartDate));
            }
        }

        if (config.ValidationEndDate <= config.ValidationStartDate)
        {
            result.AddError("Validation end date must be after validation start date.", nameof(config.ValidationEndDate));
        }
        else
        {
            var validationDuration = config.ValidationEndDate - config.ValidationStartDate;
            if (validationDuration.TotalDays < 7)
            {
                result.AddWarning("Validation period is less than 7 days. This may not provide sufficient data for validation.", nameof(config.ValidationStartDate));
            }
        }

        if (config.ValidationStartDate < config.TrainingEndDate)
        {
            result.AddError("Validation period overlaps with training period. Validation start date must be on or after training end date.", nameof(config.ValidationStartDate));
        }

        var gapDays = (config.ValidationStartDate - config.TrainingEndDate).TotalDays;
        if (gapDays > 30)
        {
            result.AddWarning($"There is a {gapDays:F0}-day gap between training and validation periods. Consider if this is intentional.", nameof(config.ValidationStartDate));
        }

        var now = DateTimeOffset.UtcNow;
        if (config.TrainingEndDate > now || config.ValidationEndDate > now)
        {
            result.AddError("Training and validation end dates cannot be in the future.", nameof(config.ValidationEndDate));
        }

        if (config.ValidationEndDate < DateTimeOffset.UtcNow.AddYears(-10))
        {
            result.AddWarning("Validation end date is more than 10 years old. Market conditions may have changed significantly.", nameof(config.ValidationEndDate));
        }
    }

    private void ValidateDataFileReferences(BacktestConfiguration config, ValidationResult result)
    {
        if (!string.IsNullOrWhiteSpace(config.HistoryPath))
        {
            if (!Directory.Exists(config.HistoryPath) && !File.Exists(config.HistoryPath))
            {
                result.AddError($"History path does not exist: '{config.HistoryPath}'", nameof(config.HistoryPath));
            }
        }

        if (!string.IsNullOrWhiteSpace(config.ExportPath))
        {
            try
            {
                var directory = Path.GetDirectoryName(config.ExportPath);
                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                {
                    result.AddWarning($"Export path directory does not exist and will be created: '{directory}'", nameof(config.ExportPath));
                }
            }
            catch (Exception ex)
            {
                result.AddError($"Invalid export path: {ex.Message}", nameof(config.ExportPath));
            }
        }
    }

    private void ValidateBacktestParameters(BacktestConfiguration config, ValidationResult result)
    {
        if (config.OptimizableParameters == null || config.OptimizableParameters.Count == 0)
        {
            result.AddError("At least one optimizable parameter must be specified.", nameof(config.OptimizableParameters));
            return;
        }

        foreach (var param in config.OptimizableParameters)
        {
            if (string.IsNullOrWhiteSpace(param.Key))
            {
                result.AddError("Parameter name cannot be null or empty.", nameof(config.OptimizableParameters));
                continue;
            }

            var paramDef = param.Value;
            if (string.IsNullOrWhiteSpace(paramDef.Type))
            {
                result.AddError($"Parameter '{param.Key}': Type is required.", nameof(config.OptimizableParameters));
            }

            ValidateParameterDefinition(param.Key, paramDef, result);
        }

        if (config.OptimizableParameters.Count > 10)
        {
            result.AddWarning($"You have {config.OptimizableParameters.Count} optimizable parameters. This may result in very long optimization times.", nameof(config.OptimizableParameters));
        }
    }

    private void ValidateParameterDefinition(string parameterName, ParameterDefinition param, ValidationResult result)
    {
        var type = param.Type?.ToLowerInvariant();

        switch (type)
        {
            case "int":
            case "integer":
                ValidateIntegerParameter(parameterName, param, result);
                break;
            case "decimal":
            case "double":
            case "float":
                ValidateNumericParameter(parameterName, param, result);
                break;
            case "bool":
            case "boolean":

                break;
            default:
                result.AddWarning($"Parameter '{parameterName}': Unknown parameter type '{param.Type}'.", nameof(param.Type));
                break;
        }
    }

    private void ValidateIntegerParameter(string parameterName, ParameterDefinition param, ValidationResult result)
    {
        try
        {
            if (!param.MinValue.TryGetInt32(out var min))
            {
                result.AddError($"Parameter '{parameterName}': MinValue must be a valid integer.", nameof(param.MinValue));
                return;
            }

            if (!param.MaxValue.TryGetInt32(out var max))
            {
                result.AddError($"Parameter '{parameterName}': MaxValue must be a valid integer.", nameof(param.MaxValue));
                return;
            }

            if (!param.StepValue.TryGetInt32(out var step))
            {
                result.AddError($"Parameter '{parameterName}': StepValue must be a valid integer.", nameof(param.StepValue));
                return;
            }

            if (min >= max)
            {
                result.AddError($"Parameter '{parameterName}': MinValue ({min}) must be less than MaxValue ({max}).", nameof(param.MinValue));
            }

            if (step <= 0)
            {
                result.AddError($"Parameter '{parameterName}': StepValue must be greater than 0.", nameof(param.StepValue));
            }

            var steps = (max - min) / step;
            if (steps > 1000)
            {
                result.AddWarning($"Parameter '{parameterName}': Range will generate {steps} values. Consider increasing step size.", nameof(param.StepValue));
            }
        }
        catch (Exception ex)
        {
            result.AddError($"Parameter '{parameterName}': Error validating integer parameter - {ex.Message}", nameof(param));
        }
    }

    private void ValidateNumericParameter(string parameterName, ParameterDefinition param, ValidationResult result)
    {
        try
        {
            if (!param.MinValue.TryGetDecimal(out var min))
            {
                result.AddError($"Parameter '{parameterName}': MinValue must be a valid decimal number.", nameof(param.MinValue));
                return;
            }

            if (!param.MaxValue.TryGetDecimal(out var max))
            {
                result.AddError($"Parameter '{parameterName}': MaxValue must be a valid decimal number.", nameof(param.MaxValue));
                return;
            }

            if (!param.StepValue.TryGetDecimal(out var step))
            {
                result.AddError($"Parameter '{parameterName}': StepValue must be a valid decimal number.", nameof(param.StepValue));
                return;
            }

            if (min >= max)
            {
                result.AddError($"Parameter '{parameterName}': MinValue ({min}) must be less than MaxValue ({max}).", nameof(param.MinValue));
            }

            if (step <= 0)
            {
                result.AddError($"Parameter '{parameterName}': StepValue must be greater than 0.", nameof(param.StepValue));
            }

            var steps = (max - min) / step;
            if (steps > 1000)
            {
                result.AddWarning($"Parameter '{parameterName}': Range will generate approximately {(int)steps} values. Consider increasing step size.", nameof(param.StepValue));
            }
        }
        catch (Exception ex)
        {
            result.AddError($"Parameter '{parameterName}': Error validating numeric parameter - {ex.Message}", nameof(param));
        }
    }

    private void AddBacktestWarnings(BacktestConfiguration config, ValidationResult result)
    {

        if (config.WalkForwardConfig != null)
        {
            if (config.WalkForwardConfig.WindowSize.TotalDays < 30)
            {
                result.AddWarning("Walk-forward window size is less than 30 days. This may not provide stable results.", nameof(config.WalkForwardConfig));
            }
        }

        if (string.IsNullOrWhiteSpace(config.ExportPath) && config.ExportDetailedMetrics)
        {
            result.AddWarning("ExportDetailedMetrics is enabled but ExportPath is not set. Metrics will not be exported.", nameof(config.ExportPath));
        }
    }

    private void ValidateRequiredStrategyParameters(StrategyParametersConfig config, ValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(config.StrategyName))
        {
            result.AddError("Strategy name is required.", nameof(config.StrategyName));
        }

        if (string.IsNullOrWhiteSpace(config.StrategyVersion))
        {
            result.AddError("Strategy version is required.", nameof(config.StrategyVersion));
        }

        if (string.IsNullOrWhiteSpace(config.StrategyHash))
        {
            result.AddError("Strategy hash is required.", nameof(config.StrategyHash));
        }

        if (config.Parameters == null || config.Parameters.Count == 0)
        {
            result.AddError("At least one parameter must be specified.", nameof(config.Parameters));
        }
    }

    private void ValidateStrategyParameterRanges(StrategyParametersConfig config, ValidationResult result)
    {
        if (config.InitialCapital <= 0)
        {
            result.AddError("Initial capital must be greater than 0.", nameof(config.InitialCapital));
        }
        else if (config.InitialCapital < 100)
        {
            result.AddWarning("Initial capital is very low (< 100). Ensure this is appropriate for your trading strategy.", nameof(config.InitialCapital));
        }

        if (config.TradeVolume <= 0)
        {
            result.AddError("Trade volume must be greater than 0.", nameof(config.TradeVolume));
        }

        if (config.Parameters != null)
        {
            CheckTradingParameterRanges(config, result);
        }
    }

    private void CheckTradingParameterRanges(StrategyParametersConfig config, ValidationResult result)
    {

        if (config.Parameters.TryGetValue("StopLossPercentage", out var stopLossElement))
        {
            if (stopLossElement.TryGetDecimal(out var stopLoss))
            {
                if (stopLoss < 0)
                {
                    result.AddError("StopLossPercentage cannot be negative.", nameof(config.Parameters));
                }
                else if (stopLoss > 100)
                {
                    result.AddError("StopLossPercentage cannot exceed 100%.", nameof(config.Parameters));
                }
                else if (stopLoss > 50)
                {
                    result.AddWarning("StopLossPercentage is very high (> 50%). This may result in large losses.", nameof(config.Parameters));
                }
            }
        }

        if (config.Parameters.TryGetValue("TakeProfitPercentage", out var takeProfitElement))
        {
            if (takeProfitElement.TryGetDecimal(out var takeProfit))
            {
                if (takeProfit <= 0)
                {
                    result.AddError("TakeProfitPercentage must be greater than 0.", nameof(config.Parameters));
                }
            }
        }

        if (config.Parameters.TryGetValue("PositionSize", out var positionElement))
        {
            if (positionElement.TryGetDecimal(out var position))
            {
                if (position <= 0)
                {
                    result.AddError("PositionSize must be greater than 0.", nameof(config.Parameters));
                }
                else if (position > config.InitialCapital)
                {
                    result.AddWarning("PositionSize exceeds initial capital. This requires leverage or will cause issues.", nameof(config.Parameters));
                }
            }
        }
    }

    private void ValidateStrategyLogicalConsistency(StrategyParametersConfig config, ValidationResult result)
    {
        if (config.Parameters == null) return;

        bool hasStopLoss = config.Parameters.TryGetValue("StopLossPercentage", out var stopLossElement);
        bool hasTakeProfit = config.Parameters.TryGetValue("TakeProfitPercentage", out var takeProfitElement);

        if (hasStopLoss && hasTakeProfit)
        {
            if (stopLossElement.TryGetDecimal(out var stopLoss) && takeProfitElement.TryGetDecimal(out var takeProfit))
            {
                if (stopLoss >= takeProfit)
                {
                    result.AddWarning("StopLossPercentage is greater than or equal to TakeProfitPercentage. This may indicate a configuration error.", nameof(config.Parameters));
                }

                if (takeProfit > 0 && stopLoss > 0)
                {
                    var riskRewardRatio = takeProfit / stopLoss;
                    if (riskRewardRatio < 1)
                    {
                        result.AddWarning($"Risk-reward ratio is less than 1:1 (TakeProfit/StopLoss = {riskRewardRatio:F2}). Consider if this aligns with your trading strategy.", nameof(config.Parameters));
                    }
                }
            }
        }

        var periodParameters = config.Parameters.Where(p => p.Key.Contains("Period", StringComparison.OrdinalIgnoreCase));
        foreach (var param in periodParameters)
        {
            if (param.Value.TryGetInt32(out var period))
            {
                if (period <= 0)
                {
                    result.AddError($"Parameter '{param.Key}' must be greater than 0.", nameof(config.Parameters));
                }
                else if (period > 1000)
                {
                    result.AddWarning($"Parameter '{param.Key}' is very large ({period}). Ensure this is correct.", nameof(config.Parameters));
                }
            }
        }
    }

    private void AddStrategyWarnings(StrategyParametersConfig config, ValidationResult result)
    {

        if (config.TrainingMetrics == null && config.ValidationMetrics == null)
        {
            result.AddWarning("No performance metrics are available. This strategy configuration may not have been properly backtested.", nameof(config.TrainingMetrics));
        }

        if (config.Securities.Count == 0)
        {
            result.AddWarning("No securities specified. Ensure the strategy will be applied to the correct instruments.", nameof(config.Securities));
        }

        if (config.OptimizationDate < DateTimeOffset.UtcNow.AddYears(-1))
        {
            result.AddWarning($"This strategy configuration is over 1 year old (optimized on {config.OptimizationDate:yyyy-MM-dd}). Consider re-optimizing with recent data.", nameof(config.OptimizationDate));
        }
    }

    private void ValidateLiveConfigPaths(LiveTradingConfiguration config, ValidationResult result)
    {

        if (string.IsNullOrWhiteSpace(config.StrategyConfigPath))
        {
            result.AddError("Strategy configuration path is required.", nameof(config.StrategyConfigPath));
        }
        else if (!File.Exists(config.StrategyConfigPath))
        {
            result.AddError($"Strategy configuration file does not exist: '{config.StrategyConfigPath}'", nameof(config.StrategyConfigPath));
        }

        if (string.IsNullOrWhiteSpace(config.BrokerConfigPath))
        {
            result.AddError("Broker configuration path is required.", nameof(config.BrokerConfigPath));
        }
        else if (!File.Exists(config.BrokerConfigPath))
        {
            result.AddError($"Broker configuration file does not exist: '{config.BrokerConfigPath}'", nameof(config.BrokerConfigPath));
        }

        if (!string.IsNullOrWhiteSpace(config.LogFilePath))
        {
            try
            {
                var directory = Path.GetDirectoryName(config.LogFilePath);
                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                {
                    result.AddWarning($"Log file directory does not exist and will be created: '{directory}'", nameof(config.LogFilePath));
                }
            }
            catch (Exception ex)
            {
                result.AddError($"Invalid log file path: {ex.Message}", nameof(config.LogFilePath));
            }
        }
    }

    private void ValidateRiskLimits(LiveTradingConfiguration config, ValidationResult result)
    {
        if (config.RiskLimits == null)
        {
            result.AddError("Risk limits configuration is required for live trading.", nameof(config.RiskLimits));
            return;
        }

        var limits = config.RiskLimits;

        if (limits.MaxPositionSize <= 0)
        {
            result.AddError("Max position size must be greater than 0.", nameof(limits.MaxPositionSize));
        }
        else if (limits.MaxPositionSize > 1000000)
        {
            result.AddWarning($"Max position size is very large ({limits.MaxPositionSize:N0}). Ensure this is appropriate for your account.", nameof(limits.MaxPositionSize));
        }

        if (limits.MaxDailyLoss <= 0)
        {
            result.AddError("Max daily loss must be greater than 0.", nameof(limits.MaxDailyLoss));
        }

        if (limits.MaxDailyLossIsPercentage && limits.MaxDailyLoss > 100)
        {
            result.AddError("Max daily loss percentage cannot exceed 100%.", nameof(limits.MaxDailyLoss));
        }
        else if (limits.MaxDailyLossIsPercentage && limits.MaxDailyLoss > 20)
        {
            result.AddWarning($"Max daily loss percentage is very high ({limits.MaxDailyLoss}%). This could result in significant losses.", nameof(limits.MaxDailyLoss));
        }

        if (limits.MaxDrawdownPercentage <= 0)
        {
            result.AddError("Max drawdown percentage must be greater than 0.", nameof(limits.MaxDrawdownPercentage));
        }
        else if (limits.MaxDrawdownPercentage > 100)
        {
            result.AddError("Max drawdown percentage cannot exceed 100%.", nameof(limits.MaxDrawdownPercentage));
        }
        else if (limits.MaxDrawdownPercentage > 50)
        {
            result.AddWarning($"Max drawdown percentage is very high ({limits.MaxDrawdownPercentage}%). Consider reducing this for better risk management.", nameof(limits.MaxDrawdownPercentage));
        }

        if (limits.MaxTradesPerDay <= 0)
        {
            result.AddError("Max trades per day must be at least 1.", nameof(limits.MaxTradesPerDay));
        }
        else if (limits.MaxTradesPerDay > 1000)
        {
            result.AddWarning($"Max trades per day is very high ({limits.MaxTradesPerDay}). This may indicate a high-frequency strategy.", nameof(limits.MaxTradesPerDay));
        }

        if (limits.CircuitBreakerEnabled)
        {
            if (limits.CircuitBreakerThresholdPercentage <= 0)
            {
                result.AddError("Circuit breaker threshold must be greater than 0.", nameof(limits.CircuitBreakerThresholdPercentage));
            }
            else if (limits.CircuitBreakerThresholdPercentage > 100)
            {
                result.AddError("Circuit breaker threshold cannot exceed 100%.", nameof(limits.CircuitBreakerThresholdPercentage));
            }

            if (limits.CircuitBreakerCooldownMinutes < 0)
            {
                result.AddError("Circuit breaker cooldown cannot be negative.", nameof(limits.CircuitBreakerCooldownMinutes));
            }
            else if (limits.CircuitBreakerCooldownMinutes > 1440)
            {
                result.AddWarning($"Circuit breaker cooldown is more than 24 hours ({limits.CircuitBreakerCooldownMinutes} minutes). This may be excessive.", nameof(limits.CircuitBreakerCooldownMinutes));
            }
        }

        if (limits.MaxLeverageRatio < 1)
        {
            result.AddError("Max leverage ratio must be at least 1.", nameof(limits.MaxLeverageRatio));
        }
        else if (limits.MaxLeverageRatio > 10)
        {
            result.AddWarning($"Max leverage ratio is very high ({limits.MaxLeverageRatio}). High leverage increases risk significantly.", nameof(limits.MaxLeverageRatio));
        }

        if (limits.MaxPositionConcentrationPercentage < 0 || limits.MaxPositionConcentrationPercentage > 100)
        {
            result.AddError("Max position concentration must be between 0 and 100%.", nameof(limits.MaxPositionConcentrationPercentage));
        }
        else if (limits.MaxPositionConcentrationPercentage > 50)
        {
            result.AddWarning($"Max position concentration is high ({limits.MaxPositionConcentrationPercentage}%). Consider diversifying positions.", nameof(limits.MaxPositionConcentrationPercentage));
        }
    }

    private void ValidateTradingSessions(LiveTradingConfiguration config, ValidationResult result)
    {
        if (config.TradingSessions != null && config.TradingSessions.Count > 0)
        {
            for (int i = 0; i < config.TradingSessions.Count; i++)
            {
                var session = config.TradingSessions[i];

                if (string.IsNullOrWhiteSpace(session.Name))
                {
                    result.AddError($"Trading session at index {i}: Name is required.", nameof(config.TradingSessions));
                }

                if (session.EndTime <= session.StartTime)
                {
                    result.AddError($"Trading session '{session.Name}': End time must be after start time.", nameof(config.TradingSessions));
                }

                if (session.DaysOfWeek == null || session.DaysOfWeek.Count == 0)
                {
                    result.AddWarning($"Trading session '{session.Name}': No days of week specified. Session will never be active.", nameof(config.TradingSessions));
                }

                for (int j = i + 1; j < config.TradingSessions.Count; j++)
                {
                    var otherSession = config.TradingSessions[j];
                    if (HasOverlappingDays(session, otherSession) && HasOverlappingTime(session, otherSession))
                    {
                        result.AddWarning($"Trading sessions '{session.Name}' and '{otherSession.Name}' have overlapping time periods.", nameof(config.TradingSessions));
                    }
                }
            }
        }
    }

    private void ValidateMonitoringSettings(LiveTradingConfiguration config, ValidationResult result)
    {
        if (config.EnableAlerts)
        {
            if (string.IsNullOrWhiteSpace(config.AlertEmail) && string.IsNullOrWhiteSpace(config.AlertWebhookUrl))
            {
                result.AddWarning("Alerts are enabled but no alert email or webhook URL is configured.", nameof(config.EnableAlerts));
            }
        }

        if (config.SafetyCheckIntervalSeconds < 1)
        {
            result.AddError("Safety check interval must be at least 1 second.", nameof(config.SafetyCheckIntervalSeconds));
        }
        else if (config.SafetyCheckIntervalSeconds > 300)
        {
            result.AddWarning("Safety check interval is more than 5 minutes. Consider reducing for better monitoring.", nameof(config.SafetyCheckIntervalSeconds));
        }

        if (config.EnableAutoRecovery)
        {
            if (config.MaxRecoveryAttempts < 1 || config.MaxRecoveryAttempts > 10)
            {
                result.AddWarning("Max recovery attempts should be between 1 and 10.", nameof(config.MaxRecoveryAttempts));
            }

            if (config.RecoveryDelaySeconds < 1)
            {
                result.AddError("Recovery delay must be at least 1 second.", nameof(config.RecoveryDelaySeconds));
            }
        }
    }

    private void AddLiveTradingWarnings(LiveTradingConfiguration config, ValidationResult result)
    {

        if (!config.EnableDryRun && !config.RequireManualApproval)
        {
            result.AddWarning("Live trading is enabled without dry run or manual approval. Ensure you have thoroughly tested your strategy.", nameof(config.EnableDryRun));
        }

        if (!config.EnableFileLogging)
        {
            result.AddWarning("File logging is disabled. You will have no persistent log records of trading activity.", nameof(config.EnableFileLogging));
        }

        if (!config.EnablePerformanceMonitoring)
        {
            result.AddWarning("Performance monitoring is disabled. You will not be able to track strategy performance in real-time.", nameof(config.EnablePerformanceMonitoring));
        }

        if (config.SessionTimeoutHours > 12)
        {
            result.AddWarning($"Session timeout is set to {config.SessionTimeoutHours} hours. Consider if this is appropriate for your trading schedule.", nameof(config.SessionTimeoutHours));
        }
    }

    private bool HasOverlappingDays(TradingSession session1, TradingSession session2)
    {
        return session1.DaysOfWeek.Intersect(session2.DaysOfWeek).Any();
    }

    private bool HasOverlappingTime(TradingSession session1, TradingSession session2)
    {
        return session1.StartTime < session2.EndTime && session2.StartTime < session1.EndTime;
    }
}
