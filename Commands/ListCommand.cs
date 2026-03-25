namespace Dataverse.PluginRegistration;

internal static class ListCommand
{
    internal static int Run(string[] args, Dictionary<string, string> envVars)
    {
        var (assemblyPath, _, _, _, _, _, _, error) = ArgsResolver.ResolveArgs(args, requireConnection: false, envVars);
        if (error != null) { Console.Error.WriteLine(error); return 1; }

        Console.WriteLine($"Reading: {assemblyPath}");
        var steps = AttributeReader.ReadFromAssembly(assemblyPath!);
        var customApis = AttributeReader.ReadCustomApisFromAssembly(assemblyPath!, Console.WriteLine);

        if (steps.Count == 0 && customApis.Count == 0)
        {
            Console.WriteLine("No plugin step or Custom API registrations found.");
            return 0;
        }

        if (steps.Count > 0)
        {
            Console.WriteLine($"\n  Steps:");
            Console.WriteLine($"  {"Plugin Type",-55} {"Message",-15} {"Entity",-20} {"Stage",-6} {"Mode",-6}");
            Console.WriteLine($"  {new string('─', 108)}");
            foreach (var s in steps)
            {
                var stage = s.Stage switch { 10 => "PreVal", 20 => "PreOp", 40 => "PostOp", _ => s.Stage.ToString() };
                var mode = s.ExecutionMode == 0 ? "Sync" : "Async";
                Console.WriteLine($"  {Truncate(s.PluginTypeName, 54),-55} {s.Message,-15} {s.EntityLogicalName ?? "(all)",-20} {stage,-6} {mode,-6}");

                if (s.Image1Type >= 0)
                    Console.WriteLine($"    └─ Image1: {s.Image1Name} (type={s.Image1Type}, attrs={s.Image1Attributes})");
                if (s.Image2Type >= 0)
                    Console.WriteLine($"    └─ Image2: {s.Image2Name} (type={s.Image2Type}, attrs={s.Image2Attributes})");
            }
        }

        if (customApis.Count > 0)
        {
            Console.WriteLine($"\n  Custom APIs:");
            foreach (var api in customApis)
            {
                var binding = api.BindingType switch
                {
                    0 => "Global",
                    1 => $"Entity-bound: {api.BoundEntity}",
                    2 => $"EntityCollection-bound: {api.BoundEntity}",
                    _ => $"BindingType={api.BindingType}"
                };
                var kind = api.IsFunction ? "Function" : "Action";
                Console.WriteLine($"    {api.UniqueName}  [{kind}, {binding}]");

                foreach (var p in api.RequestParameters)
                {
                    var req = p.IsRequired ? "required" : "optional";
                    Console.WriteLine($"      Request:  {p.UniqueName} ({MapParameterTypeName(p.Type)}, {req})");
                }
                foreach (var p in api.ResponseProperties)
                {
                    Console.WriteLine($"      Response: {p.UniqueName} ({MapParameterTypeName(p.Type)})");
                }
            }
        }

        Console.WriteLine($"\nTotal: {steps.Count} step(s), {customApis.Count} Custom API(s)");
        return 0;
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..(max - 2)] + "..";

    private static string MapParameterTypeName(int type) => type switch
    {
        0 => "Boolean", 1 => "DateTime", 2 => "Decimal", 3 => "Entity",
        4 => "EntityCollection", 5 => "EntityReference", 6 => "Float",
        7 => "Integer", 8 => "Money", 9 => "Picklist", 10 => "String",
        11 => "StringArray", 12 => "Guid", _ => $"Type({type})"
    };
}
