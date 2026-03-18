using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;

namespace Dataverse.PluginRegistration;

/// <summary>
/// Reads CrmPluginRegistrationAttribute from compiled plugin assemblies via reflection.
/// Works with any project that uses the standard CrmPluginRegistrationAttribute.
/// </summary>
public static class AttributeReader
{
    public static List<PluginStepInfo> ReadFromAssembly(string assemblyPath)
    {
        var steps = new List<PluginStepInfo>();
        var dir = Path.GetDirectoryName(Path.GetFullPath(assemblyPath))!;

        // Load into a separate context so we can read types without locking
        var runtimeDir = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();
        var resolver = new PathAssemblyResolver(
            Directory.GetFiles(dir, "*.dll")
                .Concat(Directory.GetFiles(runtimeDir, "*.dll")));

        using var mlc = new MetadataLoadContext(resolver);
        var assembly = mlc.LoadFromAssemblyPath(Path.GetFullPath(assemblyPath));

        foreach (var type in assembly.GetTypes())
        {
            var attrs = type.GetCustomAttributesData()
                .Where(a => a.AttributeType.Name == "CrmPluginRegistrationAttribute")
                .ToList();

            foreach (var attr in attrs)
            {
                var step = ParseAttribute(type.FullName ?? type.Name, attr);
                if (step != null)
                    steps.Add(step);
            }
        }

        return steps;
    }

    private static PluginStepInfo? ParseAttribute(string pluginTypeName, CustomAttributeData attr)
    {
        var args = attr.ConstructorArguments;
        if (args.Count == 0) return null;

        // Detect which constructor was used by argument count
        string message;
        string? entity = null;
        int stage = 40; // PostOperation default
        int execMode = 1; // Synchronous default
        string? filterAttrs = null;
        string? stepName = null;
        int execOrder = 1;
        int isolation = 1; // Sandbox

        if (args.Count == 5)
        {
            // Workflow activity constructor: (name, friendlyName, description, groupName, isolationMode)
            // Skip workflow registrations for now
            return null;
        }
        else if (args.Count == 1)
        {
            // Custom API constructor: (message)
            message = args[0].Value?.ToString() ?? "";
            stepName = message;
        }
        else if (args.Count == 8)
        {
            // Plugin constructor: (message, entity, stage, execMode, filterAttrs, stepName, execOrder, isolation)
            var firstArg = args[0];

            // Could be string or MessageNameEnum (int)
            if (firstArg.ArgumentType.IsEnum)
                message = firstArg.ArgumentType.GetEnumName(firstArg.Value!) ?? firstArg.Value!.ToString()!;
            else
                message = firstArg.Value?.ToString() ?? "";

            entity = args[1].Value?.ToString();
            stage = Convert.ToInt32(args[2].Value);
            execMode = Convert.ToInt32(args[3].Value);
            filterAttrs = args[4].Value?.ToString();
            stepName = args[5].Value?.ToString();
            execOrder = Convert.ToInt32(args[6].Value);
            isolation = Convert.ToInt32(args[7].Value);
        }
        else
        {
            return null;
        }

        var step = new PluginStepInfo
        {
            PluginTypeName = pluginTypeName,
            Message = message,
            EntityLogicalName = string.IsNullOrWhiteSpace(entity) ? null : entity,
            Stage = MapStage(stage),
            ExecutionMode = MapExecMode(execMode),
            FilteringAttributes = string.IsNullOrWhiteSpace(filterAttrs) ? null : filterAttrs,
            Name = stepName ?? $"{pluginTypeName}: {message}",
            ExecutionOrder = execOrder,
            IsolationMode = MapIsolation(isolation)
        };

        // Read named properties
        foreach (var named in attr.NamedArguments)
        {
            switch (named.MemberName)
            {
                case "Image1Type":
                    step.Image1Type = Convert.ToInt32(named.TypedValue.Value);
                    break;
                case "Image1Name":
                    step.Image1Name = named.TypedValue.Value?.ToString();
                    break;
                case "Image1Attributes":
                    step.Image1Attributes = named.TypedValue.Value?.ToString();
                    break;
                case "Image2Type":
                    step.Image2Type = Convert.ToInt32(named.TypedValue.Value);
                    break;
                case "Image2Name":
                    step.Image2Name = named.TypedValue.Value?.ToString();
                    break;
                case "Image2Attributes":
                    step.Image2Attributes = named.TypedValue.Value?.ToString();
                    break;
                case "Description":
                    step.Description = named.TypedValue.Value?.ToString();
                    break;
                case "UnSecureConfiguration":
                    step.UnSecureConfiguration = named.TypedValue.Value?.ToString();
                    break;
                case "SecureConfiguration":
                    step.SecureConfiguration = named.TypedValue.Value?.ToString();
                    break;
                case "DeleteAsyncOperation":
                    step.DeleteAsyncOperation = Convert.ToBoolean(named.TypedValue.Value);
                    break;
            }
        }

        return step;
    }

    /// <summary>Maps StageEnum values to Dataverse SdkMessageProcessingStep stage values.</summary>
    private static int MapStage(int stageEnumValue) => stageEnumValue switch
    {
        // StageEnum: PreValidation=10, PreOperation=20, PostOperation=40
        // Dataverse: PreValidation=10, PreOperation=20, MainOperation=30, PostOperation=40
        10 or 20 or 40 => stageEnumValue,
        // If enum ordinal was stored (0=PreValidation, 1=PreOperation, 2=PostOperation)
        0 => 10,
        1 => 20,
        2 => 40,
        _ => stageEnumValue
    };

    /// <summary>Maps ExecutionModeEnum to Dataverse values.</summary>
    private static int MapExecMode(int enumValue) => enumValue switch
    {
        // ExecutionModeEnum: Asynchronous=0, Synchronous=1
        // Dataverse: Synchronous=0, Asynchronous=1 (reversed!)
        0 => 1, // Asynchronous enum -> Dataverse async (1)
        1 => 0, // Synchronous enum -> Dataverse sync (0)
        _ => enumValue
    };

    /// <summary>Maps IsolationModeEnum to Dataverse values.</summary>
    private static int MapIsolation(int enumValue) => enumValue switch
    {
        // IsolationModeEnum: None=0, Sandbox=1 -> Dataverse: None=1, Sandbox=2
        0 => 1,
        1 => 2,
        _ => enumValue
    };
}
