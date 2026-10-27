namespace CycloneDDS.Schema;

/// <summary>
/// Specifies the liveliness QoS policy kind: how a writer shows it is still alive.
/// </summary>
public enum DdsLivelinessKind
{
    /// <summary>
    /// The DDS infrastructure asserts liveliness automatically while the writer's participant runs.
    /// </summary>
    Automatic = 0,

    /// <summary>
    /// Asserted by any write on, or an explicit assert of, any writer of the participant.
    /// </summary>
    ManualByParticipant = 1,

    /// <summary>
    /// Asserted only by writing on, or explicitly asserting, this particular writer.
    /// </summary>
    ManualByTopic = 2
}
