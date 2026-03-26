using Dataverse.PluginRegistration;

namespace Dataverse.PluginRegistration.Tests;

public class AttributeReaderTests
{
    // ═══════════════════════════════════════════════════════════════
    //  MapStage Tests
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(10, 10)]  // PreValidation direct
    [InlineData(20, 20)]  // PreOperation direct
    [InlineData(40, 40)]  // PostOperation direct
    [InlineData(0, 10)]   // Enum ordinal 0 → PreValidation
    [InlineData(1, 20)]   // Enum ordinal 1 → PreOperation
    [InlineData(2, 40)]   // Enum ordinal 2 → PostOperation
    [InlineData(30, 30)]  // MainOperation passthrough
    public void MapStage_AllValues_MapsCorrectly(int input, int expected)
    {
        Assert.Equal(expected, AttributeReader.MapStage(input));
    }

    // ═══════════════════════════════════════════════════════════════
    //  MapExecMode Tests
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(0, 1)]  // Asynchronous enum → Dataverse async (1)
    [InlineData(1, 0)]  // Synchronous enum → Dataverse sync (0)
    public void MapExecMode_AllValues_MapsCorrectly(int input, int expected)
    {
        Assert.Equal(expected, AttributeReader.MapExecMode(input));
    }

    // ═══════════════════════════════════════════════════════════════
    //  MapIsolation Tests
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(0, 1)]  // None enum → Dataverse None (1)
    [InlineData(1, 2)]  // Sandbox enum → Dataverse Sandbox (2)
    [InlineData(2, 2)]  // Already Dataverse value → passthrough
    public void MapIsolation_AllValues_MapsCorrectly(int input, int expected)
    {
        Assert.Equal(expected, AttributeReader.MapIsolation(input));
    }
}
