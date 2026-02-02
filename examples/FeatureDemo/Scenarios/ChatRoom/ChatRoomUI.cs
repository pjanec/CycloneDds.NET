using System;
using Spectre.Console;

namespace FeatureDemo.Scenarios.ChatRoom;

public class ChatRoomUI
{
    private readonly string _localUser;

    public ChatRoomUI(string localUser = "Me")
    {
        _localUser = localUser;
    }

    public void DisplayMessage(ChatMessage message, string senderInfo)
    {
        var user = message.User.ToString();
        var content = message.Content.ToString();
        var time = DateTimeOffset.FromUnixTimeMilliseconds(message.Timestamp).ToString("HH:mm:ss");

        var isMe = user == _localUser;
        var color = isMe ? "green" : "blue";
        var align = isMe ? Justify.Right : Justify.Left;

        // Using Panel for a chat bubble effect
        var panel = new Panel($"[{color}]{content}[/]")
            .Header($"[bold]{user}[/] @ {time} ([grey]{senderInfo}[/])", align)
            .BorderColor(isMe ? Color.Green : Color.Blue)
            .RoundedBorder();

        // Workaround for alignment since Panel alignment in console is tricky without a Padder
        // or Grid. We'll simply print it.
        
        // For right alignment (Me), we can pad left.
        if (isMe)
        {
             AnsiConsole.Write(new Padder(panel).PadLeft(20));
        }
        else
        {
             AnsiConsole.Write(new Padder(panel).PadRight(20));
        }
    }
}
