using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Dataverse.PluginRegistration;

/// <summary>
/// Registers/updates plugin steps and images in Dataverse,
/// similar to what spkl does for ILMerge-based plugins.
/// Supports both classic PluginAssembly and NuGet-based PluginPackage deployments.
/// </summary>
public class StepRegistrar
{
    private readonly IOrganizationService _svc;
    private readonly Action<string> _log;

    public StepRegistrar(IOrganizationService service, Action<string> log)
    {
        _svc = service;
        _log = log;
    }

    /// <summary>
    /// Registers all steps for the given plugin assembly.
    /// Finds the PluginAssembly or PluginPackage by name, then upserts steps + images.
    /// </summary>
    public void RegisterSteps(string assemblyName, List<PluginStepInfo> steps)
    {
        // 1. Find plugin types that are already registered
        var pluginTypes = FindPluginTypes(assemblyName);

        if (pluginTypes.Count == 0)
        {
            _log($"ERROR: No registered PluginTypes found for assembly '{assemblyName}'.");
            _log("Make sure the assembly/package is already deployed (e.g. via 'pac plugin push').");
            return;
        }

        _log($"Found {pluginTypes.Count} registered PluginType(s) for '{assemblyName}'.");

        // 2. Cache SdkMessage + SdkMessageFilter lookups
        var messageCache = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var filterCache = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        foreach (var step in steps)
        {
            var typeFullName = step.PluginTypeName;
            if (!pluginTypes.TryGetValue(typeFullName, out var pluginTypeId))
            {
                _log($"  SKIP: PluginType '{typeFullName}' not found in Dataverse. Skipping step '{step.Name}'.");
                continue;
            }

            // Resolve SdkMessage
            var messageId = ResolveMessage(step.Message, messageCache);
            if (messageId == Guid.Empty)
            {
                _log($"  SKIP: SdkMessage '{step.Message}' not found. Skipping step '{step.Name}'.");
                continue;
            }

            // Resolve SdkMessageFilter (optional, depends on entity)
            Guid? filterId = null;
            if (!string.IsNullOrEmpty(step.EntityLogicalName))
            {
                filterId = ResolveFilter(messageId, step.EntityLogicalName, filterCache);
                if (filterId == null)
                {
                    _log($"  WARN: No SdkMessageFilter for '{step.Message}' on '{step.EntityLogicalName}'. Registering without filter.");
                }
            }

            // 3. Upsert the step
            var stepId = UpsertStep(pluginTypeId, messageId, filterId, step);

            // 4. Upsert images (skip invalid message/image combinations)
            if (step.Image1Type >= 0 && !string.IsNullOrEmpty(step.Image1Name))
            {
                if (IsValidImageType(step.Message, step.Image1Type))
                    UpsertImage(stepId, step.Image1Name, step.Image1Type, step.Image1Attributes, step.Message);
                else
                    _log($"    SKIP image '{step.Image1Name}': {step.Message} does not support image type {step.Image1Type} (0=Pre, 1=Post, 2=Both)");
            }

            if (step.Image2Type >= 0 && !string.IsNullOrEmpty(step.Image2Name))
            {
                if (IsValidImageType(step.Message, step.Image2Type))
                    UpsertImage(stepId, step.Image2Name, step.Image2Type, step.Image2Attributes, step.Message);
                else
                    _log($"    SKIP image '{step.Image2Name}': {step.Message} does not support image type {step.Image2Type} (0=Pre, 1=Post, 2=Both)");
            }
        }
    }

    /// <summary>Finds all PluginType records for a given assembly name.</summary>
    private Dictionary<string, Guid> FindPluginTypes(string assemblyName)
    {
        var result = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        // Try PluginAssembly first
        var assemblyQuery = new QueryExpression("pluginassembly")
        {
            ColumnSet = new ColumnSet("pluginassemblyid"),
            Criteria = { Conditions = { new ConditionExpression("name", ConditionOperator.Equal, assemblyName) } }
        };

        var assemblies = _svc.RetrieveMultiple(assemblyQuery);
        Guid? assemblyId = assemblies.Entities.FirstOrDefault()?.Id;

        // Also try PluginPackage (NuGet-based)
        Guid? packageId = null;
        try
        {
            var packageQuery = new QueryExpression("pluginpackage")
            {
                ColumnSet = new ColumnSet("pluginpackageid"),
                Criteria = { Conditions = { new ConditionExpression("name", ConditionOperator.Equal, assemblyName) } }
            };
            var packages = _svc.RetrieveMultiple(packageQuery);
            packageId = packages.Entities.FirstOrDefault()?.Id;
        }
        catch
        {
            // PluginPackage entity might not exist in older environments
        }

        // Query PluginType by assembly or package
        var typeQuery = new QueryExpression("plugintype")
        {
            ColumnSet = new ColumnSet("typename", "plugintypeid")
        };

        if (assemblyId.HasValue && packageId.HasValue)
        {
            var orFilter = new FilterExpression(LogicalOperator.Or);
            orFilter.AddCondition("pluginassemblyid", ConditionOperator.Equal, assemblyId.Value);
            orFilter.AddCondition("pluginpackageid", ConditionOperator.Equal, packageId.Value);
            typeQuery.Criteria.AddFilter(orFilter);
        }
        else if (assemblyId.HasValue)
        {
            typeQuery.Criteria.AddCondition("pluginassemblyid", ConditionOperator.Equal, assemblyId.Value);
        }
        else if (packageId.HasValue)
        {
            typeQuery.Criteria.AddCondition("pluginpackageid", ConditionOperator.Equal, packageId.Value);
        }
        else
        {
            return result;
        }

        var types = _svc.RetrieveMultiple(typeQuery);
        foreach (var t in types.Entities)
        {
            var typeName = t.GetAttributeValue<string>("typename");
            if (!string.IsNullOrEmpty(typeName))
                result[typeName] = t.Id;
        }

        return result;
    }

