namespace Dataverse.PluginRegistration;

// ═════════════════════════════════════════════════════════════════════
//  Custom API Attribute Definitions
//
//  These are the TOOL-SIDE copies used for documentation and reference.
//  Plugin developers need their OWN copies in their plugin project
//  (copy-paste or via NuGet Dataverse.PluginRegistration.Attributes).
//
//  The AttributeReader matches on NAME only (MetadataLoadContext),
//  so the plugin project does NOT need to reference this assembly.
// ═════════════════════════════════════════════════════════════════════

// ─── Enums (must match what the developer uses) ──────────────────

public enum CustomApiBindingType { Global = 0, Entity = 1, EntityCollection = 2 }
public enum CustomApiProcessingStepType { None = 0, AsyncOnly = 1, SyncAndAsync = 2 }

public enum CustomApiParameterType
{
    Boolean = 0, DateTime = 1, Decimal = 2, Entity = 3, EntityCollection = 4,
    EntityReference = 5, Float = 6, Integer = 7, Money = 8, Picklist = 9,
    String = 10, StringArray = 11, Guid = 12
}

// ─── CustomApiDefinitionAttribute ────────────────────────────────

/// <summary>
/// Marks a plugin class as a Custom API backend.
/// Used together with [CrmPluginRegistration("message_name")].
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class CustomApiDefinitionAttribute : Attribute
{
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public CustomApiBindingType BindingType { get; set; } = CustomApiBindingType.Global;
    public string BoundEntity { get; set; } = "";
    public bool IsFunction { get; set; } = false;
    public bool IsPrivate { get; set; } = false;
    public CustomApiProcessingStepType AllowedProcessingStepType { get; set; }
        = CustomApiProcessingStepType.SyncAndAsync;
    public string ExecutePrivilegeName { get; set; } = "";
}

// ─── CustomApiRequestParameterAttribute ──────────────────────────

/// <summary>
/// Defines a request (input) parameter for a Custom API.
/// Multiple allowed per class — one attribute per parameter.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class CustomApiRequestParameterAttribute : Attribute
{
    public string UniqueName { get; }
    public CustomApiParameterType Type { get; }
    public bool IsRequired { get; set; } = true;
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public string LogicalEntityName { get; set; } = "";

    public CustomApiRequestParameterAttribute(string uniqueName, CustomApiParameterType type)
    {
        UniqueName = uniqueName;
        Type = type;
    }
}

// ─── CustomApiResponsePropertyAttribute ──────────────────────────

/// <summary>
/// Defines a response (output) property for a Custom API.
/// Multiple allowed per class — one attribute per property.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class CustomApiResponsePropertyAttribute : Attribute
{
    public string UniqueName { get; }
    public CustomApiParameterType Type { get; }
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public string LogicalEntityName { get; set; } = "";

    public CustomApiResponsePropertyAttribute(string uniqueName, CustomApiParameterType type)
    {
        UniqueName = uniqueName;
        Type = type;
    }
}
