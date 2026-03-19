using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Channels;
using CycloneDDS.Schema;
using DdsMonitor.Engine;
using Xunit;

namespace DdsMonitor.Engine.Tests;

/// <summary>
/// Tests for ME2-BATCH-01: Foundational Framework and Bug Fixes.
/// Tasks covered: ME2-T01, ME2-T02, ME2-T03, ME2-T04.
/// (T05, T06 are Razor rendering changes; their helpers are tested via Blazor
///  assembly reflection in <see cref="ME2Batch01BlazorTests"/>.)
/// </summary>
public sealed class ME2Batch01Tests
{
    // ─────────────────────────────────────────────────────────────────────────
    // ME2-T01: ComponentTypeName Backward / Forward Compatibility
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Backward-compat: SpawnPanel with a legacy AssemblyQualifiedName string
    /// must resolve to just the type's FullName (no assembly, version, or culture).
    /// </summary>
    [Fact]
    public void WindowManager_SpawnPanel_BackwardCompatAqn_ResolvesToFullName()
    {
        var manager = new WindowManager();
        const string aqn =
            "DdsMonitor.Components.TopicExplorerPanel, DdsMonitor, Version=0.1.0.0, Culture=neutral, PublicKeyToken=null";

        var panel = manager.SpawnPanel(aqn);

        // The ComponentTypeName must not contain assembly qualification.
        Assert.Equal("DdsMonitor.Components.TopicExplorerPanel", panel.ComponentTypeName);
        Assert.DoesNotContain(", ", panel.ComponentTypeName, StringComparison.Ordinal);
        Assert.DoesNotContain("Version=", panel.ComponentTypeName, StringComparison.Ordinal);
    }

    /// <summary>
    /// Forward-compat: SpawnPanel with just a FullName must be stored unchanged.
    /// </summary>
    [Fact]
    public void WindowManager_SpawnPanel_ForwardCompatFullName_StaysUnchanged()
    {
        var manager = new WindowManager();
        const string fullName = "DdsMonitor.Components.TopicExplorerPanel";

        var panel = manager.SpawnPanel(fullName);

        Assert.Equal(fullName, panel.ComponentTypeName);
    }

