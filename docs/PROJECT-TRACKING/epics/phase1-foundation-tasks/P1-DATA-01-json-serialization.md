# Task: P1-DATA-01 - Create JsonSerializationService

**Epic**: Phase1-Foundation
**Priority**: HIGH-05
**Agent**: dotnet-csharp-expert
**Status**: READY
**Dependencies**: None

## Overview

Implement a high-performance JsonSerializationService using System.Text.Json that provides optimized serialization for strategy configurations, market data, trade results, and performance metrics. This service will support the JSON export functionality required for web visualization and artifact management.

## Technical Requirements - Enterprise JSON with .NET 10

### Core Implementation - Source-Generated High Performance

1. **JsonSerializationService Class - Zero-Reflection Architecture**
   - Ultra-high-performance JSON using System.Text.Json source generators
   - Support progressive loading with async streaming for massive datasets
   - Implement adaptive compression with multiple algorithms (Brotli, GZip, LZ4)
   - Maintain schema versioning with automatic migration capabilities
   - Handle financial decimal precision with custom converters
   - Use memory-mapped files for extremely large datasets
   - Provide real-time streaming serialization for live data feeds

2. **Modern Architecture - Source Generation & Streaming**
   ```csharp
   // Source-generated serialization context for zero reflection
   [JsonSerializable(typeof(StrategyConfiguration))]
   [JsonSerializable(typeof(PerformanceMetrics))]
   [JsonSerializable(typeof(TradeExecution))]
   [JsonSerializable(typeof(OptimizationStageResult))]
   [JsonSourceGenerationOptions(
       WriteIndented = false,
       PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
       DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
       GenerationMode = JsonSourceGenerationMode.Default)]
   internal partial class FinancialDataSerializationContext : JsonSerializerContext
   {
   }

   // High-performance serialization service
   public sealed class JsonSerializationService : IAsyncDisposable
   {
       private readonly ILogger<JsonSerializationService> _logger;
       private readonly IMemoryStreamManager _memoryManager;
       private readonly ICompressionService _compressionService;

       // Memory-efficient async streaming
       public async IAsyncEnumerable<T> DeserializeStreamAsync<T>(
           string filePath,
           [EnumeratorCancellation] CancellationToken cancellationToken = default)
       {
           await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
           await using var decompressedStream = await _compressionService.CreateDecompressionStreamAsync(fileStream);

           await foreach (var item in JsonSerializer.DeserializeAsyncEnumerable<T>(
               decompressedStream, FinancialDataSerializationContext.Default.Options, cancellationToken))
           {
               if (item != null) yield return item;
           }
       }

       // Zero-allocation serialization for hot paths
       public ValueTask<ReadOnlyMemory<byte>> SerializeToMemoryAsync<T>(
           T value,
           CancellationToken cancellationToken = default)
       {
           var buffer = _memoryManager.GetBuffer();
           try
           {
               using var writer = new Utf8JsonWriter(buffer);
               JsonSerializer.Serialize(writer, value, FinancialDataSerializationContext.Default.Options);
               writer.Flush();

               return new ValueTask<ReadOnlyMemory<byte>>(buffer.WrittenMemory.ToArray());
           }
           finally
           {
               _memoryManager.ReturnBuffer(buffer);
           }
       }

       // Memory-mapped file support for massive datasets
       public async Task SerializeLargeDatasetAsync<T>(
           IAsyncEnumerable<T> data,
           string filePath,
           SerializationOptions options,
           CancellationToken cancellationToken = default)
       {
           var estimatedSize = options.EstimatedTotalSize ?? 1024L * 1024 * 1024; // 1GB default
           using var mmf = MemoryMappedFile.CreateFromFile(filePath, FileMode.Create, "large-dataset", estimatedSize);
           using var accessor = mmf.CreateViewAccessor();

           await SerializeToMemoryMappedFileAsync(data, accessor, options, cancellationToken);
       }
   }
   ```

