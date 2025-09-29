using StockSharp.AdvancedBacktest.Core.Optimization.Demo;

Console.WriteLine("Starting ParameterSpaceExplorer Performance Test...");
Console.WriteLine();

try
{
    await PerformanceDemo.RunAsync();
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    return 1;
}

Console.WriteLine("Press any key to exit...");
Console.ReadKey();
return 0;