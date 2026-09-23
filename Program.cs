namespace ShooterAstroidConsole;

internal static class Program
{
    private static int Main(string[] args)
    {
        // Headless logic sanity check: dotnet run -- --selftest
        if (args.Length > 0 && args[0] == "--selftest")
            return SelfTest.Run();

        // Debug probe: dotnet run -- --probe
        if (args.Length > 0 && args[0] == "--probe")
        {
            Console.WriteLine($"WindowWidth={Console.WindowWidth} WindowHeight={Console.WindowHeight}");
            Console.WriteLine($"BufferWidth={Console.BufferWidth} BufferHeight={Console.BufferHeight}");
            return 0;
        }

        var game = new Game();
        return game.Run();
    }
}