    private Guid ResolveMessage(string messageName, Dictionary<string, Guid> cache)
    {
        if (cache.TryGetValue(messageName, out var cached)) return cached;

        var query = new QueryExpression("sdkmessage")
        {
            ColumnSet = new ColumnSet("sdkmessageid"),
            Criteria = { Conditions = { new ConditionExpression("name", ConditionOperator.Equal, messageName) } }
        };

        var msg = _svc.RetrieveMultiple(query).Entities.FirstOrDefault();
        var id = msg?.Id ?? Guid.Empty;
        cache[messageName] = id;
        return id;
    }

    private Guid? ResolveFilter(Guid messageId, string entityLogicalName, Dictionary<string, Guid> cache)
    {
        var key = $"{messageId}|{entityLogicalName}";
        if (cache.TryGetValue(key, out var cached)) return cached;

        var query = new QueryExpression("sdkmessagefilter")
        {
            ColumnSet = new ColumnSet("sdkmessagefilterid"),
            Criteria =
            {
                Conditions =
                {
                    new ConditionExpression("sdkmessageid", ConditionOperator.Equal, messageId),
                    new ConditionExpression("primaryobjecttypecode", ConditionOperator.Equal, entityLogicalName)
                }
            }
        };

        var filter = _svc.RetrieveMultiple(query).Entities.FirstOrDefault();
        if (filter != null)
        {
            cache[key] = filter.Id;
            return filter.Id;
        }

        return null;
    }

    /// <summary>Creates or updates an SdkMessageProcessingStep.</summary>
    private Guid UpsertStep(Guid pluginTypeId, Guid messageId, Guid? filterId, PluginStepInfo step)
    {
        var existing = FindExistingStep(pluginTypeId, step.Name);

        var entity = new Entity("sdkmessageprocessingstep")
        {
            ["name"] = step.Name,
            ["plugintypeid"] = new EntityReference("plugintype", pluginTypeId),
            ["sdkmessageid"] = new EntityReference("sdkmessage", messageId),
            ["stage"] = new OptionSetValue(step.Stage),
            ["mode"] = new OptionSetValue(step.ExecutionMode),
            ["rank"] = step.ExecutionOrder,
            ["supporteddeployment"] = new OptionSetValue(0), // ServerOnly
            ["asyncautodelete"] = step.DeleteAsyncOperation,
            ["statecode"] = new OptionSetValue(0),      // Enabled
            ["statuscode"] = new OptionSetValue(1)       // Active
        };

        if (filterId.HasValue)
            entity["sdkmessagefilterid"] = new EntityReference("sdkmessagefilter", filterId.Value);

        if (!string.IsNullOrEmpty(step.FilteringAttributes))
            entity["filteringattributes"] = step.FilteringAttributes;

        if (!string.IsNullOrEmpty(step.Description))
            entity["description"] = step.Description;

        if (!string.IsNullOrEmpty(step.UnSecureConfiguration))
            entity["configuration"] = step.UnSecureConfiguration;

        if (existing != null)
        {
            if (!StepHasChanges(existing, step, messageId, filterId))
            {
                _log($"  UNCHANGED step: {step.Name}");
                return existing.Id;
            }

            entity.Id = existing.Id;
            _svc.Update(entity);
            _log($"  UPDATED step: {step.Name}");
            return existing.Id;
        }
        else
        {
            var id = _svc.Create(entity);
            _log($"  CREATED step: {step.Name}");
            return id;
        }
    }

