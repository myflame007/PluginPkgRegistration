namespace Dataverse.PluginRegistration;

internal static class HelpCommand
{
    internal static int Show()
    {
        Console.WriteLine("""
    Dataverse Plugin Registration Tool
    ═════════════════════════════════

    Commands:
      init                     Create a pluginreg.json config file in the current directory
      register [options]       Register plugin steps in Dataverse
      list     [options]       List discovered steps (dry-run, no connection needed)

    Options for 'register':
      --env <name>             Use named environment from pluginreg.json (e.g. --env dev)
      --config <path>          Path to pluginreg.json (default: ./pluginreg.json)
      --dll <path>             Path to plugin DLL (overrides config)
      --connection <string>    Dataverse connection string (overrides config)
      --assembly-name <name>   Assembly/package name in Dataverse (default: DLL filename)

    Options for 'list':
      --dll <path>             Path to plugin DLL (overrides config)
      --config <path>          Path to pluginreg.json (default: ./pluginreg.json)

    Examples:
      plugin-reg init
      plugin-reg register --env dev
      plugin-reg register --env dev --dll ..\bin\Debug\net462\MyPlugin.dll
      plugin-reg register --dll MyPlugin.dll --connection "AuthType=OAuth;Url=..."
      plugin-reg list
      plugin-reg list --dll ..\bin\Debug\net462\MyPlugin.dll
    """);
        return 0;
    }
}
