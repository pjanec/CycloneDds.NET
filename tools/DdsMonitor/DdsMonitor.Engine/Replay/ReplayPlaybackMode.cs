namespace DdsMonitor.Engine.Replay;

/// <summary>
/// Determines how the scrubber and positional tracking interpret the replay position.
/// </summary>
public enum ReplayPlaybackMode
{
    /// <summary>
    /// Frames mode: the scrubber position maps linearly to the zero-based sample index
    /// within the filtered playback sequence.
    /// </summary>
    Frames,

    /// <summary>
    /// Time mode: the scrubber position maps linearly to elapsed time from
    /// <see cref="IReplayEngine.StartTime"/> to <see cref="IReplayEngine.EndTime"/>.
    /// </summary>
    Time
}
