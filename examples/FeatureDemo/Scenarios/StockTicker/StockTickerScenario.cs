using System;
using System.Threading;
using System.Threading.Tasks;
using FeatureDemo.Orchestration;
using CycloneDDS.Runtime;

namespace FeatureDemo.Scenarios.StockTicker;

public class StockTickerScenario : ScenarioBase
{
    public override string Name => "Stock Ticker (Filtering)";
    public override string Description => "Demonstrates content filtering (Server or Client side).";

    public StockTickerScenario(DemoOrchestrator orchestrator) : base(orchestrator)
    {
    }

    public override async Task RunStandaloneAsync(CancellationToken ct)
    {
        PrintScenarioHeader();
        
        using var participant = new DdsParticipant(); 
        using var publisher = new StockPublisher(participant);
        using var subscriber = new StockSubscriber(participant);
        
        // Setup filter
        subscriber.SetFilter("AAPL", 0);
        
        // Start publishing in background
        var pubTask = publisher.StartPublishingAsync(20, ct); 
        
        // Run UI
        var ui = new StockUI(subscriber);
        await ui.RunAsync(ct);
        
        publisher.Stop();
        try { await pubTask; } catch {}
    }

    public override async Task RunPublisherAsync(CancellationToken ct)
    {
        PrintScenarioHeader();
        using var participant = new DdsParticipant();
        using var publisher = new StockPublisher(participant);
        
        await publisher.StartPublishingAsync(20, ct); 
    }

    public override async Task RunSubscriberAsync(CancellationToken ct)
    {
        PrintScenarioHeader();
        using var participant = new DdsParticipant();
        using var subscriber = new StockSubscriber(participant);
        
        // In distributed mode, also filter AAPL?
        subscriber.SetFilter("AAPL", 0);
        
        var ui = new StockUI(subscriber);
        await ui.RunAsync(ct);
    }
}
