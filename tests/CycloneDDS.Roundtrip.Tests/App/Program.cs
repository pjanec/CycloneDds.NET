using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Reflection;
using System.Diagnostics;
using CycloneDDS.Core;
using CycloneDDS.Runtime;
using RoundtripTests;
using AtomicTests;

namespace CycloneDDS.Roundtrip.App;

/// <summary>
/// Roundtrip test application entry point.
/// Tests interoperability between C# and native C implementations.
/// </summary>
internal class Program
{
    private const int DefaultDomainId = 0;
    private const int DefaultTimeoutMs = 5000;

    // DDS Entities shared across tests
    private static DdsParticipant? _participant;

    static int Main(string[] args)
    {
        try
        {
            // Parse arguments
            int domainId = args.Length > 0 && int.TryParse(args[0], out int d) ? d : DefaultDomainId;

            // Initialize Native Side
            ConsoleReporter.PrintInitialization(domainId);
            NativeInterop.ThrowIfFailed(
                NativeInterop.Native_Init(domainId),
                "Native_Init");
            ConsoleReporter.PrintNativeInfo();

            // Initialize C# Side
            ConsoleReporter.Info("Initializing C# DDS Participant...");
            _participant = new DdsParticipant((uint)domainId);

            // Get test scenarios
            var scenarios = TestScenarios.GetEnabled();
            
            if (scenarios.Count == 0)
            {
                ConsoleReporter.Warning("No enabled test scenarios found!");
                return 1;
            }

            ConsoleReporter.PrintHeader("Test Suite: Roundtrip Verification");

            // Run tests
            var stopwatch = Stopwatch.StartNew();
            int passed = 0;
            int failed = 0;
            int testNumber = 1;

            foreach (var scenario in scenarios)
            {
                ConsoleReporter.StartTest(testNumber, scenarios.Count, scenario.TopicName);
                ConsoleReporter.TestStep($"Description: {scenario.Description}");

                bool scenarioPassed = RunScenarioDispatch(scenario);

                if (scenarioPassed)
                {
                    passed++;
                    ConsoleReporter.TestResult(true);
                }
                else
                {
                    failed++;
                    ConsoleReporter.TestResult(false);
                }

                testNumber++;
            }

            stopwatch.Stop();

            // Print summary
            ConsoleReporter.PrintSummary(
                total: scenarios.Count,
                passed: passed,
                failed: failed,
                elapsed: stopwatch.Elapsed);

            int exitCode = failed == 0 ? 0 : 1;
            ConsoleReporter.PrintExitCode(exitCode);

            // Cleanup
            _participant.Dispose();
            NativeInterop.Native_Cleanup();

            return exitCode;
        }
        catch (Exception ex)
        {
            ConsoleReporter.Error($"Fatal error: {ex.Message}");
            ConsoleReporter.Error($"Stack trace: {ex.StackTrace}");
            return 2;
        }
    }

    /// <summary>
    /// Dispatch to generic handler based on topic name.
    /// Since we can't easily resolve Type from string dynamically without Assembly scanning
    /// (and we want compile-time safety), we use a switch here.
    /// </summary>
    private static bool RunScenarioDispatch(TestScenario scenario)
    {
        return scenario.TopicName switch
        {
            "AllPrimitives" => RunTypedScenario<AllPrimitives>(scenario),
            "CompositeKey" => RunTypedScenario<CompositeKey>(scenario),
            "NestedKeyTopic" => RunTypedScenario<NestedKeyTopic>(scenario),
            "SequenceTopic" => RunTypedScenario<SequenceTopic>(scenario),
            "ArrayInt32Topic" => RunTypedScenario<ArrayInt32Topic>(scenario),
            "ArrayFloat64Topic" => RunTypedScenario<ArrayFloat64Topic>(scenario),
            "ArrayStringTopic" => RunTypedScenario<ArrayStringTopic>(scenario),
            _ => RunUnimplementedScenario(scenario)
        };
    }

    private static bool RunUnimplementedScenario(TestScenario scenario)
    {
        ConsoleReporter.Warning($"No C# implementation for '{scenario.TopicName}' yet.");
        return false;
    }