3. **Progressive Export Capabilities**
   ```csharp
   public class ProgressiveJsonExporter
   {
       public async Task ExportStrategyConfigAsync(ArtifactPath path, StrategyConfiguration config);
       public async Task ExportMarketDataReferenceAsync(ArtifactPath path, MarketDataReference data);
       public async Task ExportTradesAsync(ArtifactPath path, List<TradeExecution> trades);
       public async Task ExportMetricsAsync(ArtifactPath path, PerformanceMetrics metrics);
       public async Task ExportOptimizationResultsAsync(ArtifactPath path, OptimizationStageResult results);
   }
   ```

### File Structure

Create in `StockSharp.AdvancedBacktest/Infrastructure/Serialization/`:
- `JsonSerializationService.cs` - Main serialization service
- `ProgressiveJsonExporter.cs` - Progressive export for large datasets
- `SchemaVersioning.cs` - Schema version management
- `FinancialDataConverters.cs` - Custom converters for financial types
- `CompressionService.cs` - Compression for large datasets
- `ValidationService.cs` - JSON schema validation

## Implementation Details

### Ultra-High-Performance Serialization - Source Generated

1. **Source-Generated Configuration with Custom Converters**
   ```csharp
   // Compile-time optimized serialization options
   [JsonSourceGenerationOptions(
       WriteIndented = false, // Minimize size for performance
       PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
       DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
       GenerationMode = JsonSourceGenerationMode.Default,
       UseStringEnumConverter = true)]
   internal partial class OptimizedSerializationContext : JsonSerializerContext
   {
       // Custom financial precision decimal converter
       public static readonly JsonConverter<decimal> FinancialDecimalConverter = new FinancialPrecisionDecimalConverter();

       // High-performance DateTime converter with ISO8601
       public static readonly JsonConverter<DateTime> OptimizedDateTimeConverter = new Iso8601DateTimeConverter();

       // StockSharp SecurityId converter
       public static readonly JsonConverter<SecurityId> SecurityIdConverter = new SecurityIdJsonConverter();
   }

   // Financial-precision decimal converter preventing precision loss
   public sealed class FinancialPrecisionDecimalConverter : JsonConverter<decimal>
   {
       public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
       {
           if (reader.TokenType == JsonTokenType.String)
           {
               var stringValue = reader.GetString();
               return decimal.Parse(stringValue!, NumberStyles.Float, CultureInfo.InvariantCulture);
           }

           return reader.GetDecimal();
       }

       public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
       {
           // Use string representation to maintain full precision
           writer.WriteStringValue(value.ToString("G29", CultureInfo.InvariantCulture));
       }
   }

   // Zero-allocation DateTime converter
   public sealed class Iso8601DateTimeConverter : JsonConverter<DateTime>
   {
       private static readonly byte[] FormatBytes = "yyyy-MM-ddTHH:mm:ss.fffffffZ"u8.ToArray();

       public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
       {
           return DateTime.ParseExact(reader.GetString()!, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
       }

       public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
       {
           // Use UTF-8 directly for maximum performance
           Span<byte> buffer = stackalloc byte[28]; // Max ISO8601 length
           var written = Utf8Formatter.TryFormat(value, buffer, out var bytesWritten, new StandardFormat('O'));
           if (written)
           {
               writer.WriteStringValue(buffer.Slice(0, bytesWritten));
           }
           else
           {
               writer.WriteStringValue(value.ToString("O", CultureInfo.InvariantCulture));
           }
       }
   }
   ```

2. **Custom Financial Data Converters**
   ```csharp
   public class DecimalConverter : JsonConverter<decimal>
   {
       public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options);
       public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options);
   }

   public class SecurityIdConverter : JsonConverter<SecurityId>
   public class TradeConverter : JsonConverter<Trade>
   public class DateTimeConverter : JsonConverter<DateTime>
   ```

3. **Progressive Loading Support**
   ```csharp
   public class PaginatedJsonWriter<T>
   {
       public async Task WritePageAsync(IEnumerable<T> data, string filePath, int pageNumber);
       public async Task WritePaginatedAsync(IEnumerable<T> data, string basePath, int pageSize);
       public async Task CreateIndexAsync(string basePath, PaginationMetadata metadata);
   }
   ```

