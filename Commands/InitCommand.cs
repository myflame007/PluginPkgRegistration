namespace Dataverse.PluginRegistration;

internal static class InitCommand
{
    internal static int Run(string[] args)
    {
        var configPath = ArgsResolver.GetArg(args, "--config") ?? PluginRegConfig.DefaultFileName;

        if (File.Exists(configPath))
        {
            Console.WriteLine($"Config file already exists: {configPath}");
            Console.Write("Overwrite? (y/N): ");
            var answer = Console.ReadLine()?.Trim().ToLowerInvariant();
            if (answer != "y") return 0;
        }

        var currentDir = Path.GetFileName(Directory.GetCurrentDirectory()) ?? "MyPlugin";
        var config = PluginRegConfig.CreateDefault(currentDir, $@"bin\Debug\net462\{currentDir}.dll");

        config.Save(configPath);
        Console.WriteLine($"Created: {Path.GetFullPath(configPath)}");
        Console.WriteLine("Edit environments and assembly paths, then run: plugin-reg register --env dev");
        return 0;
    }
}
