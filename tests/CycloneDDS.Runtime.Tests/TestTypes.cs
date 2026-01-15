using System;
using System.Runtime.InteropServices;
using CycloneDDS.CodeGen.Runtime;

namespace CycloneDDS.Runtime.Tests;

[StructLayout(LayoutKind.Sequential)]
public struct TestMessageNative
{
    public int Id;
    public int Value;
}

public static class TestRegistration
{
    public static void Register()
    {
        MetadataRegistry.Register(new TopicMetadata
        {
            TopicName = "TestMessage",
            NativeType = typeof(TestMessageNative),
            ManagedType = typeof(TestMessageNative), // Using same type for simplicity in this test
            Descriptor = null // Using implicit descriptor for now
        });
    }
}