### Schema Management

1. **Schema Versioning**
   ```csharp
   public class SchemaVersioning
   {
       public const string CurrentVersion = "1.0.0";

       public static SchemaInfo GetSchemaInfo<T>();
       public static bool IsCompatible(string version);
       public static T MigrateSchema<T>(string json, string fromVersion, string toVersion);
   }

   public class SchemaInfo
   {
       public string Version { get; set; }
       public string TypeName { get; set; }
       public DateTime CreatedAt { get; set; }
       public Dictionary<string, PropertyInfo> Properties { get; set; }
   }
   ```

2. **Export Manifest**
   ```csharp
   public class ExportManifest
   {
       public string SchemaVersion { get; set; }
       public DateTime ExportTimestamp { get; set; }
       public Dictionary<string, FileInfo> Files { get; set; }
       public ExportStatistics Statistics { get; set; }

       public class FileInfo
       {
           public long Size { get; set; }
           public string Hash { get; set; }
           public string Description { get; set; }
           public string ContentType { get; set; }
           public bool IsCompressed { get; set; }
       }
   }
   ```

### Specialized Export Functions

1. **Strategy Configuration Export**
   ```csharp
   public async Task ExportStrategyConfigAsync(ArtifactPath path, StrategyConfiguration config)
   {
       var exportData = new
       {
           Metadata = new
           {
               StrategyName = config.StrategyName,
               Version = config.Version,
               ExportedAt = DateTime.UtcNow,
               SchemaVersion = SchemaVersioning.CurrentVersion
           },
           Parameters = config.Parameters,
           Settings = config.Settings,
           Validation = config.ValidationRules
       };

       await SerializeToFileAsync(exportData, Path.Combine(path.GetFullPath(), "strategy-config.json"));
   }
   ```

2. **Performance Metrics Export**
   ```csharp
   public async Task ExportMetricsAsync(ArtifactPath path, PerformanceMetrics metrics)
   {
       var exportData = new
       {
           Metadata = new
           {
               CalculatedAt = DateTime.UtcNow,
               SchemaVersion = SchemaVersioning.CurrentVersion,
               MetricsVersion = "1.0.0"
           },
           BasicMetrics = new
           {
               metrics.TotalReturn,
               metrics.AnnualizedReturn,
               metrics.MaxDrawdown,
               metrics.SharpeRatio,
               metrics.WinRate
           },
           RiskMetrics = metrics.RiskMetrics,
           TradeStatistics = metrics.TradeStats,
           DrawdownAnalysis = metrics.DrawdownStats
       };

       await SerializeToFileAsync(exportData, Path.Combine(path.GetFullPath(), "performance-metrics.json"));
   }
   ```

3. **Trade Data Export with Pagination**
   ```csharp
   public async Task ExportTradesAsync(ArtifactPath path, List<TradeExecution> trades)
   {
       const int pageSize = 1000;
       var tradesPath = Path.Combine(path.GetFullPath(), "trades");
       Directory.CreateDirectory(tradesPath);

       // Export trades in pages
       await ExportWithPaginationAsync(trades, tradesPath, pageSize);

       // Create summary file
       var summary = new
       {
           TotalTrades = trades.Count,
           PageSize = pageSize,
           TotalPages = (int)Math.Ceiling((double)trades.Count / pageSize),
           FirstTradeDate = trades.FirstOrDefault()?.Time,
           LastTradeDate = trades.LastOrDefault()?.Time,
           SchemaVersion = SchemaVersioning.CurrentVersion
       };

       await SerializeToFileAsync(summary, Path.Combine(path.GetFullPath(), "trades-summary.json"));
   }
   ```

### Advanced Compression and Memory Management

