namespace StockSharp.AdvancedBacktest.DebugEventLogMcpServer;

public sealed class ProgramArgs
{
    public bool Shutdown { get; init; }
    public string? DatabasePath { get; init; }

    public static ProgramArgs Parse(string[] args)
    {
        bool shutdown = false;
        string? databasePath = null;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--shutdown")
            {
                shutdown = true;
            }
            else if (args[i] == "--database" && i + 1 < args.Length)
            {
                databasePath = args[++i];
            }
        }

        return new ProgramArgs
        {
            Shutdown = shutdown,
            DatabasePath = databasePath
        };
    }
}
