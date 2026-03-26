using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk.Query;

namespace Dataverse.PluginRegistration;

internal static class RegisterCommand
{
    internal static async Task<int> RunAsync(string[] args, Dictionary<string, string> envVars)
    {
        var (assemblyPath, connectionString, assemblyName, nupkgPath, publisherPrefix, solutionName, envConfig, error)
            = ArgsResolver.ResolveArgs(args, requireConnection: true, envVars);
        if (error != null) { Console.Error.WriteLine(error); return 1; }

        // 1. Read attributes
        Console.WriteLine($"Reading: {assemblyPath}");
        List<PluginStepInfo> steps;
        List<CustomApiInfo> customApis;
        try
        {
            steps = AttributeReader.ReadFromAssembly(assemblyPath!);
            customApis = AttributeReader.ReadCustomApisFromAssembly(assemblyPath!, Console.WriteLine);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR reading assembly: {ex.Message}");
            return 1;
        }

        if (steps.Count == 0 && customApis.Count == 0)
        {
            Console.WriteLine("No plugin step or Custom API registrations found.");
            return 0;
        }

        Console.WriteLine($"Found {steps.Count} step(s), {customApis.Count} Custom API(s).\n");

        // 2. Connect
        Console.WriteLine("Connecting to Dataverse...");
        Console.WriteLine("  (Waiting for browser login — close the tab or press Ctrl+C to cancel)");
        ServiceClient client;
        try
        {
            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

            if (envConfig != null)
            {
                client = await DataverseAuth.ConnectAsync(envConfig, cts.Token);
            }
            else
            {
                try
                {
                    var connectTask = Task.Run(() => new ServiceClient(connectionString), cts.Token);
                    client = await connectTask.WaitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    Console.Error.WriteLine("\nAborted by user (Ctrl+C).");
                    return 1;
                }

                if (!client.IsReady)
                {
                    Console.Error.WriteLine($"ERROR: Connection failed.");
                    Console.Error.WriteLine($"  LastError: {client.LastError ?? ""}");
                    if (client.LastException != null)
                        Console.Error.WriteLine($"  Exception: {client.LastException.Message}");
                    return 1;
                }
            }
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("\nAborted by user (Ctrl+C).");
            return 1;
        }
        catch (Exception ex) when (
            ex.Message.Contains("canceled", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("cancelled", StringComparison.OrdinalIgnoreCase) ||
            ex.InnerException?.Message?.Contains("canceled", StringComparison.OrdinalIgnoreCase) == true ||
            ex.InnerException?.Message?.Contains("cancelled", StringComparison.OrdinalIgnoreCase) == true)
        {
            Console.Error.WriteLine("\nAuthentication was cancelled (browser tab closed or login denied).");
            Console.Error.WriteLine("Run the command again to retry.");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: {ex.Message}");
            if (ex.InnerException != null)
                Console.Error.WriteLine($"  Inner: {ex.InnerException.Message}");
            return 1;
        }

        Console.WriteLine($"Connected: {client.ConnectedOrgFriendlyName} ({client.ConnectedOrgUniqueName})\n");

        // ── Solution check ─────────────────────────────────────────
        if (!string.IsNullOrEmpty(solutionName))
        {
            Console.WriteLine($"Checking solution: {solutionName}");
            var solutionQuery = new QueryExpression("solution") { TopCount = 1 };
            solutionQuery.Criteria.AddCondition("uniquename", ConditionOperator.Equal, solutionName);
            if (client.RetrieveMultiple(solutionQuery).Entities.Count == 0)
            {
                Console.Error.WriteLine($"ERROR: Solution '{solutionName}' not found in this environment.");
                Console.Error.WriteLine($"  Create it in Dataverse first, or remove 'solution' from pluginreg.json.");
                return 1;
            }
            Console.WriteLine($"  Solution found.\n");
        }

        // ── Step 1/3: Push Assembly or NuGet package ───────────────
        Console.WriteLine("Step 1/3: Push Plugin Assembly/Package");
        Console.WriteLine(new string('─', 40));
        if (!string.IsNullOrEmpty(nupkgPath) && File.Exists(nupkgPath))
        {
            var deployer = new PackageDeployer(client, Console.WriteLine);
            try
            {
                deployer.Push(nupkgPath, assemblyName!, publisherPrefix!, solutionName);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"ERROR pushing package: {ex.Message}");
                if (ex.InnerException != null)
                    Console.Error.WriteLine($"  Inner: {ex.InnerException.Message}");
                return 1;
            }
        }
        else
        {
            var pluginTypeNames = steps.Select(s => s.PluginTypeName)
                .Concat(customApis.Select(a => a.PluginTypeName))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var asmDeployer = new AssemblyDeployer(client, Console.WriteLine);
            try
            {
                asmDeployer.Push(assemblyPath!, assemblyName!, pluginTypeNames);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"ERROR pushing assembly: {ex.Message}");
                if (ex.InnerException != null)
                    Console.Error.WriteLine($"  Inner: {ex.InnerException.Message}");
                return 1;
            }
        }

        // ── Step 2/3: Register steps ──────────────────────────────
        Console.WriteLine();
        Console.WriteLine("Step 2/3: Register Steps");
        Console.WriteLine(new string('─', 40));
        if (steps.Count > 0)
        {
            var registrar = new StepRegistrar(client, Console.WriteLine);
            try
            {
                registrar.RegisterSteps(assemblyName!, steps, solutionName);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"ERROR registering steps: {ex.Message}");
                return 1;
            }
        }
        else
        {
            Console.WriteLine("  No plugin steps to register.");
        }

        // ── Step 3/3: Register Custom APIs ───────────────────────────
        Console.WriteLine();
        Console.WriteLine("Step 3/3: Register Custom APIs");
        Console.WriteLine(new string('─', 40));
        if (customApis.Count > 0)
        {
            var apiRegistrar = new CustomApiRegistrar(client, Console.WriteLine);
            try
            {
                apiRegistrar.RegisterCustomApis(assemblyName!, customApis, solutionName);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"ERROR registering Custom APIs: {ex.Message}");
                return 1;
            }
        }
        else
        {
            Console.WriteLine("  No Custom APIs to register.");
        }

        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════");
        Console.WriteLine("  ✓ Plugin deployment completed successfully!");
        Console.WriteLine("═══════════════════════════════════════════════════════");
        Console.WriteLine($"  Environment:  {client.ConnectedOrgFriendlyName}");
        Console.WriteLine($"  Package:      {assemblyName}");
        Console.WriteLine($"  Steps:        {steps.Count} checked & synced");
        Console.WriteLine($"  Custom APIs:  {customApis.Count} checked & synced");
        if (!string.IsNullOrEmpty(solutionName))
            Console.WriteLine($"  Solution:     {solutionName}");
        Console.WriteLine();
        Console.WriteLine("  ☕ Like this tool? Buy me a coffee:");
        Console.WriteLine("     https://buymeacoffee.com/rstickler.dev");
        Console.WriteLine();
        return 0;
    }

}