    /// <summary>Compares existing step attributes with desired values to detect changes.</summary>
    private static bool StepHasChanges(Entity existing, PluginStepInfo step, Guid messageId, Guid? filterId)
    {
        if (existing.GetAttributeValue<OptionSetValue>("stage")?.Value != step.Stage) return true;
        if (existing.GetAttributeValue<OptionSetValue>("mode")?.Value != step.ExecutionMode) return true;
        if (existing.GetAttributeValue<int>("rank") != step.ExecutionOrder) return true;
        if (existing.GetAttributeValue<bool>("asyncautodelete") != step.DeleteAsyncOperation) return true;
        if ((existing.GetAttributeValue<string>("filteringattributes") ?? "") != (step.FilteringAttributes ?? "")) return true;
        if ((existing.GetAttributeValue<string>("description") ?? "") != (step.Description ?? "")) return true;
        if ((existing.GetAttributeValue<string>("configuration") ?? "") != (step.UnSecureConfiguration ?? "")) return true;

        var existingMsg = existing.GetAttributeValue<EntityReference>("sdkmessageid")?.Id;
        if (existingMsg != messageId) return true;

        var existingFilter = existing.GetAttributeValue<EntityReference>("sdkmessagefilterid")?.Id;
        if (existingFilter != filterId) return true;

        return false;
    }

    private Entity? FindExistingStep(Guid pluginTypeId, string stepName)
    {
        var query = new QueryExpression("sdkmessageprocessingstep")
        {
            ColumnSet = new ColumnSet(
                "sdkmessageprocessingstepid", "stage", "mode", "rank",
                "filteringattributes", "description", "configuration",
                "asyncautodelete", "sdkmessageid", "sdkmessagefilterid"),
            Criteria =
            {
                Conditions =
                {
                    new ConditionExpression("plugintypeid", ConditionOperator.Equal, pluginTypeId),
                    new ConditionExpression("name", ConditionOperator.Equal, stepName)
                }
            }
        };

        return _svc.RetrieveMultiple(query).Entities.FirstOrDefault();
    }

    /// <summary>Creates or updates an SdkMessageProcessingStepImage.</summary>
    private void UpsertImage(Guid stepId, string imageName, int imageType, string? attributes, string message)
    {
        var existing = FindExistingImage(stepId, imageName);

        var entity = new Entity("sdkmessageprocessingstepimage")
        {
            ["sdkmessageprocessingstepid"] = new EntityReference("sdkmessageprocessingstep", stepId),
            ["name"] = imageName,
            ["entityalias"] = imageName,
            ["imagetype"] = new OptionSetValue(imageType),
            ["messagepropertyname"] = GetMessagePropertyName(message)
        };

        if (!string.IsNullOrEmpty(attributes))
            entity["attributes"] = attributes;

        if (existing != null)
        {
            if (!ImageHasChanges(existing, imageType, attributes))
            {
                _log($"    UNCHANGED image: {imageName} (type={imageType})");
                return;
            }

            entity.Id = existing.Id;
            _svc.Update(entity);
            _log($"    UPDATED image: {imageName} (type={imageType})");
        }
        else
        {
            _svc.Create(entity);
            _log($"    CREATED image: {imageName} (type={imageType})");
        }
    }

    /// <summary>Compares existing image attributes with desired values to detect changes.</summary>
    private static bool ImageHasChanges(Entity existing, int imageType, string? attributes)
    {
        if (existing.GetAttributeValue<OptionSetValue>("imagetype")?.Value != imageType) return true;
        if ((existing.GetAttributeValue<string>("attributes") ?? "") != (attributes ?? "")) return true;
        return false;
    }

    private Entity? FindExistingImage(Guid stepId, string imageName)
    {
        var query = new QueryExpression("sdkmessageprocessingstepimage")
        {
            ColumnSet = new ColumnSet(
                "sdkmessageprocessingstepimageid", "imagetype", "attributes"),
            Criteria =
            {
                Conditions =
                {
                    new ConditionExpression("sdkmessageprocessingstepid", ConditionOperator.Equal, stepId),
                    new ConditionExpression("name", ConditionOperator.Equal, imageName)
                }
            }
        };

        return _svc.RetrieveMultiple(query).Entities.FirstOrDefault();
    }

    /// <summary>Maps message names to their MessagePropertyName for images.</summary>
    private static string GetMessagePropertyName(string message) => message.ToUpperInvariant() switch
    {
        "CREATE" => "Id",
        "UPDATE" => "Target",
        "DELETE" => "Target",
        "SETSTATE" or "SETSTATEDYNAMICENTITY" => "EntityMoniker",
        "ASSIGN" => "Target",
        "MERGE" => "Target",
        "RETRIEVE" => "Target",
        "RETRIEVEMULTIPLE" => "Query",
        _ => "Target"
    };

    /// <summary>
    /// Validates whether an image type is supported for the given message.
    /// Create only supports PostImage, Delete only PreImage.
    /// </summary>
    private static bool IsValidImageType(string message, int imageType) => message.ToUpperInvariant() switch
    {
        // 0=PreImage, 1=PostImage, 2=Both
        "CREATE" => imageType == 1,              // Only PostImage
        "DELETE" => imageType == 0,              // Only PreImage
        _ => true                                // Update, SetState, etc. support all types
    };
}
