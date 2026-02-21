using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CycloneDDS.Core;
using CycloneDDS.Runtime;

// Namespaces from the imported IDLs
using CommonLib; 
using AppLib;

Console.WriteLine("CycloneDDS IDL Import Demo");
Console.WriteLine("==========================");

// 1. Create Participant
using var participant = new DdsParticipant();


// ---------------------------------------------------------
// Part A: Publish/Subscribe basic types from CommonLib
// ---------------------------------------------------------
Console.WriteLine("\n[Part A] Testing CommonLib types...");

// The generated 'Point' struct is in the CommonLib namespace
var pointTopicName = "CommonPointTopic"; // Arbitrary topic name
using var pointWriter = new DdsWriter<Point>(participant, pointTopicName);
using var pointReader = new DdsReader<Point>(participant, pointTopicName);

// Publish a sample
var pointSample = new Point { x = 10, y = 20 };
pointWriter.Write(pointSample);
Console.WriteLine($"Sent Point: X={pointSample.x}, Y={pointSample.y}");

// Receive the sample
Task.Delay(100).Wait(); // Brief wait for discovery/传输
using var pointLoan = pointReader.Take(1);
foreach (var sample in pointLoan)
{
    if (sample.IsValid)
    {
        var data = sample.Data;
        Console.WriteLine($"Received Point: X={data.x}, Y={data.y}");
    }
}


// ---------------------------------------------------------
// Part B: Publish/Subscribe complex types from AppLib
// ---------------------------------------------------------
Console.WriteLine("\n[Part B] Testing AppLib types (referencing CommonLib)...");

var appTopicName = "ExtendedPointTopic";
using var extendedWriter = new DdsWriter<ExtendedPoint>(participant, appTopicName);
using var extendedReader = new DdsReader<ExtendedPoint>(participant, appTopicName);

// Create a complex sample
// Note: extended.BasePoint is a CommonLib.Point
var extendedSample = new ExtendedPoint
{
    id = 12345,
    base_point = new Point { x = 100, y = 200 }, 
    color = CommonLib.Color.GREEN, // From CommonLib enum (Scoped now!)
    extra_info = new Result 
    { 
       _d = true, // Discriminator for Union
       value = new Point { x = 999, y = 999 } 
    },
    // Using List<string> for sequence<string>
    tags = new List<string> { "demo", "idl-import", "cyclonedds" }
};

extendedWriter.Write(extendedSample);
Console.WriteLine($"Sent ExtendedPoint: ID={extendedSample.id}, Color={extendedSample.color}");

Task.Delay(100).Wait();

using var extendedLoan = extendedReader.Take(1);
foreach (var sample in extendedLoan)
{
    if (sample.IsValid)
    {
        // Using managed read for convenience with lists/strings
        var data = sample.Data; 
        Console.WriteLine($"Received ExtendedPoint: ID={data.id}");
        Console.WriteLine($"  Base Point: ({data.base_point.x}, {data.base_point.y})");
        Console.WriteLine($"  Color: {data.color}");
        Console.WriteLine($"  Tags: {string.Join(", ", data.tags)}");
        
        if (data.extra_info._d)
        {
             Console.WriteLine($"  Extra Info (Point): ({data.extra_info.value.x}, {data.extra_info.value.y})");
        }
    }
}

Console.WriteLine("\nDemo completed successfully.");