1. **Adaptive Compression with Multiple Algorithms**
   ```csharp
   // High-performance compression service with algorithm selection
   public sealed class AdaptiveCompressionService : ICompressionService
   {
       private readonly ILogger<AdaptiveCompressionService> _logger;
       private readonly ConcurrentDictionary<CompressionAlgorithm, ObjectPool<Stream>> _streamPools;

       public async Task<CompressionResult> CompressAsync(
           ReadOnlyMemory<byte> data,
           CompressionAlgorithm? preferredAlgorithm = null,
           CancellationToken cancellationToken = default)
       {
           var algorithm = preferredAlgorithm ?? SelectOptimalAlgorithm(data.Length);

           return algorithm switch
           {
               CompressionAlgorithm.Brotli => await CompressBrotliAsync(data, cancellationToken),
               CompressionAlgorithm.GZip => await CompressGZipAsync(data, cancellationToken),
               CompressionAlgorithm.LZ4 => await CompressLZ4Async(data, cancellationToken),
               CompressionAlgorithm.None => new CompressionResult(data, CompressionAlgorithm.None, 1.0),
               _ => throw new ArgumentException($"Unsupported algorithm: {algorithm}")
           };
       }

       // Algorithm selection based on data characteristics
       private static CompressionAlgorithm SelectOptimalAlgorithm(int dataSize)
       {
           return dataSize switch
           {
               < 1024 => CompressionAlgorithm.None,           // Too small to compress
               < 100_000 => CompressionAlgorithm.LZ4,         // Fast compression for small data
               < 10_000_000 => CompressionAlgorithm.GZip,     // Balanced for medium data
               _ => CompressionAlgorithm.Brotli               // Best compression for large data
           };
       }

       // SIMD-accelerated Brotli compression
       private async Task<CompressionResult> CompressBrotliAsync(
           ReadOnlyMemory<byte> data,
           CancellationToken cancellationToken)
       {
           using var outputStream = new MemoryStream();
           using var brotliStream = new BrotliStream(outputStream, CompressionLevel.Optimal);

           await brotliStream.WriteAsync(data, cancellationToken);
           await brotliStream.FlushAsync(cancellationToken);

           var compressedData = outputStream.ToArray();
           var compressionRatio = (double)compressedData.Length / data.Length;

           return new CompressionResult(compressedData, CompressionAlgorithm.Brotli, compressionRatio);
       }
   }

   public readonly record struct CompressionResult(
       ReadOnlyMemory<byte> Data,
       CompressionAlgorithm Algorithm,
       double CompressionRatio
   );

   public enum CompressionAlgorithm
   {
       None,
       GZip,
       Brotli,
       LZ4
   }
   ```

2. **Memory-Mapped File Support for Massive Datasets**
   ```csharp
   // Memory-mapped file serialization for datasets > 2GB
   public sealed class MemoryMappedJsonSerializer
   {
       private readonly ILogger<MemoryMappedJsonSerializer> _logger;

       public async Task SerializeMassiveDatasetAsync<T>(
           IAsyncEnumerable<T> data,
           string filePath,
           long estimatedSize,
           CancellationToken cancellationToken = default)
       {
           // Create memory-mapped file
           using var mmf = MemoryMappedFile.CreateFromFile(
               filePath,
               FileMode.Create,
               "massive-dataset",
               estimatedSize);

           using var accessor = mmf.CreateViewAccessor(0, estimatedSize);
           var position = 0L;

           // Write JSON array start
           accessor.Write(position, (byte)'[');
           position += 1;

           var isFirst = true;
           await foreach (var item in data.WithCancellation(cancellationToken))
           {
               if (!isFirst)
               {
                   accessor.Write(position, (byte)',');
                   position += 1;
               }

               // Serialize item to memory
               var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(item, FinancialDataSerializationContext.Default.Options);

               // Write to memory-mapped file
               for (int i = 0; i < jsonBytes.Length; i++)
               {
                   accessor.Write(position + i, jsonBytes[i]);
               }
               position += jsonBytes.Length;

               isFirst = false;

               // Periodic flush and progress reporting
               if (position % (1024 * 1024) == 0) // Every 1MB
               {
                   accessor.Flush();
                   _logger.LogDebug("Serialized {Bytes} bytes to memory-mapped file", position);
               }
           }

           // Write JSON array end
           accessor.Write(position, (byte)']');
           accessor.Flush();

           _logger.LogInformation("Completed massive dataset serialization: {Bytes} bytes", position + 1);
       }
   }
   ```

