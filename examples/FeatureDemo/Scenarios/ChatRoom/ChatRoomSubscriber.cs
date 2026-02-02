using System;
using System.Threading;
using System.Threading.Tasks;
using CycloneDDS.Runtime;
using CycloneDDS.Schema;

namespace FeatureDemo.Scenarios.ChatRoom;

public class ChatRoomSubscriber : IDisposable
{
    private readonly DdsParticipant _participant;
    private readonly DdsReader<ChatMessage> _reader;
    private bool _disposed;

    public ChatRoomSubscriber(DdsParticipant participant)
    {
        _participant = participant ?? throw new ArgumentNullException(nameof(participant));
        _reader = new DdsReader<ChatMessage>(_participant, "ChatMessage");
    }

    /// <summary>
    /// Waits for messages and invokes the callback for each received message.
    /// </summary>
    /// <param name="callback">Action taking the message and sender info. Returns true to continue, false to stop.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task WaitForMessagesAsync(Func<ChatMessage?, string, bool> callback, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (_disposed) return;

            // Wait for data with a small timeout to allow cancellation check
            // Note: WaitData is blocking, so we run it on a thread or use a small timeout + loop
            // For a demo, polling with small sleep + Take() is often simpler if WaitData async isn't available
            
            // Checking if we have data
            bool hasData = false;
            try 
            {
                // Simple polling for the demo since WaitData overload with timespan might be missing
                // or we can use Read(1) to check check count.
                using var check = _reader.Read(1);
                hasData = check.Count > 0;
            }
            catch {}

            if (hasData)
            {
                 var samples = _reader.Take();
                 try 
                 {
                     foreach (var sample in samples)
                     {
                         if (sample.IsValid)
                         {
                             // In a real app we might lookup the publication handle to get more info
                             // For now we just use the PublicationHandle as a string ID
                             var senderInfo = $"Pub-{sample.Info.PublicationHandle}";
                             
                             if (!callback(sample.Data, senderInfo))
                             {
                                 return;
                             }
                         }
                     }
                 }
                 finally
                 {
                     samples.Dispose();
                 }
            }
            else
            {
                await Task.Delay(50, ct); 
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _reader?.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
