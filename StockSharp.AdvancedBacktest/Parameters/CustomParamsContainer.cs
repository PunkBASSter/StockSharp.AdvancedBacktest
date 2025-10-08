namespace StockSharp.AdvancedBacktest.Parameters;

/// <summary>
/// Container for managing strategy parameters with efficient lookup and validation.
/// Centralizes all parameter-related operations including access, modification, and hashing.
/// </summary>
public class CustomParamsContainer
{
    private readonly Dictionary<string, ICustomParam> _paramsLookup = new();

    public List<ICustomParam> CustomParams { get; private set; } = [];
    public List<Func<IDictionary<string, ICustomParam>, bool>> ValidationRules { get; set; } = [];

    public void Add(ICustomParam param)
    {
        CustomParams.Add(param);
        _paramsLookup[param.Id] = param;
    }

    public void AddRange(IEnumerable<ICustomParam> parameters)
    {
        foreach (var param in parameters)
        {
            Add(param);
        }
    }

    public void Initialize()
    {
        _paramsLookup.Clear();
        foreach (var param in CustomParams)
        {
            _paramsLookup[param.Id] = param;
        }
    }

    public T Get<T>(string id)
    {
        if (_paramsLookup.TryGetValue(id, out var param))
        {
            return (T)param.Value;
        }

        throw new InvalidOperationException(
            $"Parameter '{id}' not found in CustomParams. " +
            $"Available parameters: {string.Join(", ", _paramsLookup.Keys)}");
    }

    public bool TryGet<T>(string id, out T value)
    {
        if (_paramsLookup.TryGetValue(id, out var param))
        {
            value = (T)param.Value;
            return true;
        }

        value = default!;
        return false;
    }

    public bool Contains(string id) => _paramsLookup.ContainsKey(id);

    public int Count => CustomParams.Count;

    public string GenerateHash()
    {
        var hash = string.Join(";", CustomParams.Select(p => $"{p.Id}={p.Value}"));
        return hash.GetHashCode().ToString("X");
    }

    public bool Validate()
    {
        var paramsDict = _paramsLookup.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        return ValidationRules.All(rule => rule(paramsDict));
    }
}
