using Dataverse.PluginRegistration;

var envVars = EnvFile.Load();
var command = args.Length > 0 ? args[0].ToLowerInvariant() : "help";

return command switch
{
    "init"     => InitCommand.Run(args),
    "register" => await RegisterCommand.RunAsync(args, envVars),
    "list"     => ListCommand.Run(args, envVars),
    _          => HelpCommand.Show()
};