    /// <summary>
    /// Generic test runner for a specific IDL type T.
    /// </summary>
    private static bool RunTypedScenario<T>(TestScenario scenario) where T : struct
    {
        try
        {
            // 1. Create C# Entities
            ConsoleReporter.TestStep("Creating C# Entities...");
            using var writer = new DdsWriter<T>(_participant!, scenario.TopicName);
            using var reader = new DdsReader<T, T>(_participant!, scenario.TopicName);

            // 2. Create Native Entities
            ConsoleReporter.TestStep("Creating Native Entities...");
            NativeInterop.ThrowIfFailed(
                NativeInterop.Native_CreatePublisher(scenario.TopicName),
                "Native_CreatePublisher");
            
            NativeInterop.ThrowIfFailed(
                NativeInterop.Native_CreateSubscriber(scenario.TopicName),
                "Native_CreateSubscriber");
            
            // Allow discovery to happen
            Thread.Sleep(500);

            // 3. Test Loop
            foreach (int seed in scenario.Seeds)
            {
                // Direction 1: C# → Native
                if (!TestCSharpToNative(writer, seed, scenario.TopicName))
                    return false;

                // Direction 2: Native → C#
                if (!TestNativeToCSharp(reader, seed, scenario.TopicName))
                    return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            ConsoleReporter.Error($"Exception running test for {scenario.TopicName}: {ex.Message}");
            return false;
        }
    }

    private static bool TestCSharpToNative<T>(DdsWriter<T> writer, int seed, string topicName) where T : struct
    {
        ConsoleReporter.TestStep($"[C# → Native] Seed={seed}");

        // Generate Data
        var sample = new T();
        DataGenerator.Fill(ref sample, seed);

        // Publish
        try
        {
            writer.Write(sample);
        }
        catch (Exception ex)
        {
            ConsoleReporter.Error($"  C# Write failed: {ex.Message}");
            return false;
        }

        // Verify on Native Side
        int result = NativeInterop.Native_ExpectWithSeed(topicName, seed, DefaultTimeoutMs);
        string resultDesc = NativeInterop.DescribeExpectResult(result);

        if (result == 0)
        {
            ConsoleReporter.TestStep($"  Native Verified: {resultDesc} ✓");
            return true;
        }
        else
        {
            ConsoleReporter.Error($"  Native Verification Failed: {resultDesc}");
            if (result == -2) // Mismatch
               ConsoleReporter.Error($"  Details: {NativeInterop.GetLastError()}");
            return false;
        }
    }

    private static bool TestNativeToCSharp<T>(DdsReader<T, T> reader, int seed, string topicName) where T : struct
    {
        ConsoleReporter.TestStep($"[Native → C#] Seed={seed}");

        // Clear any old data
        try { reader.Take(); } catch {}

        // Send from Native
        int sendResult = NativeInterop.Native_SendWithSeed(topicName, seed);
        if (sendResult != 0)
        {
            ConsoleReporter.Error($"  Native Send failed: {NativeInterop.GetLastError()}");
            return false;
        }

        // Receive in C#
        T? receivedSample = null;
        
        // Polling loop (simple timeout)
        var deadline = DateTime.Now.AddMilliseconds(DefaultTimeoutMs);
        while (DateTime.Now < deadline)
        {
            try 
            {
                using var samples = reader.Take(1);
                if (samples.Count > 0)
                {
                    receivedSample = samples[0];
                    break;
                }
            }
            catch (Exception ex)
            {
                ConsoleReporter.Warning($"  Read error (retrying): {ex.Message}");
            }
            Thread.Sleep(100);
        }

        if (receivedSample == null)
        {
            ConsoleReporter.Error("  Timeout: No data received in C#");
            return false;
        }

        // Compare
        var expected = new T();
        DataGenerator.Fill(ref expected, seed);

        // Perform deep comparison
        bool match = DataGenerator.AreEqual(expected, receivedSample);

        if (match)
        {
            ConsoleReporter.TestStep("  C# Verified: Match ✓");
            return true;
        }
        else
        {
            ConsoleReporter.Error("  C# Verification Failed: Mismatch");
            ConsoleReporter.Error($"    Expected (seed={seed}): {expected}");
            ConsoleReporter.Error($"    Received: {receivedSample}");
            return false;
        }
    }
}
