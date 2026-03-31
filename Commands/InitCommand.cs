using System.Text.RegularExpressions;

namespace Dataverse.PluginRegistration;

internal static class InitCommand
{
    internal static int Run(string[] args)
    {
        // ── Config file ────────────────────────────────────────────────────────
        var configPath = ArgsResolver.GetArg(args, "--config") ?? PluginRegConfig.DefaultFileName;

        bool writeConfig = true;
        if (File.Exists(configPath))
        {
            Console.WriteLine($"Config file already exists: {configPath}");
            Console.Write("Overwrite? (y/N): ");
            var answer = Console.ReadLine()?.Trim().ToLowerInvariant();
            writeConfig = answer == "y";
        }

        if (writeConfig)
        {
            var (assemblyName, assemblyPath) = DetectProject();
            var config = PluginRegConfig.CreateDefault(assemblyName, assemblyPath);
            config.Save(configPath);
            Console.WriteLine($"Created: {Path.GetFullPath(configPath)}");
        }

        // ── Attribute templates (always scaffolded / updated) ──────────────────
        ScaffoldAttributeTemplates();

        Console.WriteLine("Edit environments and assembly paths, then run: plugin-reg register --env dev");
        return 0;
    }

    /// <summary>
    /// Detects the project in the current directory and returns the assembly name and DLL path.
    /// Reads the .csproj to determine the assembly name and whether it is an SDK-style or
    /// classic project, then builds the correct output path accordingly.
    /// Falls back to the directory name with a classic-style path if no .csproj is found.
    /// </summary>
    private static (string assemblyName, string assemblyPath) DetectProject()
    {
        var currentDir = Directory.GetCurrentDirectory();
        var csprojFiles = Directory.GetFiles(currentDir, "*.csproj");

        if (csprojFiles.Length == 1)
        {
            var csproj = File.ReadAllText(csprojFiles[0]);

            // SDK-style: <Project Sdk="...">  vs  classic: <Project ToolsVersion="...">
            bool isSdkStyle = Regex.IsMatch(csproj, @"<Project\s[^>]*Sdk\s*=");

            // AssemblyName element, fall back to project file name
            var assemblyNameMatch = Regex.Match(csproj, @"<AssemblyName>([^<]+)</AssemblyName>");
            var assemblyName = assemblyNameMatch.Success
                ? assemblyNameMatch.Groups[1].Value.Trim()
                : Path.GetFileNameWithoutExtension(csprojFiles[0]);

            string dllPath;
            if (isSdkStyle)
            {
                // SDK-style output: bin\Debug\<tfm>\Assembly.dll
                var tfmMatch = Regex.Match(csproj, @"<TargetFramework>([^<]+)</TargetFramework>");
                var tfm = tfmMatch.Success ? tfmMatch.Groups[1].Value.Trim() : "net462";
                dllPath = $@"bin\Debug\{tfm}\{assemblyName}.dll";
            }
            else
            {
                // Classic .csproj output: bin\Debug\Assembly.dll  (no framework subfolder)
                dllPath = $@"bin\Debug\{assemblyName}.dll";
            }

            return (assemblyName, dllPath);
        }

        // Fallback: directory name, classic-style path
        var fallbackName = Path.GetFileName(currentDir) ?? "MyPlugin";
        return (fallbackName, $@"bin\Debug\{fallbackName}.dll");
    }

    private static void ScaffoldAttributeTemplates()
    {
        var attributesDir = Path.Combine(Directory.GetCurrentDirectory(), "Attributes");
        Directory.CreateDirectory(attributesDir);

        var assembly = typeof(InitCommand).Assembly;

        var templates = new[]
        {
            "CrmPluginRegistrationAttribute.cs",
            "CustomApiAttributes.cs",
        };

        foreach (var fileName in templates)
        {
            var resourceName = $"Dataverse.PluginRegistration.RequiredAttributes.{fileName}";
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                Console.WriteLine($"  WARN: embedded resource '{resourceName}' not found — skipping.");
                continue;
            }

            var outPath = Path.Combine(attributesDir, fileName);
            bool existed = File.Exists(outPath);

            using var fs = File.Create(outPath);
            stream.CopyTo(fs);

            Console.WriteLine(existed
                ? $"  Updated : Attributes/{fileName}"
                : $"  Created : Attributes/{fileName}");
        }
    }
}
