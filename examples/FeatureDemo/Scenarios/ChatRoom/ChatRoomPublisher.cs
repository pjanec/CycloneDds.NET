using System;
using CycloneDDS.Runtime;
using CycloneDDS.Schema;

namespace FeatureDemo.Scenarios.ChatRoom;

public class ChatRoomPublisher : IDisposable
{
    private readonly DdsParticipant _participant;
    private readonly DdsWriter<ChatMessage> _writer;
    private long _nextMessageId = 1;
    private bool _disposed;

    public ChatRoomPublisher(DdsParticipant participant)
    {
        _participant = participant ?? throw new ArgumentNullException(nameof(participant));
        _writer = new DdsWriter<ChatMessage>(_participant, "ChatMessage");
        
        // Ensure sender tracking is enabled on the participant to allow subscribers 
        // to identify who sent what. Though this is a participant-level setting,
        // it's good practice to ensure it's set if we rely on it.
        // However, in CycloneDDS-CSharp, sender tracking is usually automatic or 
        // handled via sample info. Let's assume standard behavior.
    }

    public void SendMessage(string user, string content)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ChatRoomPublisher));

        var msg = new ChatMessage
        {
            MessageId = _nextMessageId++,
            User = new FixedString32(user),
            Content = new FixedString128(content),
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        _writer.Write(msg);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _writer?.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
