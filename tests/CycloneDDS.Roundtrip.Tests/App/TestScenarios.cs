using System.Collections.Generic;
using System.Linq;

namespace CycloneDDS.Roundtrip.App;

/// <summary>
/// Defines the test scenarios for roundtrip verification.
/// Each scenario tests both C#→Native and Native→C# directions.
/// </summary>
internal class TestScenarios
{
    #region Test Definitions

    /// <summary>
    /// Get all test scenarios to run.
    /// </summary>
    public static List<TestScenario> GetAll()
    {
        return new List<TestScenario>
        {
            // Basic types - handler implemented
            new TestScenario
            {
                TopicName = "AllPrimitives",
                Description = "All primitive types (bool, char, int8/16/32/64, float, double)",
                Seeds = new[] { 42, 99, 0, -1, 12345 },
                Enabled = true
            },

            new TestScenario
            {
                TopicName = "CompositeKey",
                Description = "Multiple key fields",
                Seeds = new[] { 1, 2, 3, 100, 200 },
                Enabled = true
            },

            new TestScenario
            {
                TopicName = "NestedKeyTopic",
                Description = "Nested struct with keys at multiple levels",
                Seeds = new[] { 10, 20, 30 },
                Enabled = false
            },

            new TestScenario
            {
                TopicName = "SequenceTopic",
                Description = "Variable-length sequences",
                Seeds = new[] { 5, 10, 15 },
                Enabled = false
            },

            // Advanced types - handlers TODO
            new TestScenario
            {
                TopicName = "NestedSequences",
                Description = "Sequences of sequences (2D variable-length)",
                Seeds = new[] { 7, 14 },
                Enabled = false // Enable when handler is implemented
            },

            new TestScenario
            {
                TopicName = "ArrayTopic",
                Description = "Fixed-size arrays (1D and 2D)",
                Seeds = new[] { 25, 50 },
                Enabled = false
            },

            new TestScenario
            {
                TopicName = "StringTopic",
                Description = "Bounded and unbounded strings",
                Seeds = new[] { 111, 222 },
                Enabled = false
            },

            new TestScenario
            {
                TopicName = "OptionalFields",
                Description = "Optional fields (nullable types)",
                Seeds = new[] { 0, 1, 5, 10 }, // Seed 0 and 5 will trigger nulls
                Enabled = false
            },

            new TestScenario
            {
                TopicName = "Person",
                Description = "Nested structs (Address inside Person)",
                Seeds = new[] { 888, 999 },
                Enabled = false
            },

            new TestScenario
            {
                TopicName = "UnionTopic",
                Description = "Discriminated unions",
                Seeds = new[] { 0, 1, 2, 3 }, // Different discriminator values
                Enabled = false
            },

            new TestScenario
            {
                TopicName = "TypedefChain",
                Description = "Nested typedef aliases",
                Seeds = new[] { 444, 555 },
                Enabled = false
            },

            new TestScenario
            {
                TopicName = "MixedComplexTopic",
                Description = "Complex mix: sequences + arrays + optional + nested",
                Seeds = new[] { 77, 88 },
                Enabled = false
            },

            new TestScenario
            {
                TopicName = "LargePayload",
                Description = "Large data payload (10KB+)",
                Seeds = new[] { 12345 },
                Enabled = false
            },

            new TestScenario
            {
                TopicName = "SensorData",
                Description = "Multi-instance keyed topic",
                Seeds = new[] { 1001, 1002, 1003 }, // Different sensor IDs
                Enabled = false
            },

            new TestScenario
            {
                TopicName = "DeepNesting",
                Description = "Deeply nested structures (5+ levels)",
                Seeds = new[] { 2000, 3000 },
                Enabled = false
            }
        };
    }

    /// <summary>
    /// Get only enabled test scenarios.
    /// </summary>
    public static List<TestScenario> GetEnabled()
    {
        return GetAll().Where(t => t.Enabled).ToList();
    }

    #endregion
}

/// <summary>
/// Represents a single test scenario.
/// </summary>
internal class TestScenario
{
    /// <summary>
    /// Topic name (matches IDL @topic annotation).
    /// </summary>
    public required string TopicName { get; init; }

    /// <summary>
    /// Human-readable description of what this test covers.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// List of seed values to test.
    /// </summary>
    public required int[] Seeds { get; init; }

    /// <summary>
    /// Whether this test is enabled.
    /// Set to false for tests where handlers are not yet implemented.
    /// </summary>
    public required bool Enabled { get; init; }
}
