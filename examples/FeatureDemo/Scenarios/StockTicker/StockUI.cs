using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;
using FeatureDemo.Scenarios;
using CycloneDDS.Schema;

namespace FeatureDemo.Scenarios.StockTicker;

public class StockUI
{
    private readonly StockSubscriber _subscriber;
    private readonly ConcurrentQueue<StockTick> _trafficLog = new ConcurrentQueue<StockTick>();
    private readonly ConcurrentQueue<StockTick> _filteredLog = new ConcurrentQueue<StockTick>();
    private const int MaxLogSize = 15;

    public StockUI(StockSubscriber subscriber)
    {
        _subscriber = subscriber;
        
        _subscriber.OnTickReceived += tick => 
        {
            _filteredLog.Enqueue(tick);
            while (_filteredLog.Count > MaxLogSize) _filteredLog.TryDequeue(out _);
        };

        _subscriber.OnRawTick += tick => 
        {
            _trafficLog.Enqueue(tick);
            while (_trafficLog.Count > MaxLogSize) _trafficLog.TryDequeue(out _);
        };
    }

    public async Task RunAsync(CancellationToken ct)
    {
        var runTask = _subscriber.StartProcessingAsync(ct);

        // Initial Layout
        var layout = new Layout("Root")
            .SplitColumns(
                new Layout("Traffic").Ratio(1),
                new Layout("Filtered").Ratio(1)
            );

        await AnsiConsole.Live(layout)
            .AutoClear(false)
            .StartAsync(async ctx =>
            {
                while (!ct.IsCancellationRequested)
                {
                   layout["Traffic"].Update(RenderTable("Network Traffic (All)", _trafficLog, false));
                   layout["Filtered"].Update(RenderTable("Filtered Feed (AAPL Only)", _filteredLog, true));
                   
                   ctx.Refresh();
                   try {
                       await Task.Delay(100, ct);
                   } catch (OperationCanceledException) { break; }
                }
            });

        try { await runTask; } catch (OperationCanceledException) {}
    }

    private Table RenderTable(string title, IEnumerable<StockTick> ticks, bool isFiltered)
    {
        var table = new Table().Title(title).Expand().Border(TableBorder.Rounded);
        table.AddColumn("Time");
        table.AddColumn("Sym");
        table.AddColumn("Price");

        if (isFiltered)
        {
             table.Caption($"Passed: {_subscriber.PassedFilter} / Total: {_subscriber.TotalReceived}");
        }

        // Show newest on top
        foreach (var tick in ticks.Reverse())
        {
            var time = DateTimeOffset.FromUnixTimeMilliseconds(tick.Timestamp).ToString("HH:mm:ss.f");
            var sym = tick.Symbol.ToString();
            var price = tick.Price;
            
            string color = "green";
            if (price < 100) color = "red"; // Arbitrary
            
            table.AddRow(
                time, 
                sym == "AAPL" ? $"[bold yellow]{sym}[/]" : sym, 
                $"[{color}]{price:F2}[/]"
            );
        }
        return table;
    }
}
