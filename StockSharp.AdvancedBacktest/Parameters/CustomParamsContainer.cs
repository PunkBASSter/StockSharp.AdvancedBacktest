namespace StockSharp.AdvancedBacktest.Parameters;

/// <summary>
/// Immutable container for managing strategy parameters with efficient lookup and validation.
/// All parameters must be provided at construction time.
/// </summary>
public class CustomParamsContainer
{
    private readonly IReadOnlyDictionary<string, ICustomParam> _params;

    public IReadOnlyList<ICustomParam> CustomParams { get; }
    public List<Func<IDictionary<string, ICustomParam>, bool>> ValidationRules { get; init; } = [];

    /// <summary>
    /// Creates an immutable parameter container.
    /// </summary>
    public CustomParamsContainer(IEnumerable<ICustomParam> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var paramList = parameters.ToList();
        CustomParams = paramList.AsReadOnly();
        _params = paramList.ToDictionary(p => p.Id, p => p);
    }

    public T Get<T>(string id)
    {
        if (_params.TryGetValue(id, out var param))
        {
            return (T)param.Value;
        }

        throw new InvalidOperationException(
            $"Parameter '{id}' not found in CustomParams. " +
            $"Available parameters: {string.Join(", ", _params.Keys)}");
    }

    public bool TryGet<T>(string id, out T value)
    {
        if (_params.TryGetValue(id, out var param))
        {
            value = (T)param.Value;
            return true;
        }

        value = default!;
        return false;
    }

    public bool Contains(string id) => _params.ContainsKey(id);

    public int Count => _params.Count;

    public string GenerateHash()
    {
        var hash = string.Join(";", CustomParams.Select(p => $"{p.Id}={p.Value}"));
        return hash.GetHashCode().ToString("X");
    }

    public bool Validate()
    {
        var paramsDict = _params.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        return ValidationRules.All(rule => rule(paramsDict));
    }
}
