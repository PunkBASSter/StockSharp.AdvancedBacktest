namespace StockSharp.AdvancedBacktest.LauncherTemplate.Configuration.Validation;

public class ValidationResult
{
    public List<ValidationMessage> Errors { get; } = [];
    public List<ValidationMessage> Warnings { get; } = [];
    public bool IsValid => Errors.Count == 0;
    public bool HasWarnings => Warnings.Count > 0;
    public void AddError(string message, string? propertyName = null)
    {
        Errors.Add(new ValidationMessage(message, propertyName, ValidationSeverity.Error));
    }
    public void AddWarning(string message, string? propertyName = null)
    {
        Warnings.Add(new ValidationMessage(message, propertyName, ValidationSeverity.Warning));
    }
    public IEnumerable<ValidationMessage> GetAllMessages()
    {
        return Errors.Concat(Warnings);
    }
    public string GetFormattedMessages()
    {
        var messages = new List<string>();
        if (Errors.Count > 0)
        {
            messages.Add("Errors:");
            foreach (var error in Errors)
            {
                messages.Add($"  - {error}");
            }
        }
        if (Warnings.Count > 0)
        {
            if (messages.Count > 0)
            {
                messages.Add("");
            }
            messages.Add("Warnings:");
            foreach (var warning in Warnings)
            {
                messages.Add($"  - {warning}");
            }
        }
        return string.Join(Environment.NewLine, messages);
    }
    public void ThrowIfInvalid(string configurationName = "Configuration")
    {
        if (!IsValid)
        {
            throw new ConfigurationValidationException(
                $"{configurationName} validation failed with {Errors.Count} error(s).",
                this);
        }
    }
}
public class ValidationMessage
{
    public ValidationMessage(string message, string? propertyName, ValidationSeverity severity)
    {
        Message = message ?? throw new ArgumentNullException(nameof(message));
        PropertyName = propertyName;
        Severity = severity;
    }
    public string Message { get; }
    public string? PropertyName { get; }
    public ValidationSeverity Severity { get; }
    public override string ToString()
    {
        return PropertyName != null
            ? $"{PropertyName}: {Message}"
            : Message;
    }
}
public enum ValidationSeverity
{
    Warning,
    Error
}
public class ConfigurationValidationException : Exception
{
    public ConfigurationValidationException(string message, ValidationResult validationResult)
        : base(message)
    {
        ValidationResult = validationResult ?? throw new ArgumentNullException(nameof(validationResult));
    }
    public ValidationResult ValidationResult { get; }
    public override string Message => $"{base.Message}{Environment.NewLine}{ValidationResult.GetFormattedMessages()}";
}
