using System;
using System.Reflection;
using NUnit.Framework;
using CycloneDDS.Schema;
using CycloneDDS.Runtime;

namespace FeatureDemo.Tests;

[TestFixture]
public class SchemaTests
{
    [Test]
    public void SchemaDefinitions_CompileSuccessfully()
    {
        // Verify code generation produces expected types
        Assert.That(typeof(DemoControl), Is.Not.Null);
        Assert.That(typeof(ChatMessage), Is.Not.Null);
        Assert.That(typeof(SensorData), Is.Not.Null);
        Assert.That(typeof(StockTick), Is.Not.Null);
        Assert.That(typeof(FlightPosition), Is.Not.Null);
        Assert.That(typeof(SystemLog), Is.Not.Null);
    }

    [Test]
    public void DemoControl_HasCorrectQoS()
    {
        // Verify DemoControl has TransientLocal + Reliable
        var attr = typeof(DemoControl).GetCustomAttribute<DdsTopicAttribute>();
        Assert.That(attr, Is.Not.Null);
        
        var qos = typeof(DemoControl).GetCustomAttribute<DdsQosAttribute>();
        Assert.That(qos, Is.Not.Null);
        Assert.That(qos.Reliability, Is.EqualTo(DdsReliability.Reliable));
        Assert.That(qos.Durability, Is.EqualTo(DdsDurability.TransientLocal));
    }

    [Test]
    public void ChatMessage_HasCorrectQoS()
    {
        var qos = typeof(ChatMessage).GetCustomAttribute<DdsQosAttribute>();
        Assert.That(qos, Is.Not.Null);
        Assert.That(qos.Reliability, Is.EqualTo(DdsReliability.Reliable));
    }

    [Test]
    public void SensorData_HasCorrectQoS()
    {
        var qos = typeof(SensorData).GetCustomAttribute<DdsQosAttribute>();
        Assert.That(qos, Is.Not.Null);
        // BestEffort for high throughput
        Assert.That(qos.Reliability, Is.EqualTo(DdsReliability.BestEffort));
    }
}