2. **Performance Monitoring**
   ```csharp
   public class SerializationMetrics
   {
       public TimeSpan SerializationTime { get; set; }
       public long OriginalSize { get; set; }
       public long CompressedSize { get; set; }
       public double CompressionRatio => (double)CompressedSize / OriginalSize;
       public int ObjectCount { get; set; }
   }
   ```

## Acceptance Criteria

### Functional Requirements

- [ ] Serializes all required data types (strategies, trades, metrics, market data)
- [ ] Supports progressive loading and pagination for large datasets
- [ ] Maintains precision for financial decimal values
- [ ] Handles schema versioning and migration
- [ ] Provides compression for large files

### Performance Requirements

- [ ] Serializes 10,000 trade records within 2 seconds
- [ ] Memory usage stays below 512MB for large datasets
- [ ] Compression reduces file size by at least 30% for large datasets
- [ ] Deserialization performance within 50% of serialization speed

### Quality Requirements

- [ ] No precision loss for decimal financial values
- [ ] Proper handling of null and empty collections
- [ ] Thread-safe for concurrent operations
- [ ] Graceful error handling with detailed error messages

## Implementation Specifications

### JSON Schema Standards

1. **Strategy Configuration Schema**
   ```json
   {
     "schemaVersion": "1.0.0",
     "metadata": {
       "strategyName": "string",
       "version": "string",
       "exportedAt": "ISO8601 datetime"
     },
     "parameters": {
       "parameterName": {
         "value": "any",
         "type": "string",
         "description": "string"
       }
     }
   }
   ```

2. **Trade Data Schema**
   ```json
   {
     "schemaVersion": "1.0.0",
     "trades": [
       {
         "id": "string",
         "time": "ISO8601 datetime",
         "symbol": "string",
         "side": "Buy|Sell",
         "quantity": "number",
         "price": "number",
         "commission": "number",
         "pnl": "number"
       }
     ]
   }
   ```

### Error Handling

1. **Serialization Errors**
   - Detailed error messages with property path
   - Graceful handling of circular references
   - Validation of required properties

2. **File System Errors**
   - Retry mechanisms for transient I/O errors
   - Atomic write operations to prevent corruption
   - Proper cleanup of temporary files

## Dependencies - High-Performance JSON Stack

### NuGet Packages Required

```xml
<!-- Core JSON Serialization -->
<PackageReference Include="System.Text.Json" Version="8.0.0" />

<!-- Advanced Compression -->
<PackageReference Include="System.IO.Compression" Version="8.0.0" />
<PackageReference Include="System.IO.Compression.Brotli" Version="8.0.0" />
<PackageReference Include="K4os.Compression.LZ4" Version="1.3.5" /> <!-- Fast compression for streaming -->

<!-- Memory Management -->
<PackageReference Include="System.Memory" Version="8.0.0" />
<PackageReference Include="System.IO.MemoryMappedFiles" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.ObjectPool" Version="8.0.0" />

<!-- Modern .NET Patterns -->
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Options" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="8.0.0" />

<!-- Async Streaming -->
<PackageReference Include="System.Threading.Channels" Version="8.0.0" />
<PackageReference Include="System.Linq.Async" Version="6.0.1" />

<!-- Performance Monitoring -->
<PackageReference Include="System.Diagnostics.DiagnosticSource" Version="8.0.0" />

<!-- Development/Testing -->
<PackageReference Include="BenchmarkDotNet" Version="0.13.7" Condition="'$(Configuration)' == 'Release'" />
```

### Framework Dependencies - High-Performance JSON

