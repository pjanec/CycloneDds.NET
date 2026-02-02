using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CycloneDDS.Runtime;
using CycloneDDS.Schema;

namespace FeatureDemo.Scenarios.FlightRadar;

public class FlightPublisher : IDisposable
{
    private class FlightState
    {
        public string Id { get; set; } = "";
        public double Lat { get; set; }
        public double Lon { get; set; }
        public double Alt { get; set; }
        public double Heading { get; set; }
    }

    private readonly DdsWriter<FlightPosition> _writer;
    private bool _running;
    private readonly List<FlightState> _flights = new List<FlightState>();
    private long _messagesSent;

    public long MessagesSent => Interlocked.Read(ref _messagesSent);

    public FlightPublisher(DdsParticipant participant)
    {
        _writer = new DdsWriter<FlightPosition>(participant, "FlightPosition");
        
        // Init flights around the world
        _flights.Add(new FlightState { Id = "BA-123", Lat = 51.5, Lon = -0.1, Alt = 30000, Heading = 90 });
        _flights.Add(new FlightState { Id = "LH-456", Lat = 50.0, Lon = 8.5, Alt = 32000, Heading = 180 });
        _flights.Add(new FlightState { Id = "AF-789", Lat = 48.8, Lon = 2.3, Alt = 28000, Heading = 45 });
        _flights.Add(new FlightState { Id = "UA-101", Lat = 40.7, Lon = -74.0, Alt = 35000, Heading = 270 });
        _flights.Add(new FlightState { Id = "KL-202", Lat = 52.3, Lon = 4.7, Alt = 31000, Heading = 0 });
    }

    public async Task StartPublishingAsync(int targetRateHz, CancellationToken ct)
    {
        _running = true;
        var period = TimeSpan.FromSeconds(1.0 / targetRateHz);
        var nextTime = DateTime.UtcNow;
        var rand = new Random();

        while (!ct.IsCancellationRequested && _running)
        {
            foreach (var flight in _flights)
            {
                // Simple movement simulation
                flight.Lat += Math.Cos(flight.Heading * Math.PI / 180.0) * 0.001; 
                flight.Lon += Math.Sin(flight.Heading * Math.PI / 180.0) * 0.001;
                flight.Alt += (rand.NextDouble() - 0.5) * 50;

                var data = new FlightPosition
                {
                    FlightId = new FixedString32(flight.Id),
                    Latitude = flight.Lat,
                    Longitude = flight.Lon,
                    Altitude = flight.Alt,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };

                _writer.Write(data);
                Interlocked.Increment(ref _messagesSent);
            }

            nextTime += period;
            var delay = nextTime - DateTime.UtcNow;
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, ct);
            }
        }
    }

    public void Stop()
    {
        _running = false;
    }

    public void Dispose()
    {
        _writer?.Dispose();
    }
}