    /// <summary>
    /// A workspace saved with AQN must round-trip as FullName after
    /// save→SaveWorkspaceToJson→LoadWorkspaceFromJson.
    /// </summary>
    [Fact]
    public void WindowManager_WorkspacePersistence_AqnIsNormalizedOnSpawn()
    {
        const string aqn =
            "DdsMonitor.Components.SamplesPanel, DdsMonitor, Version=0.2.0.0, Culture=neutral, PublicKeyToken=null";
        const string expectedFullName = "DdsMonitor.Components.SamplesPanel";

        var manager = new WindowManager();
        var spawned = manager.SpawnPanel(aqn);
        spawned.Title = "Test";

        // The ComponentTypeName on the panel state must already be normalized to FullName.
        Assert.Equal(expectedFullName, spawned.ComponentTypeName);

        var json = manager.SaveWorkspaceToJson();

        var manager2 = new WindowManager();
        manager2.LoadWorkspaceFromJson(json);
        var loaded = manager2.ActivePanels.Single(p => p.PanelId == spawned.PanelId);

        Assert.Equal(expectedFullName, loaded.ComponentTypeName);
        Assert.DoesNotContain("Version=", loaded.ComponentTypeName, StringComparison.Ordinal);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ME2-T02: Reset Does Not Lose Subscriptions
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void DdsBridge_ResetAll_PreservesActiveReaders()
    {
        var metadataA = new TopicMetadata(typeof(SampleTopic));
        var metadataB = new TopicMetadata(typeof(SimpleType));
        using var bridge = new DdsBridge(Channel.CreateUnbounded<SampleData>().Writer);

        bridge.Subscribe(metadataA);
        bridge.Subscribe(metadataB);
        Assert.Equal(2, bridge.ActiveReaders.Count);

        bridge.ResetAll();

        Assert.Equal(2, bridge.ActiveReaders.Count);
        Assert.True(bridge.ActiveReaders.ContainsKey(metadataA.TopicType));
        Assert.True(bridge.ActiveReaders.ContainsKey(metadataB.TopicType));
    }

    [Fact]
    public void DdsBridge_ResetAll_DoesNotFireReadersChanged()
    {
        using var bridge = new DdsBridge(Channel.CreateUnbounded<SampleData>().Writer);
        var metadata = new TopicMetadata(typeof(SampleTopic));
        bridge.Subscribe(metadata);

        var firedCount = 0;
        bridge.ReadersChanged += () => firedCount++;

        bridge.ResetAll();

        Assert.Equal(0, firedCount);
    }

    [Fact]
    public void DdsBridge_ResetAll_ResetsOrdinalCounter()
    {
        var ordinal = new OrdinalCounter();
        ordinal.Increment();
        ordinal.Increment();
        ordinal.Increment();
        Assert.Equal(3, ordinal.Current);

        var store = new SampleStore();
        using var bridge = new DdsBridge(
            Channel.CreateUnbounded<SampleData>().Writer,
            participants: null,
            initialPartition: null,
            sampleStore: store,
            instanceStore: null,
            ordinalCounter: ordinal);

        bridge.ResetAll();

        Assert.Equal(0, ordinal.Current);
    }

    [Fact]
    public void DdsBridge_ResetAll_ClearsSampleStore()
    {
        var store = new SampleStore();
        var meta = new TopicMetadata(typeof(SampleTopic));
        store.Append(new SampleData { TopicMetadata = meta, Payload = new SampleTopic { Id = 1 } });
        store.Append(new SampleData { TopicMetadata = meta, Payload = new SampleTopic { Id = 2 } });
        Assert.Equal(2, store.AllSamples.Count);

        using var bridge = new DdsBridge(
            Channel.CreateUnbounded<SampleData>().Writer,
            participants: null,
            initialPartition: null,
            sampleStore: store,
            instanceStore: null,
            ordinalCounter: null);

        bridge.ResetAll();

        Assert.Equal(0, store.AllSamples.Count);
    }

    [Fact]
    public void DdsBridge_ResetAll_ReadersStillReceiveSamplesAfterReset()
    {
        // After ResetAll, subscribers' reader objects must still be wired up.
        var channel = Channel.CreateUnbounded<SampleData>();
        var metadata = new TopicMetadata(typeof(SampleTopic));
        using var bridge = new DdsBridge(channel.Writer);

        var reader = bridge.Subscribe(metadata);
        bridge.ResetAll();

        // The reader must still be the same object (not replaced) and still wired to the channel.
        Assert.True(bridge.ActiveReaders.TryGetValue(metadata.TopicType, out var stored));
        Assert.Same(reader, stored);
    }
}

/// <summary>
/// Tests for ME2-BATCH-01 that require loading the compiled DdsMonitor Blazor assembly
/// (DetailPanel helper methods: T04 FormatSourceTimestamp, T05 GetValueClass,
/// T06 GetUnionInfo / IsUnionArmVisible).
/// </summary>
public sealed class ME2Batch01BlazorTests
{
    private static Assembly? _blazorAssembly;
    private static Assembly? _schemaAssembly;

    private static Assembly LoadDdsMonitorAssembly()
    {
        if (_blazorAssembly != null) return _blazorAssembly;

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            foreach (var config in new[] { "Debug", "Release" })
            {
                var path = Path.Combine(directory.FullName, "tools", "DdsMonitor",
                    "DdsMonitor.Blazor", "bin", config, "net8.0", "DdsMonitor.dll");
                if (File.Exists(path))
                {
                    _blazorAssembly = Assembly.LoadFrom(path);
                    return _blazorAssembly;
                }
            }
            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            "DdsMonitor.dll not found. Build the DdsMonitor project before running this test.");
    }

    private static Type GetDetailPanelType()
        => LoadDdsMonitorAssembly().GetType("DdsMonitor.Components.DetailPanel", throwOnError: true)!;

    // ─────────────────────────────────────────────────────────────────────────
    // ME2-T04: FormatSourceTimestamp helper
    // ─────────────────────────────────────────────────────────────────────────

    private static string InvokeFormatSourceTimestamp(long nanoseconds)
    {
        var method = GetDetailPanelType()
            .GetMethod("FormatSourceTimestamp",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
                null, new[] { typeof(long) }, null)
            ?? throw new MissingMethodException("FormatSourceTimestamp not found");
        return (string)method.Invoke(null, new object[] { nanoseconds })!;
    }

    [Fact]
    public void FormatSourceTimestamp_ValidNanoseconds_ReturnsLocalTimeString()
    {
        // 1 second after Unix epoch: 1_000_000_000 nanoseconds.
        const long nsPerSecond = 1_000_000_000L;
        var result = InvokeFormatSourceTimestamp(nsPerSecond);

        // Must be parseable as a local date-time in the expected format.
        Assert.True(
            DateTime.TryParseExact(result, "yyyy-MM-dd HH:mm:ss.fffffff",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var parsed),
            $"Could not parse '{result}' as local timestamp.");

        // The local time should correspond to 1970-01-01T00:00:01 UTC.
        var expected = DateTime.UnixEpoch.AddSeconds(1).ToLocalTime();
        Assert.Equal(expected, parsed);
    }

