namespace Dataverse.PluginRegistration;

internal static class ArgsResolver
{
    internal static string? GetArg(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        return null;
    }

    internal static (string? assemblyPath, string? connectionString, string? assemblyName, string? nupkgPath, string? publisherPrefix, string? solutionName, EnvironmentConfig? envConfig, string? error)
        ResolveArgs(string[] args, bool requireConnection, Dictionary<string, string> envVars)
    {
        var dllArg = GetArg(args, "--dll");
        var connArg = GetArg(args, "--connection");
        var envArg = GetArg(args, "--env");
        var nameArg = GetArg(args, "--assembly-name");
        var configPath = GetArg(args, "--config") ?? PluginRegConfig.DefaultFileName;

        string? assemblyPath = null;
        string? connectionString = null;
        string? assemblyName = null;
        string? nupkgPath = null;
        string? publisherPrefix = null;
        string? solutionName = null;
        EnvironmentConfig? envConfig = null;

        PluginRegConfig? config = null;
        if (File.Exists(configPath))
        {
            try { config = PluginRegConfig.Load(configPath); }
            catch (Exception ex) { return (null, null, null, null, null, null, null, $"ERROR loading config: {ex.Message}"); }
        }

        if (!string.IsNullOrEmpty(dllArg))
        {
            assemblyPath = dllArg;
        }
        else if (config?.Assemblies.Count > 0)
        {
            assemblyPath = config.Assemblies[0].Path;
        }

        if (string.IsNullOrEmpty(assemblyPath))
            return (null, null, null, null, null, null, null, "ERROR: No assembly path. Use --dll <path> or create a pluginreg.json (run 'init').");

        if (!File.Exists(assemblyPath))
            return (null, null, null, null, null, null, null, $"ERROR: DLL not found: {assemblyPath}");

        assemblyName = nameArg ?? config?.Assemblies.FirstOrDefault()?.Name ?? Path.GetFileNameWithoutExtension(assemblyPath);

        var assemblyConfig = config?.Assemblies.FirstOrDefault();
        nupkgPath = assemblyConfig?.NupkgPath;
        publisherPrefix = assemblyConfig?.PublisherPrefix ?? "";
        solutionName = assemblyConfig?.SolutionName;

        if (requireConnection)
        {
            if (!string.IsNullOrEmpty(connArg))
            {
                connectionString = EnvFile.Resolve(connArg, envVars);
            }
            else if (!string.IsNullOrEmpty(envArg) && config?.Environments.TryGetValue(envArg, out var ec) == true)
            {
                EnvFile.ResolveConfig(ec, envVars);
                envConfig = ec;
                connectionString = ec.BuildConnectionString();
                Console.WriteLine($"Using environment: {envArg} ({ec.Url})");
            }
            else if (!string.IsNullOrEmpty(envArg))
            {
                var available = config?.Environments.Keys;
                var hint = available?.Count > 0 ? $" Available: {string.Join(", ", available)}" : "";
                return (null, null, null, null, null, null, null, $"ERROR: Environment '{envArg}' not found in config.{hint}");
            }
            else
            {
                return (null, null, null, null, null, null, null, "ERROR: No connection. Use --env <name>, --connection <string>, or set DATAVERSE_CONNECTION_STRING.");
            }
        }

        return (assemblyPath, connectionString, assemblyName, nupkgPath, publisherPrefix, solutionName, envConfig, null);
    }
}
