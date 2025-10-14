namespace StockSharp.AdvancedBacktest.Parameters;

public class CustomParamsContainer
{
    private readonly IReadOnlyDictionary<string, ICustomParam> _params;

    public IReadOnlyList<ICustomParam> CustomParams { get; }
    public List<Func<IDictionary<string, ICustomParam>, bool>> ValidationRules { get; init; } = [];

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

    public string GenerateHash()
    {
        return string.Join(";", CustomParams
            .OrderBy(p => p.Id)  // Deterministic ordering
            .Select(p => $"{p.Id}={p.Value}"));
    }
}
