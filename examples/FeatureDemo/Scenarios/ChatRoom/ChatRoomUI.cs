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

        // Offset the bubble to its side by padding the other side. Only when the console is
        // wide enough: on a narrow (or redirected) console the padding would leave no room
        // and Spectre.Console throws, so the bubble is printed as is.
        const int BubbleOffset = 20;
        if (AnsiConsole.Profile.Width < BubbleOffset * 3)
        {
             AnsiConsole.Write(panel);
        }
        else if (isMe)
        {
             AnsiConsole.Write(new Padder(panel).PadLeft(BubbleOffset));
        }
        else
        {
             AnsiConsole.Write(new Padder(panel).PadRight(BubbleOffset));
        }
    }
}
