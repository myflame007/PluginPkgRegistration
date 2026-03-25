namespace Dataverse.PluginRegistration;

/// <summary>
/// Represents a Custom API registration extracted from CustomApiDefinitionAttribute.
/// Used together with CrmPluginRegistrationAttribute (1-arg constructor).
/// </summary>
public class CustomApiInfo
{
    /// <summary>The unique name of the Custom API (from CrmPluginRegistration message arg).</summary>
    public required string UniqueName { get; set; }

    /// <summary>Full type name of the plugin class that backs this Custom API.</summary>
    public required string PluginTypeName { get; set; }

    // ── From CustomApiDefinitionAttribute ────────────────────────

    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";

    /// <summary>0=Global, 1=Entity, 2=EntityCollection</summary>
    public int BindingType { get; set; }

    /// <summary>Only when BindingType is Entity or EntityCollection.</summary>
    public string? BoundEntity { get; set; }

    /// <summary>false = Action (POST), true = Function (GET).</summary>
    public bool IsFunction { get; set; }

    public bool IsPrivate { get; set; }

    /// <summary>0=None, 1=AsyncOnly, 2=SyncAndAsync</summary>
    public int AllowedProcessingStepType { get; set; } = 2;

    public string? ExecutePrivilegeName { get; set; }

    // ── Parameters ───────────────────────────────────────────────

    public List<CustomApiParameterInfo> RequestParameters { get; set; } = [];
    public List<CustomApiParameterInfo> ResponseProperties { get; set; } = [];
}

/// <summary>
/// Represents a single request parameter or response property on a Custom API.
/// </summary>
public class CustomApiParameterInfo
{
    public required string UniqueName { get; set; }

    /// <summary>
    /// Dataverse CustomApiParameterType:
    /// Boolean=0, DateTime=1, Decimal=2, Entity=3, EntityCollection=4,
    /// EntityReference=5, Float=6, Integer=7, Money=8, Picklist=9,
    /// String=10, StringArray=11, Guid=12
    /// </summary>
    public int Type { get; set; }

    /// <summary>Only relevant for request parameters (ignored on response).</summary>
    public bool IsRequired { get; set; } = true;

    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";

    /// <summary>Only for Entity/EntityReference/EntityCollection parameter types.</summary>
    public string? LogicalEntityName { get; set; }
}
