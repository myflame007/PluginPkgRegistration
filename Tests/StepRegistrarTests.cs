using Dataverse.PluginRegistration;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using NSubstitute;

namespace Dataverse.PluginRegistration.Tests;

public class StepRegistrarTests
{
    // ═══════════════════════════════════════════════════════════════
    //  BuildStepEntity Tests
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void BuildStepEntity_SetsAllRequiredFields()
    {
        var pluginTypeId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var filterId = Guid.NewGuid();
        var step = CreateStep(stage: 40, execMode: 0, isolationMode: 2, execOrder: 5);

        var entity = StepRegistrar.BuildStepEntity(pluginTypeId, messageId, filterId, step);

        Assert.Equal(step.Name, entity["name"]);
        Assert.Equal(40, ((OptionSetValue)entity["stage"]).Value);
        Assert.Equal(0, ((OptionSetValue)entity["mode"]).Value);
        Assert.Equal(2, ((OptionSetValue)entity["isolationmode"]).Value);
        Assert.Equal(5, entity["rank"]);
        Assert.Equal(pluginTypeId, ((EntityReference)entity["plugintypeid"]).Id);
        Assert.Equal(messageId, ((EntityReference)entity["sdkmessageid"]).Id);
        Assert.Equal(filterId, ((EntityReference)entity["sdkmessagefilterid"]).Id);
    }

    [Fact]
    public void BuildStepEntity_IsolationMode_Sandbox_SetsCorrectValue()
    {
        var step = CreateStep(isolationMode: 2);
        var entity = StepRegistrar.BuildStepEntity(Guid.NewGuid(), Guid.NewGuid(), null, step);

        Assert.Equal(2, ((OptionSetValue)entity["isolationmode"]).Value);
    }

    [Fact]
    public void BuildStepEntity_IsolationMode_None_SetsCorrectValue()
    {
        var step = CreateStep(isolationMode: 1);
        var entity = StepRegistrar.BuildStepEntity(Guid.NewGuid(), Guid.NewGuid(), null, step);

        Assert.Equal(1, ((OptionSetValue)entity["isolationmode"]).Value);
    }

    [Fact]
    public void BuildStepEntity_NoFilter_OmitsFilterReference()
    {
        var step = CreateStep();
        var entity = StepRegistrar.BuildStepEntity(Guid.NewGuid(), Guid.NewGuid(), null, step);

        Assert.False(entity.Contains("sdkmessagefilterid"));
    }

    [Fact]
    public void BuildStepEntity_WithFilteringAttributes_SetsAttribute()
    {
        var step = CreateStep();
        step.FilteringAttributes = "name,statuscode";
        var entity = StepRegistrar.BuildStepEntity(Guid.NewGuid(), Guid.NewGuid(), null, step);

        Assert.Equal("name,statuscode", entity["filteringattributes"]);
    }

    [Fact]
    public void BuildStepEntity_WithDescription_SetsAttribute()
    {
        var step = CreateStep();
        step.Description = "Test description";
        var entity = StepRegistrar.BuildStepEntity(Guid.NewGuid(), Guid.NewGuid(), null, step);

        Assert.Equal("Test description", entity["description"]);
    }

    [Fact]
    public void BuildStepEntity_WithUnSecureConfig_SetsAttribute()
    {
        var step = CreateStep();
        step.UnSecureConfiguration = "some config";
        var entity = StepRegistrar.BuildStepEntity(Guid.NewGuid(), Guid.NewGuid(), null, step);

        Assert.Equal("some config", entity["configuration"]);
    }

    // ═══════════════════════════════════════════════════════════════
    //  StepHasChanges Tests
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void StepHasChanges_AllFieldsSame_ReturnsFalse()
    {
        var messageId = Guid.NewGuid();
        var filterId = Guid.NewGuid();
        var step = CreateStep(stage: 40, execMode: 0, isolationMode: 2, execOrder: 1);
        step.FilteringAttributes = "name";
        step.Description = "desc";
        step.UnSecureConfiguration = "config";

        var existing = BuildExistingStepEntity(step, messageId, filterId);

        Assert.False(StepRegistrar.StepHasChanges(existing, step, messageId, filterId));
    }

    [Fact]
    public void StepHasChanges_StageChanged_ReturnsTrue()
    {
        var messageId = Guid.NewGuid();
        var step = CreateStep(stage: 40);
        var existing = BuildExistingStepEntity(step, messageId, null);
        step.Stage = 20; // Change: PostOperation -> PreOperation

        Assert.True(StepRegistrar.StepHasChanges(existing, step, messageId, null));
    }

    [Fact]
    public void StepHasChanges_IsolationModeChanged_ReturnsTrue()
    {
        var messageId = Guid.NewGuid();
        var step = CreateStep(isolationMode: 2);
        var existing = BuildExistingStepEntity(step, messageId, null);
        step.IsolationMode = 1; // Change: Sandbox -> None

        Assert.True(StepRegistrar.StepHasChanges(existing, step, messageId, null));
    }

    [Fact]
    public void StepHasChanges_ExecutionModeChanged_ReturnsTrue()
    {
        var messageId = Guid.NewGuid();
        var step = CreateStep(execMode: 0);
        var existing = BuildExistingStepEntity(step, messageId, null);
        step.ExecutionMode = 1; // Change: Sync -> Async

        Assert.True(StepRegistrar.StepHasChanges(existing, step, messageId, null));
    }

    [Fact]
    public void StepHasChanges_FilteringAttributesChanged_ReturnsTrue()
    {
        var messageId = Guid.NewGuid();
        var step = CreateStep();
        step.FilteringAttributes = "name";
        var existing = BuildExistingStepEntity(step, messageId, null);
        step.FilteringAttributes = "name,statuscode";

        Assert.True(StepRegistrar.StepHasChanges(existing, step, messageId, null));
    }

