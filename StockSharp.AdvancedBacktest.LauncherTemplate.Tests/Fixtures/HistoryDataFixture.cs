using StockSharp.Algo.Storages;
using StockSharp.Messages;

namespace StockSharp.AdvancedBacktest.Tests.Fixtures;

public class HistoryDataFixture : IDisposable
{
    public string TestDataPath { get; private set; }
    public string MockSecurityId { get; } = "TESTBTC@TESTEX";

    public HistoryDataFixture()
    {
        TestDataPath = Path.Combine(Path.GetTempPath(), $"HistoryDataTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(TestDataPath);

        CreateMockHydraStructure();
    }

    private void CreateMockHydraStructure()
    {
        var securityDir = Path.Combine(TestDataPath, "T", MockSecurityId);
        Directory.CreateDirectory(securityDir);

        var dateDir = Path.Combine(securityDir, "2024_01_01");
        Directory.CreateDirectory(dateDir);

        var candleFile = Path.Combine(dateDir, "candles_TimeFrameCandle_1.00-00-00.bin");
        File.WriteAllBytes(candleFile, CreateMockCandleData());
    }

    private byte[] CreateMockCandleData()
    {
        using var memoryStream = new MemoryStream();
        using var writer = new BinaryWriter(memoryStream);

        writer.Write((long)638400000000000000);
        writer.Write(50000.0);
        writer.Write(51000.0);
        writer.Write(49000.0);
        writer.Write(50500.0);
        writer.Write(1000.0);

        return memoryStream.ToArray();
    }

    public LocalMarketDataDrive CreateDrive()
    {
        return new LocalMarketDataDrive(TestDataPath);
    }

    public StorageRegistry CreateRegistry()
    {
        return new StorageRegistry
        {
            DefaultDrive = CreateDrive()
        };
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(TestDataPath))
            {
                Directory.Delete(TestDataPath, recursive: true);
            }
        }
        catch
        {
        }
    }
}
