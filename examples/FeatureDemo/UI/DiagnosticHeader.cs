using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using FeatureDemo.Orchestration;
using Spectre.Console;

namespace FeatureDemo.UI;

public class DiagnosticHeader
{
    private readonly DemoMode _mode;
    private readonly uint _domainId;

    public DiagnosticHeader(DemoMode mode, uint domainId)
    {
        _mode = mode;
        _domainId = domainId;
    }

    public void Render()
    {
        var ips = GetLocalIPAddresses();
        var ipDisplay = ips.Any() ? string.Join(", ", ips) : "No Network";
        
        // We can't easily get peer count without a participant passed in, 
        // but for now we'll just show static info or maybe pass in peer count later if needed.
        // For the static header as designed, we'll keep it simple.

        var table = new Table();
        table.Border = TableBorder.Rounded;
        table.Expand();
        table.AddColumn("Diagnostics");

        var modeColor = _mode switch
        {
            DemoMode.Master => "green",
            DemoMode.Slave => "blue",
            DemoMode.Standalone => "yellow",
            _ => "white"
        };

        var content = $"[bold]Mode:[/] [{modeColor}]{_mode}[/] | [bold]IP:[/] {ipDisplay} | [bold]Domain:[/] {_domainId}";
        
        table.AddRow(content);
        AnsiConsole.Write(table);
    }

    public static List<string> GetLocalIPAddresses()
    {
        var output = new List<string>();
        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
             if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                 networkInterface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
             {
                 if (networkInterface.OperationalStatus == OperationalStatus.Up)
                 {
                     foreach (var ip in networkInterface.GetIPProperties().UnicastAddresses)
                     {
                         if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                         {
                             output.Add(ip.Address.ToString());
                         }
                     }
                 }
             }
        }
        return output;
    }
}
