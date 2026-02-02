using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;

namespace FeatureDemo.Scenarios.SensorArray;

public class SensorUI
{
    private readonly SensorSubscriber _subscriber;

    public SensorUI(SensorSubscriber subscriber)
    {
        _subscriber = subscriber;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        var subTask = _subscriber.StartReceivingAsync(ct);

        var initial = new Panel("Initializing...");
        
        await AnsiConsole.Live(initial)
            .AutoClear(false)
            .StartAsync(async ctx =>
            {
                var stopwatch = Stopwatch.StartNew();
                long lastMsg = 0;
                long lastBytes = 0;
                double smoothRate = 0;
                double smoothAlloc = 0;

                while (!ct.IsCancellationRequested)
                {
                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(true);
                        if (key.Key == ConsoleKey.Spacebar)
                        {
                            var newMode = _subscriber.CurrentMode == SubscriberMode.Managed 
                                ? SubscriberMode.ZeroCopy 
                                : SubscriberMode.Managed;
                            _subscriber.CurrentMode = newMode;
                        }
                    }

                    long currentMsg = _subscriber.MessagesReceived;
                    long currentBytes = _subscriber.BytesAllocated;
                    double elapsed = stopwatch.Elapsed.TotalSeconds;
                    stopwatch.Restart();

                    long deltaMsg = currentMsg - lastMsg;
                    long deltaBytes = currentBytes - lastBytes;
                    
                    lastMsg = currentMsg;
                    lastBytes = currentBytes;

                    double rate = elapsed > 0 ? deltaMsg / elapsed : 0;
                    double allocPerMsg = deltaMsg > 0 ? (double)deltaBytes / deltaMsg : 0;

                    if (smoothRate == 0) smoothRate = rate;
                    else smoothRate = smoothRate * 0.8 + rate * 0.2;
                    
                    if (smoothAlloc == 0) smoothAlloc = allocPerMsg;
                    else smoothAlloc = smoothAlloc * 0.8 + allocPerMsg * 0.2;

                    ctx.UpdateTarget(UpdateView(smoothRate, smoothAlloc, _subscriber.CurrentMode, currentMsg));

                    await Task.Delay(200, ct); 
                }
            });

        try { await subTask; } catch (OperationCanceledException) {}
    }
    
    private Table UpdateView(double throughput, double allocPerMsg, SubscriberMode mode, long totalMsgs)
    {
        var table = new Table().Border(TableBorder.Double).Expand();
        table.AddColumn("Zero-Allocation Sensor Demo");
        
        var modeStr = mode == SubscriberMode.Managed 
            ? "[red]⚪ Managed (Allocating)[/]" 
            : "[bold green]⚫ Zero-Copy (Optimized)[/]";
            
        var allocColor = allocPerMsg > 80 ? "red" : "green";

        var grid = new Grid();
        grid.AddColumn(new GridColumn().Width(20));
        grid.AddColumn();
        
        grid.AddRow(new Markup("[bold]Current Mode:[/]") , new Markup(modeStr));
        grid.AddRow(new Markup(""), new Markup("[dim]Press SPACE to toggle[/]"));
        grid.AddRow(new Markup(""), new Markup(""));

        grid.AddRow(new Markup("[bold]Throughput:[/]") , new BarChart()
                                                            .Width(50)
                                                            .AddItem("Msg/s", throughput, Color.Blue)
                                                            .WithMaxValue(10000));
        
        grid.AddRow(new Markup("[bold]Alloc/Msg:[/]") , new Markup($"[{allocColor}]{allocPerMsg:F1} bytes[/]"));
        grid.AddRow(new Markup("[bold]Total Received:[/]") , new Markup($"{totalMsgs:N0}"));
        
        table.AddRow(grid);
        return table;
    }
}
