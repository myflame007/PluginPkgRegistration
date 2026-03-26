using Dataverse.PluginRegistration;
using Microsoft.Xrm.Sdk;

namespace Dataverse.PluginRegistration.Tests;

public class CustomApiRegistrarTests
{
    // ═══════════════════════════════════════════════════════════════
    //  CustomApiHasChanges Tests
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void CustomApiHasChanges_AllSame_ReturnsFalse()
    {
        var pluginTypeId = Guid.NewGuid();
        var api = CreateApi();
        var existing = BuildExistingApiEntity(api, pluginTypeId);

        Assert.False(CustomApiRegistrar.CustomApiHasChanges(existing, api, pluginTypeId));
    }

    [Fact]
    public void CustomApiHasChanges_DisplayNameChanged_ReturnsTrue()
    {
        var pluginTypeId = Guid.NewGuid();
        var api = CreateApi();
        var existing = BuildExistingApiEntity(api, pluginTypeId);
        api.DisplayName = "New Display Name";

        Assert.True(CustomApiRegistrar.CustomApiHasChanges(existing, api, pluginTypeId));
    }

    [Fact]
    public void CustomApiHasChanges_DescriptionChanged_ReturnsTrue()
    {
        var pluginTypeId = Guid.NewGuid();
        var api = CreateApi();
        var existing = BuildExistingApiEntity(api, pluginTypeId);
        api.Description = "New Description";

        Assert.True(CustomApiRegistrar.CustomApiHasChanges(existing, api, pluginTypeId));
    }

    [Fact]
    public void CustomApiHasChanges_BindingTypeChanged_ReturnsTrue()
    {
        var pluginTypeId = Guid.NewGuid();
        var api = CreateApi();
        var existing = BuildExistingApiEntity(api, pluginTypeId);
        api.BindingType = 1; // Global → Entity

        Assert.True(CustomApiRegistrar.CustomApiHasChanges(existing, api, pluginTypeId));
    }

    [Fact]
    public void CustomApiHasChanges_IsFunctionChanged_ReturnsTrue()
    {
        var pluginTypeId = Guid.NewGuid();
        var api = CreateApi();
        var existing = BuildExistingApiEntity(api, pluginTypeId);
        api.IsFunction = true;

        Assert.True(CustomApiRegistrar.CustomApiHasChanges(existing, api, pluginTypeId));
    }

    [Fact]
    public void CustomApiHasChanges_PluginTypeChanged_ReturnsTrue()
    {
        var oldPluginTypeId = Guid.NewGuid();
        var newPluginTypeId = Guid.NewGuid();
        var api = CreateApi();
        var existing = BuildExistingApiEntity(api, oldPluginTypeId);

        Assert.True(CustomApiRegistrar.CustomApiHasChanges(existing, api, newPluginTypeId));
    }

    // ═══════════════════════════════════════════════════════════════
    //  ParameterHasChanges Tests
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void ParameterHasChanges_AllSame_ReturnsFalse()
    {
        var param = CreateParam();
        var existing = BuildExistingParamEntity(param, isRequest: true);

        Assert.False(CustomApiRegistrar.ParameterHasChanges(existing, param, isRequest: true));
    }

    [Fact]
    public void ParameterHasChanges_TypeChanged_ReturnsTrue()
    {
        var param = CreateParam();
        var existing = BuildExistingParamEntity(param, isRequest: true);
        param.Type = 7; // String → Integer

        Assert.True(CustomApiRegistrar.ParameterHasChanges(existing, param, isRequest: true));
    }

    [Fact]
    public void ParameterHasChanges_IsRequiredChanged_ReturnsTrue()
    {
        var param = CreateParam();
        var existing = BuildExistingParamEntity(param, isRequest: true);
        param.IsRequired = false; // was true

        Assert.True(CustomApiRegistrar.ParameterHasChanges(existing, param, isRequest: true));
    }

    [Fact]
    public void ParameterHasChanges_DescriptionChanged_ReturnsTrue()
    {
        var param = CreateParam();
        var existing = BuildExistingParamEntity(param, isRequest: true);
        param.Description = "New Desc";

        Assert.True(CustomApiRegistrar.ParameterHasChanges(existing, param, isRequest: true));
    }

    [Fact]
    public void ParameterHasChanges_ResponseIgnoresIsRequired_ReturnsFalse()
    {
        var param = CreateParam();
        var existing = BuildExistingParamEntity(param, isRequest: false);
        // isoptional is not checked for response properties
        existing["isoptional"] = true; // different from !param.IsRequired

        Assert.False(CustomApiRegistrar.ParameterHasChanges(existing, param, isRequest: false));
    }

    // ═══════════════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════════════

    private static CustomApiInfo CreateApi() => new()
    {
        UniqueName = "test_myapi",
        PluginTypeName = "MyPlugin.CustomApiHandler",
        DisplayName = "My API",
        Description = "Test API",
        BindingType = 0,
        IsFunction = false,
        IsPrivate = false,
        AllowedProcessingStepType = 2
    };

    private static CustomApiParameterInfo CreateParam() => new()
    {
        UniqueName = "TestParam",
        Type = 10, // String
        IsRequired = true,
        DisplayName = "Test Param",
        Description = "A test parameter"
    };

    private static Entity BuildExistingApiEntity(CustomApiInfo api, Guid pluginTypeId)
    {
        var displayName = !string.IsNullOrEmpty(api.DisplayName) ? api.DisplayName : api.UniqueName;
        return new Entity("customapi", Guid.NewGuid())
        {
            ["displayname"] = displayName,
            ["description"] = api.Description ?? "",
            ["bindingtype"] = new OptionSetValue(api.BindingType),
            ["isfunction"] = api.IsFunction,
            ["isprivate"] = api.IsPrivate,
            ["allowedcustomprocessingsteptype"] = new OptionSetValue(api.AllowedProcessingStepType),
            ["boundentitylogicalname"] = api.BoundEntity ?? "",
            ["plugintypeid"] = new EntityReference("plugintype", pluginTypeId)
        };
    }

    private static Entity BuildExistingParamEntity(CustomApiParameterInfo param, bool isRequest)
    {
        var displayName = !string.IsNullOrEmpty(param.DisplayName) ? param.DisplayName : param.UniqueName;
        var entity = new Entity(isRequest ? "customapirequestparameter" : "customapiresponseproperty", Guid.NewGuid())
        {
            ["displayname"] = displayName,
            ["description"] = param.Description ?? "",
            ["type"] = new OptionSetValue(param.Type),
            ["logicalentityname"] = param.LogicalEntityName ?? ""
        };

        if (isRequest)
            entity["isoptional"] = !param.IsRequired;

        return entity;
    }
}
