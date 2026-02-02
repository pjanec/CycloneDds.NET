using Spectre.Console;
using System.Collections.Generic;

namespace FeatureDemo.Scenarios.BlackBox;

public class BlackBoxUI
{
    public void DisplayLogs(List<SystemLog> logs)
    {
        var table = new Table();
        table.AddColumn("ID");
        table.AddColumn("Level");
        table.AddColumn("Component");
        table.AddColumn("Message");
        table.AddColumn("Timestamp");

        foreach (var log in logs)
        {
            var color = log.Level switch
            {
                LogLevel.Info => "green",
                LogLevel.Warning => "yellow",
                LogLevel.Error => "red",
                LogLevel.Critical => "red bold reverse",
                _ => "white"
            };

            table.AddRow(
                log.LogId.ToString(),
                $"[{color}]{log.Level}[/]",
                log.Component.ToString(),
                log.Message.ToString(),
                log.Timestamp.ToString()
            );
        }

        AnsiConsole.Write(table);
    }
}