- **.NET 10**: Required for latest System.Text.Json source generation improvements
- **System.Text.Json**: Source-generated serialization for zero reflection
- **System.Memory**: High-performance memory operations with Span<T> and Memory<T>
- **System.IO.Compression**: Multiple compression algorithms (GZip, Brotli, Deflate)
- **System.IO.MemoryMappedFiles**: Handle datasets larger than available RAM
- **System.Threading.Channels**: High-performance async streaming
- **System.Linq.Async**: Async enumerable operations for large datasets

### Source Generation Configuration

```xml
<!-- Enable System.Text.Json source generation -->
<PropertyGroup>
  <JsonSerializerIsReflectionEnabledByDefault>false</JsonSerializerIsReflectionEnabledByDefault>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
</PropertyGroup>

<!-- Source generator analyzers -->
<ItemGroup>
  <Analyzer Include="System.Text.Json.SourceGeneration" />
</ItemGroup>

<!-- Unsafe code for high-performance scenarios -->
<PropertyGroup>
  <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
</PropertyGroup>
```

### Performance Optimization Settings

```xml
<!-- Enable all performance optimizations -->
<PropertyGroup>
  <TieredCompilation>true</TieredCompilation>
  <TieredPGO>true</TieredPGO>
  <ReadyToRun>true</ReadyToRun>
</PropertyGroup>
```

## Definition of Done

1. **Code Complete**
   - JsonSerializationService fully implemented
   - Progressive export capabilities working
   - Schema versioning functional
   - Compression support enabled

2. **Testing Complete**
   - Unit tests for all serialization scenarios
   - Performance testing with large datasets
   - Schema validation testing
   - Round-trip serialization tests

3. **Documentation Complete**
   - XML documentation for all public APIs
   - JSON schema documentation
   - Performance characteristics documented
   - Usage examples and best practices

4. **Integration Verified**
   - Works with all required data types
   - Integrates with artifact management
   - Compatible with reporting system
   - Performance meets requirements

## Implementation Notes

### Design Considerations

1. **Performance**: Optimize for large dataset serialization
2. **Precision**: Maintain decimal precision for financial calculations
3. **Compatibility**: Support schema evolution and migration
4. **Reliability**: Robust error handling and recovery

### Common Pitfalls to Avoid

1. Precision loss with decimal financial values
2. Memory exhaustion with large datasets
3. Circular reference issues in object graphs
4. Platform-specific serialization differences

## Summary - Enterprise-Grade JSON Performance

This task delivers **world-class JSON serialization performance** using the latest .NET 10 capabilities for financial data processing:

### Technical Excellence:
- **Zero-Reflection Serialization**: Source-generated JSON with compile-time optimization
- **Financial-Precision Handling**: Custom decimal converters maintaining full precision
- **Adaptive Compression**: Automatic algorithm selection (Brotli, GZip, LZ4) based on data characteristics
- **Memory-Mapped Files**: Handle datasets larger than available RAM efficiently
- **Streaming Architecture**: Process massive datasets with constant memory usage
- **SIMD Acceleration**: Vectorized operations where applicable for mathematical data

### Performance Achievements:
- **100MB/second** JSON serialization throughput for financial data
- **50-80% compression ratios** for typical trading datasets
- **O(1) memory usage** regardless of dataset size through streaming
- **Zero allocations** in hot serialization paths
- **Sub-millisecond** deserialization for small objects
- **Multi-GB dataset support** with memory-mapped file architecture

### Financial Data Specialization:
- **Decimal Precision**: No precision loss for financial calculations
- **StockSharp Integration**: Native support for SecurityId, Trade, and Portfolio types
- **Schema Evolution**: Automatic migration for data format changes
- **Time Series Optimization**: Specialized handling for high-frequency financial data
- **Regulatory Compliance**: Audit-trail friendly serialization with versioning

### Integration Points:
- **Artifact Management**: Foundation for optimization result storage
- **Web Visualization**: JSON export for Next.js reporting frontend
- **Real-Time Streaming**: Live data feed serialization for monitoring
- **Backup and Recovery**: Checkpoint serialization for long-running optimizations

**Success Criteria**: Serialize complex financial datasets at institutional scale while maintaining mathematical precision and enabling real-time visualization workflows.