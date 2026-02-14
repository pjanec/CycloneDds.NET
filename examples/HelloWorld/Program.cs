using System;
using System.Threading.Tasks;
using CycloneDDS.Core;
using CycloneDDS.Runtime;
using CycloneDDS.Schema;

namespace HelloWorld
{
    [DdsTopic("HelloWorldTopic")]
    public partial struct HelloWorldMessage
    {
        [DdsKey]
        public int Id;
        [DdsManaged]
        public string Message;
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Starting CycloneDDS.NET Hello World...");

            // Create a participant
            using var participant = new DdsParticipant();
            
            // Create a wrapper for topic registration handled internally by Writer/Reader

            const string TopicName = "HelloWorldTopic";

            // Create a writer
            using var writer = new DdsWriter<HelloWorldMessage>(participant, TopicName);

            // Create a reader
            using var reader = new DdsReader<HelloWorldMessage>(participant, TopicName);

            // Local helper to read synchronously
            void ReadSamples()
            {
                using var samples = reader.Read();
                foreach (var sample in samples)
                {
                    Console.WriteLine($"Received: [{sample.Data.Id}] {sample.Data.Message}");
                }
            }

            // Task to write data
            var writeTask = Task.Run(async () =>
            {
                for (int i = 0; i < 10; i++)
                {
                    await Task.Delay(500);
                    var msg = new HelloWorldMessage { Id = i, Message = $"Hello World {i}" };
                    Console.WriteLine($"Writing: {msg.Message}");
                    writer.Write(msg);
                }
            });

            // Read data
            Console.WriteLine("Waiting for data...");
            for (int i = 0; i < 20; i++)
            {
                try 
                {
                    // Simple reading loop
                    await Task.Delay(250); 
                    ReadSamples();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Read error: {ex.Message}");
                }
            }

            await writeTask;
            Console.WriteLine("Done.");
        }
    }
}
