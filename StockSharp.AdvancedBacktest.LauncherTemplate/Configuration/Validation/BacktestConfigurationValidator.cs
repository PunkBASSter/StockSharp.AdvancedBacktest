using FluentValidation;
using StockSharp.AdvancedBacktest.LauncherTemplate.Configuration.Models;

namespace StockSharp.AdvancedBacktest.LauncherTemplate.Configuration.Validation;

public class BacktestConfigurationValidator : AbstractValidator<BacktestConfiguration>
{
    public BacktestConfigurationValidator()
    {
        // Strategy name validation
        RuleFor(x => x.StrategyName)
            .NotEmpty().WithMessage("Strategy name is required.")
            .MaximumLength(100).WithMessage("Strategy name cannot exceed 100 characters.");

        // Strategy version validation
        RuleFor(x => x.StrategyVersion)
            .NotEmpty().WithMessage("Strategy version is required.");

        // Securities validation
        RuleFor(x => x.Securities)
            .NotNull().WithMessage("Securities cannot be null.")
            .NotEmpty().WithMessage("At least one security must be specified.")
            .Must(securities => securities.All(s => !string.IsNullOrWhiteSpace(s)))
                .WithMessage("All securities must have valid values.")
            .Must(securities => securities.Count == securities.Distinct().Count())
                .WithMessage("Duplicate securities are not allowed.")
                .WithSeverity(Severity.Warning);

        RuleForEach(x => x.Securities)
            .NotEmpty().WithMessage("Security value cannot be empty.");

        // History path validation
        RuleFor(x => x.HistoryPath)
            .NotEmpty().WithMessage("History path is required.")
            .Must(path => Directory.Exists(path) || File.Exists(path))
                .WithMessage(x => $"History path does not exist: '{x.HistoryPath}'")
                .When(x => !string.IsNullOrWhiteSpace(x.HistoryPath));

        // Initial capital validation
        RuleFor(x => x.InitialCapital)
            .GreaterThan(0).WithMessage("Initial capital must be greater than 0.")
            .GreaterThanOrEqualTo(100).WithMessage("Initial capital is very low (< 100). This may not be realistic for testing.")
                .WithSeverity(Severity.Warning);

        // Trade volume validation
        RuleFor(x => x.TradeVolume)
            .GreaterThan(0).WithMessage("Trade volume must be greater than 0.");

        // Commission percentage validation
        RuleFor(x => x.CommissionPercentage)
            .GreaterThanOrEqualTo(0).WithMessage("Commission percentage cannot be negative.")
            .LessThanOrEqualTo(100).WithMessage("Commission percentage cannot exceed 100%.")
            .LessThanOrEqualTo(10).WithMessage("Commission percentage is unusually high (> 10%). Please verify this is correct.")
                .WithSeverity(Severity.Warning);

        // Parallel workers validation
        RuleFor(x => x.ParallelWorkers)
            .GreaterThanOrEqualTo(1).WithMessage("Parallel workers must be at least 1.")
            .LessThanOrEqualTo(Environment.ProcessorCount * 2)
                .WithMessage(x => $"Parallel workers ({x.ParallelWorkers}) exceeds 2x processor count ({Environment.ProcessorCount}). This may not improve performance.")
                .WithSeverity(Severity.Warning);

        // Date range validations
        RuleFor(x => x.TrainingEndDate)
            .GreaterThan(x => x.TrainingStartDate)
                .WithMessage("Training end date must be after training start date.");

        RuleFor(x => x)
            .Must(x => (x.TrainingEndDate - x.TrainingStartDate).TotalDays >= 7)
                .WithMessage("Training period is less than 7 days. This may not provide sufficient data for optimization.")
                .WithSeverity(Severity.Warning);

        RuleFor(x => x.ValidationEndDate)
            .GreaterThan(x => x.ValidationStartDate)
                .WithMessage("Validation end date must be after validation start date.");

        RuleFor(x => x)
            .Must(x => (x.ValidationEndDate - x.ValidationStartDate).TotalDays >= 7)
                .WithMessage("Validation period is less than 7 days. This may not provide sufficient data for validation.")
                .WithSeverity(Severity.Warning);

        RuleFor(x => x.ValidationStartDate)
            .GreaterThanOrEqualTo(x => x.TrainingEndDate)
                .WithMessage("Validation period overlaps with training period. Validation start date must be on or after training end date.");

        RuleFor(x => x)
            .Must(x => (x.ValidationStartDate - x.TrainingEndDate).TotalDays <= 30)
                .WithMessage(x => $"There is a {(x.ValidationStartDate - x.TrainingEndDate).TotalDays:F0}-day gap between training and validation periods. Consider if this is intentional.")
                .WithSeverity(Severity.Warning);

        RuleFor(x => x.TrainingEndDate)
            .LessThanOrEqualTo(DateTimeOffset.UtcNow)
                .WithMessage("Training end date cannot be in the future.");

        RuleFor(x => x.ValidationEndDate)
            .LessThanOrEqualTo(DateTimeOffset.UtcNow)
                .WithMessage("Validation end date cannot be in the future.")
            .GreaterThan(DateTimeOffset.UtcNow.AddYears(-10))
                .WithMessage("Validation end date is more than 10 years old. Market conditions may have changed significantly.")
                .WithSeverity(Severity.Warning);

        // Export path validation
        RuleFor(x => x.ExportPath)
            .Must((config, exportPath) => IsValidExportPath(exportPath))
                .WithMessage(x => $"Invalid export path: '{x.ExportPath}'")
                .When(x => !string.IsNullOrWhiteSpace(x.ExportPath));

        RuleFor(x => x.ExportPath)
            .Must((config, exportPath) => DirectoryExistsOrCanBeCreated(exportPath))
                .WithMessage(x => $"Export path directory does not exist and will be created: '{Path.GetDirectoryName(x.ExportPath)}'")
                .WithSeverity(Severity.Warning)
                .When(x => !string.IsNullOrWhiteSpace(x.ExportPath));

        // Optimizable parameters validation
        RuleFor(x => x.OptimizableParameters)
            .NotNull().WithMessage("Optimizable parameters cannot be null.")
            .NotEmpty().WithMessage("At least one optimizable parameter must be specified.")
            .Must(parameters => parameters.Count <= 10)
                .WithMessage(x => $"You have {x.OptimizableParameters.Count} optimizable parameters. This may result in very long optimization times.")
                .WithSeverity(Severity.Warning);

        RuleForEach(x => x.OptimizableParameters)
            .ChildRules(param =>
            {
                param.RuleFor(p => p.Key)
                    .NotEmpty().WithMessage("Parameter name cannot be null or empty.");

                param.RuleFor(p => p.Value)
                    .SetValidator(new ParameterDefinitionValidator());
            });

        // Walk-forward config validation
        RuleFor(x => x.WalkForwardConfig)
            .Must(config => config == null || config.WindowSize.TotalDays >= 30)
                .WithMessage("Walk-forward window size is less than 30 days. This may not provide stable results.")
                .WithSeverity(Severity.Warning)
                .When(x => x.WalkForwardConfig != null);

        // Export settings validation
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.ExportPath) || !x.ExportDetailedMetrics)
                .WithMessage("ExportDetailedMetrics is enabled but ExportPath is not set. Metrics will not be exported.")
                .WithSeverity(Severity.Warning);
    }

    private bool IsValidExportPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return true;

        try
        {
            Path.GetDirectoryName(path);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool DirectoryExistsOrCanBeCreated(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return true;

        try
        {
            var directory = Path.GetDirectoryName(path);
            return string.IsNullOrWhiteSpace(directory) || Directory.Exists(directory);
        }
        catch
        {
            return false;
        }
    }
}