    [Fact]
    public void StepHasChanges_MessageChanged_ReturnsTrue()
    {
        var oldMsgId = Guid.NewGuid();
        var newMsgId = Guid.NewGuid();
        var step = CreateStep();
        var existing = BuildExistingStepEntity(step, oldMsgId, null);

        Assert.True(StepRegistrar.StepHasChanges(existing, step, newMsgId, null));
    }

    [Fact]
    public void StepHasChanges_ExecutionOrderChanged_ReturnsTrue()
    {
        var messageId = Guid.NewGuid();
        var step = CreateStep(execOrder: 1);
        var existing = BuildExistingStepEntity(step, messageId, null);
        step.ExecutionOrder = 5;

        Assert.True(StepRegistrar.StepHasChanges(existing, step, messageId, null));
    }

    // ═══════════════════════════════════════════════════════════════
    //  ImageHasChanges Tests
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void ImageHasChanges_AllSame_ReturnsFalse()
    {
        var existing = new Entity("sdkmessageprocessingstepimage")
        {
            ["imagetype"] = new OptionSetValue(1),
            ["attributes"] = "name,accountid"
        };

        Assert.False(StepRegistrar.ImageHasChanges(existing, 1, "name,accountid"));
    }

    [Fact]
    public void ImageHasChanges_TypeChanged_ReturnsTrue()
    {
        var existing = new Entity("sdkmessageprocessingstepimage")
        {
            ["imagetype"] = new OptionSetValue(0),
            ["attributes"] = "name"
        };

        Assert.True(StepRegistrar.ImageHasChanges(existing, 1, "name"));
    }

    [Fact]
    public void ImageHasChanges_AttributesChanged_ReturnsTrue()
    {
        var existing = new Entity("sdkmessageprocessingstepimage")
        {
            ["imagetype"] = new OptionSetValue(1),
            ["attributes"] = "name"
        };

        Assert.True(StepRegistrar.ImageHasChanges(existing, 1, "name,accountid"));
    }

    // ═══════════════════════════════════════════════════════════════
    //  IsValidImageType Tests
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("Create", 1, true)]   // PostImage only
    [InlineData("Create", 0, false)]  // PreImage not supported
    [InlineData("Create", 2, false)]  // Both not supported
    [InlineData("Delete", 0, true)]   // PreImage only
    [InlineData("Delete", 1, false)]  // PostImage not supported
    [InlineData("Update", 0, true)]   // All types supported
    [InlineData("Update", 1, true)]
    [InlineData("Update", 2, true)]
    [InlineData("SetState", 0, true)]
    public void IsValidImageType_AllCases(string message, int imageType, bool expected)
    {
        Assert.Equal(expected, StepRegistrar.IsValidImageType(message, imageType));
    }

    // ═══════════════════════════════════════════════════════════════
    //  GetMessagePropertyName Tests
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("Create", "Id")]
    [InlineData("Update", "Target")]
    [InlineData("Delete", "Target")]
    [InlineData("SetState", "EntityMoniker")]
    [InlineData("SetStateDynamicEntity", "EntityMoniker")]
    [InlineData("Assign", "Target")]
    [InlineData("Retrieve", "Target")]
    [InlineData("RetrieveMultiple", "Query")]
    [InlineData("SomeCustomMessage", "Target")]
    public void GetMessagePropertyName_AllCases(string message, string expected)
    {
        Assert.Equal(expected, StepRegistrar.GetMessagePropertyName(message));
    }

    // ═══════════════════════════════════════════════════════════════
    //  RegisterSteps Integration (with mocked IOrganizationService)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void RegisterSteps_NoPluginTypes_LogsError()
    {
        var svc = Substitute.For<IOrganizationService>();
        svc.RetrieveMultiple(Arg.Any<QueryExpression>()).Returns(new EntityCollection());

        var logs = new List<string>();
        var registrar = new StepRegistrar(svc, logs.Add);

        registrar.RegisterSteps("TestAssembly", [CreateStep()]);

        Assert.Contains(logs, l => l.Contains("ERROR") && l.Contains("No registered PluginTypes"));
    }

    // ═══════════════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════════════

    private static PluginStepInfo CreateStep(
        int stage = 40, int execMode = 0, int isolationMode = 2, int execOrder = 1)
    {
        return new PluginStepInfo
        {
            PluginTypeName = "MyPlugin.TestPlugin",
            Message = "Update",
            Name = "MyPlugin.TestPlugin: Update of account",
            EntityLogicalName = "account",
            Stage = stage,
            ExecutionMode = execMode,
            IsolationMode = isolationMode,
            ExecutionOrder = execOrder
        };
    }

    private static Entity BuildExistingStepEntity(PluginStepInfo step, Guid messageId, Guid? filterId)
    {
        var entity = new Entity("sdkmessageprocessingstep", Guid.NewGuid())
        {
            ["stage"] = new OptionSetValue(step.Stage),
            ["mode"] = new OptionSetValue(step.ExecutionMode),
            ["isolationmode"] = new OptionSetValue(step.IsolationMode),
            ["rank"] = step.ExecutionOrder,
            ["asyncautodelete"] = step.DeleteAsyncOperation,
            ["filteringattributes"] = step.FilteringAttributes ?? "",
            ["description"] = step.Description ?? "",
            ["configuration"] = step.UnSecureConfiguration ?? "",
            ["sdkmessageid"] = new EntityReference("sdkmessage", messageId)
        };

        if (filterId.HasValue)
            entity["sdkmessagefilterid"] = new EntityReference("sdkmessagefilter", filterId.Value);

        return entity;
    }
}
