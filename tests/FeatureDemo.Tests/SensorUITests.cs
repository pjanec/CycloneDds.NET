using NUnit.Framework;
using FeatureDemo.Scenarios.SensorArray;
using CycloneDDS.Runtime;
using System.Threading.Tasks;
using System;
using System.Threading;

namespace FeatureDemo.Tests;

public class SensorUITests
{
    [Test]
    public void SensorUI_Instantiate_DoesNotThrow()
    {
        // Use a dummy participant
        using var participant = new DdsParticipant(200);
        using var sub = new SensorSubscriber(participant);
        var ui = new SensorUI(sub);
        Assert.That(ui, Is.Not.Null);
    }
}
