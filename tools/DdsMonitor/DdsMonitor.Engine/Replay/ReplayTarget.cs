namespace DdsMonitor.Engine.Replay;

/// <summary>
/// Determines where replayed samples are routed.
/// </summary>
public enum ReplayTarget
{
    /// <summary>
    /// Samples are appended to the in-process <see cref="ISampleStore"/> for local GUI inspection.
    /// </summary>
    LocalStore,

    /// <summary>
    /// Samples are written back to the live DDS network via <see cref="IDdsBridge.GetWriter"/>.
    /// </summary>
    DdsNetwork
}
