using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StockSharp.AdvancedBacktest.DebugEventLogMcpServer.Tests.Helpers;

public sealed class McpStdioClient(Stream inputStream, Stream outputStream) : IAsyncDisposable
{
    private readonly StreamWriter _writer = new(inputStream, Encoding.UTF8, leaveOpen: true);
    private readonly StreamReader _reader = new(outputStream, Encoding.UTF8, leaveOpen: true);
    private int _requestId;
    private bool _disposed;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<JsonElement> InitializeAsync(CancellationToken ct = default)
    {
        var request = new JsonRpcRequest
        {
            Id = ++_requestId,
            Method = "initialize",
            Params = new
            {
                protocolVersion = "2025-06-18",
                capabilities = new { },
                clientInfo = new
                {
                    name = "test-client",
                    version = "1.0.0"
                }
            }
        };

        return await SendRequestAsync(request, ct);
    }

    public async Task<JsonElement> ListToolsAsync(CancellationToken ct = default)
    {
        var request = new JsonRpcRequest
        {
            Id = ++_requestId,
            Method = "tools/list",
            Params = new { }
        };

        return await SendRequestAsync(request, ct);
    }

    public async Task<JsonElement> CallToolAsync(string toolName, object arguments, CancellationToken ct = default)
    {
        var request = new JsonRpcRequest
        {
            Id = ++_requestId,
            Method = "tools/call",
            Params = new
            {
                name = toolName,
                arguments
            }
        };

        return await SendRequestAsync(request, ct);
    }

    private async Task<JsonElement> SendRequestAsync(JsonRpcRequest request, CancellationToken ct)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(McpStdioClient));

        var requestJson = JsonSerializer.Serialize(request, JsonOptions);
        await _writer.WriteLineAsync(requestJson.AsMemory(), ct);
        await _writer.FlushAsync(ct);

        // Read lines until we get a JSON response (skip logging messages)
        string? responseLine;
        do
        {
            responseLine = await _reader.ReadLineAsync(ct);
            if (responseLine is null)
            {
                throw new InvalidOperationException("No response received from MCP server - stream ended");
            }
        } while (!responseLine.TrimStart().StartsWith('{'));

        var response = JsonSerializer.Deserialize<JsonElement>(responseLine, JsonOptions);

        // Check for JSON-RPC error
        if (response.TryGetProperty("error", out var error))
        {
            var message = error.TryGetProperty("message", out var msg) ? msg.GetString() : "Unknown error";
            var code = error.TryGetProperty("code", out var c) ? c.GetInt32() : -1;
            throw new McpErrorException(code, message ?? "Unknown error");
        }

        if (response.TryGetProperty("result", out var result))
        {
            return result;
        }

        return response;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            await _writer.DisposeAsync();
        }
        catch (ObjectDisposedException)
        {
            // Stream already closed, ignore
        }
        catch (IOException)
        {
            // Pipe closed, ignore
        }

        try
        {
            _reader.Dispose();
        }
        catch (ObjectDisposedException)
        {
            // Stream already closed, ignore
        }
    }

    private sealed class JsonRpcRequest
    {
        [JsonPropertyName("jsonrpc")]
        public string JsonRpc { get; } = "2.0";

        [JsonPropertyName("id")]
        public int Id { get; init; }

        [JsonPropertyName("method")]
        public required string Method { get; init; }

        [JsonPropertyName("params")]
        public object? Params { get; init; }
    }
}

public sealed class McpErrorException(int code, string message) : Exception(message)
{
    public int Code { get; } = code;
}
