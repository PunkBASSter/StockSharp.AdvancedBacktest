using System.Collections.Immutable;
using StockSharp.AdvancedBacktest.Core.Configuration.Parameters;
using StockSharp.AdvancedBacktest.Core.Optimization;

Console.WriteLine("Simple ParameterSpaceExplorer Test");
Console.WriteLine();

try
{
    // Create simple test parameters
    var parameters = ImmutableArray.Create<ParameterDefinitionBase>(
        ParameterDefinition.CreateInteger("x", 1, 3, 2), // Should generate: 1, 2, 3
        ParameterDefinition.CreateInteger("y", 10, 12, 11) // Should generate: 10, 11, 12
    );

    Console.WriteLine("Test Parameters:");
    foreach (var param in parameters)
    {
        Console.WriteLine($"  {param.Name}: {param.GetMinValue()} to {param.GetMaxValue()}");
        Console.WriteLine($"    Values: {string.Join(", ", param.GenerateValidValues())}");
        Console.WriteLine($"    Count: {param.GetValidValueCount()}");
    }

    using var explorer = new ParameterSpaceExplorer(parameters);

    Console.WriteLine();
    Console.WriteLine($"Total combinations expected: {explorer.TotalCombinations}");
    Console.WriteLine();

    var count = 0;
    await foreach (var combination in explorer.EnumerateAsync())
    {
        Console.WriteLine($"Combination {count + 1}: x={combination["x"]}, y={combination["y"]}");
        count++;
        if (count > 10) break; // Limit output
    }

    Console.WriteLine($"Successfully processed {count} combinations");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
    return 1;
}

return 0;