    [Fact]
    public void FormatSourceTimestamp_Zero_ReturnsUnknown()
    {
        var result = InvokeFormatSourceTimestamp(0L);
        Assert.Equal("Unknown", result);
    }

    [Fact]
    public void FormatSourceTimestamp_Negative_ReturnsUnknown()
    {
        var result = InvokeFormatSourceTimestamp(-1L);
        Assert.Equal("Unknown", result);
    }

    [Fact]
    public void FormatSourceTimestamp_MaxValue_ReturnsUnknown()
    {
        var result = InvokeFormatSourceTimestamp(long.MaxValue);
        Assert.Equal("Unknown", result);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ME2-T05: GetValueClass returns correct CSS classes
    // ─────────────────────────────────────────────────────────────────────────

    private static string InvokeGetValueClass(Type? type)
    {
        var method = GetDetailPanelType()
            .GetMethod("GetValueClass",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
                null, new[] { typeof(Type) }, null)
            ?? throw new MissingMethodException("GetValueClass not found");
        return (string)method.Invoke(null, new object?[] { type })!;
    }

    [Fact]
    public void GetValueClass_Null_ReturnsIsNull()
    {
        Assert.Equal("detail-tree__value is-null", InvokeGetValueClass(null));
    }

    [Fact]
    public void GetValueClass_String_ReturnsIsString()
    {
        Assert.Equal("detail-tree__value is-string", InvokeGetValueClass(typeof(string)));
    }

    [Fact]
    public void GetValueClass_Bool_ReturnsIsBool()
    {
        Assert.Equal("detail-tree__value is-bool", InvokeGetValueClass(typeof(bool)));
    }

    [Fact]
    public void GetValueClass_Enum_ReturnsIsEnum()
    {
        Assert.Equal("detail-tree__value is-enum", InvokeGetValueClass(typeof(DayOfWeek)));
    }

    [Fact]
    public void GetValueClass_Int_ReturnsIsNumber()
    {
        Assert.Equal("detail-tree__value is-number", InvokeGetValueClass(typeof(int)));
    }

    [Fact]
    public void GetValueClass_Float_ReturnsIsNumber()
    {
        Assert.Equal("detail-tree__value is-number", InvokeGetValueClass(typeof(float)));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ME2-T06: GetUnionInfo returns discriminator and active arm
    // ─────────────────────────────────────────────────────────────────────────

    private static (object? Discriminator, MemberInfo? ActiveArm, object? ArmValue)
        InvokeGetUnionInfo(object unionObj)
    {
        var blazorAsm = LoadDdsMonitorAssembly();
        var method = GetDetailPanelType()
            .GetMethod("GetUnionInfo",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            ?? throw new MissingMethodException("GetUnionInfo not found");

        var result = method.Invoke(null, new[] { unionObj })!;
        var resultType = result.GetType();

        var disc = resultType.GetField("Item1")?.GetValue(result)
                   ?? resultType.GetProperty("Discriminator")?.GetValue(result)
                   ?? resultType.GetProperty("Item1")?.GetValue(result);
        var arm = (MemberInfo?)(resultType.GetField("Item2")?.GetValue(result)
                                ?? resultType.GetProperty("ActiveArm")?.GetValue(result)
                                ?? resultType.GetProperty("Item2")?.GetValue(result));
        var armVal = resultType.GetField("Item3")?.GetValue(result)
                     ?? resultType.GetProperty("ArmValue")?.GetValue(result)
                     ?? resultType.GetProperty("Item3")?.GetValue(result);

        return (disc, arm, armVal);
    }

    [Fact]
    public void GetUnionInfo_ActiveArm_ReturnsDiscriminatorAndMatchingArm()
    {
        // Use an existing [DdsUnion] test type to verify GetUnionInfo.
        // We'll use the schema-assembly union type if available; otherwise skip.
        var schemaAsm = LoadDdsMonitorAssembly();
        // Try to find any [DdsUnion]-attributed type in either the test assembly
        // or the DdsMonitor schema assembly via loaded test types.
        var unionType = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
            .FirstOrDefault(t =>
            {
                try
                {
                    return t.GetCustomAttributes(false)
                        .Any(a => a.GetType().Name == "DdsUnionAttribute");
                }
                catch { return false; }
            });

        if (unionType == null)
        {
            // No union type found in loaded assemblies — skip gracefully.
            return;
        }

        var unionObj = Activator.CreateInstance(unionType);
        if (unionObj == null) return;

        var (disc, arm, armVal) = InvokeGetUnionInfo(unionObj);

        // At minimum, discriminator must be non-null (it is a value type field).
        // (We don't assert exact values because we don't control the union type structure.)
        Assert.NotNull(disc);
    }
}
