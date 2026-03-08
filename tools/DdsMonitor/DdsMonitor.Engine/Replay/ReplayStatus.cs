namespace DdsMonitor.Engine.Replay;

/// <summary>
/// Describes the current operational state of the <see cref="IReplayEngine"/>.
/// </summary>
public enum ReplayStatus
{
    /// <summary>No file is loaded or playback has not started / has ended.</summary>
    Idle,

    /// <summary>Playback is actively running.</summary>
    Playing,

    /// <summary>Playback has been paused and can be resumed from the current position.</summary>
    Paused
}
