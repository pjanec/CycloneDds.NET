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
var pointSample = new Point { X = 10, Y = 20 };
pointWriter.Write(pointSample);
Console.WriteLine($"Sent Point: X={pointSample.X}, Y={pointSample.Y}");

// Receive the sample
Task.Delay(100).Wait(); // Brief wait for discovery/传输
using var pointLoan = pointReader.Take(1);
foreach (var sample in pointLoan)
{
    if (sample.IsValid)
    {
        var data = sample.Data;
        Console.WriteLine($"Received Point: X={data.X}, Y={data.Y}");
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
// Create a complex sample
// Note: extended.BasePoint is a CommonLib.Point
var extendedSample = new ExtendedPoint
{
    Id = 12345,
    BasePoint = new Point { X = 100, Y = 200 }, 
    Color = CommonLib_Color.CommonlibGreen, // From CommonLib enum
    ExtraInfo = new Result 
    { 
       _d = true, // Discriminator for Union
       Value = new Point { X = 999, Y = 999 } 
    },
    // Using List<string> for sequence<string>
    Tags = new List<string> { "demo", "idl-import", "cyclonedds" }
};

extendedWriter.Write(extendedSample);
Console.WriteLine($"Sent ExtendedPoint: ID={extendedSample.Id}, Color={extendedSample.Color}");

Task.Delay(100).Wait();

using var extendedLoan = extendedReader.Take(1);
foreach (var sample in extendedLoan)
{
    if (sample.IsValid)
    {
        // Using managed read for convenience with lists/strings
        var data = sample.Data; 
        Console.WriteLine($"Received ExtendedPoint: ID={data.Id}");
        Console.WriteLine($"  Base Point: ({data.BasePoint.X}, {data.BasePoint.Y})");
        Console.WriteLine($"  Color: {data.Color}");
        Console.WriteLine($"  Tags: {string.Join(", ", data.Tags)}");
        
        if (data.ExtraInfo._d)
        {
             Console.WriteLine($"  Extra Info (Point): ({data.ExtraInfo.Value.X}, {data.ExtraInfo.Value.Y})");
        }
    }
}

Console.WriteLine("\nDemo completed successfully.");
