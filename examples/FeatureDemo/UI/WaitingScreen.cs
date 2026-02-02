using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Spectre.Console;

namespace FeatureDemo.UI;

public class WaitingScreen
{
    public async Task<bool> WaitForPeerAsync(Func<bool> checkPeer, TimeSpan timeout, uint domainId)
    {
        var sw = Stopwatch.StartNew();
        
        return await AnsiConsole.Live(GetTable(sw.Elapsed, domainId))
            .AutoClear(true) // Clear when done
            .StartAsync(async ctx => 
            {
                while (sw.Elapsed < timeout)
                {
                    if (checkPeer())
                    {
                        return true;
                    }
                    
                    ctx.UpdateTarget(GetTable(sw.Elapsed, domainId));
                    await Task.Delay(100);
                }
                return false;
            });
    }

    private Table GetTable(TimeSpan elapsed, uint domainId)
    {
        var table = new Table().Border(TableBorder.Rounded).Expand();
        table.AddColumn(new TableColumn("[yellow]⚠ Waiting for peer connection...[/]").Centered());

        var diagnostics = new Grid();
        diagnostics.AddColumn();
        diagnostics.AddRow($"Elapsed: {elapsed.TotalSeconds:F1}s");
        diagnostics.AddRow($"Domain ID: {domainId}");
        
        var ips = DiagnosticHeader.GetLocalIPAddresses();
        var ipList = ips.Count > 0 ? string.Join(", ", ips) : "None";
        diagnostics.AddRow($"Local IP(s): {ipList}");
        
        diagnostics.AddRow("");
        diagnostics.AddRow("[grey]Tip: Check firewall settings and ensure both nodes use same Domain ID[/]");

        table.AddRow(diagnostics);
        return table;
    }
}
