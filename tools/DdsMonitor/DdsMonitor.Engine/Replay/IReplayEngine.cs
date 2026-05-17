using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DdsMonitor.Engine.Replay;

/// <summary>
/// Controls the playback of a previously imported JSON sample stream.
/// </summary>
public interface IReplayEngine
{
    // ── State ────────────────────────────────────────────────────────────────

    /// <summary>Gets the current playback status.</summary>
    ReplayStatus Status { get; }

    /// <summary>
    /// Gets or sets the playback speed relative to the original capture rate.
    /// A value of <c>2.0</c> plays back at twice the original speed.
    /// </summary>
    double SpeedMultiplier { get; set; }

    /// <summary>Gets or sets whether playback loops back to the beginning.</summary>
    bool Loop { get; set; }

    /// <summary>Gets the total number of samples in the loaded recording.</summary>
    int TotalSamples { get; }

    /// <summary>
    /// Gets the number of playback-eligible samples after the active filter has been
    /// applied.  Equals <see cref="TotalSamples"/> when no filter is active.
    /// </summary>
    int FilteredTotalCount { get; }

    /// <summary>
    /// Gets the index (within the <em>filtered</em> sequence) of the sample that
    /// will be dispatched next.
    /// </summary>
    int CurrentIndex { get; }

    /// <summary>Gets the current scrubber / tracking mode.</summary>
    ReplayPlaybackMode PlaybackMode { get; set; }

    // ── Time metrics ─────────────────────────────────────────────────────────

    /// <summary>Gets the <see cref="DateTime"/> of the first sample in the recording.</summary>
    DateTime StartTime { get; }

    /// <summary>Gets the <see cref="DateTime"/> of the last sample in the recording.</summary>
    DateTime EndTime { get; }

    /// <summary>Gets the total recording duration (<see cref="EndTime"/> − <see cref="StartTime"/>).</summary>
    TimeSpan TotalDuration { get; }

    /// <summary>
    /// Gets the clock timestamp of the sample at <see cref="CurrentIndex"/>.
    /// Returns <see cref="DateTime.MinValue"/> when no samples are loaded.
    /// </summary>
    DateTime CurrentTimestamp { get; }

    /// <summary>
    /// Gets the elapsed time from <see cref="StartTime"/> to <see cref="CurrentTimestamp"/>.
    /// </summary>
    TimeSpan CurrentRelativeTime { get; }

    /// <summary>
    /// Gets the timestamp of the sample that would be dispatched next (i.e. at
    /// <see cref="CurrentIndex"/>), or <see cref="DateTime.MinValue"/> when there
    /// is no next sample.
    /// </summary>
    DateTime NextSampleTimestamp { get; }

    /// <summary>
    /// Gets the <see cref="SampleData"/> that will be dispatched on the next
    /// <see cref="Step"/> or playback tick, or <c>null</c> when the sequence is
    /// exhausted or nothing is loaded.
    /// </summary>
    SampleData? NextSample { get; }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads samples from a JSON export file.  Any active playback is stopped first.
    /// </summary>
    Task LoadAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts or resumes playback, routing each sample to <paramref name="target"/>.
    /// </summary>
    void Play(ReplayTarget target);

    /// <summary>Pauses playback at the current position.</summary>
    void Pause();

    /// <summary>Stops playback and resets <see cref="CurrentIndex"/> to zero.</summary>
    void Stop();

    // ── Stepping / Seeking ───────────────────────────────────────────────────

    /// <summary>
    /// Dispatches exactly the next sample in the filtered sequence when paused,
    /// then advances <see cref="CurrentIndex"/> by one.  No-op when playing or
    /// when there are no further samples.
    /// </summary>
    void Step(ReplayTarget target);

    /// <summary>
    /// Moves the playback position to the given zero-based frame index within the
    /// filtered sequence.  Clamped to [0, FilteredTotalCount).
    /// </summary>
    void SeekToFrame(int frameIndex);

    /// <summary>
    /// Moves the playback position to the sample whose timestamp is nearest to
    /// <see cref="StartTime"/> + <paramref name="relativeTime"/>.
    /// </summary>
    void SeekToTime(TimeSpan relativeTime);

    // ── Filtering ────────────────────────────────────────────────────────────

    /// <summary>
    /// Applies an external filter predicate that restricts which samples are eligible
    /// for playback.  Pass <c>null</c> to remove the filter and play all samples.
    /// The current <see cref="CurrentIndex"/> is reset to zero.
    /// </summary>
    void SetFilter(Func<SampleData, bool>? filterPredicate);

    // ── Events ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Raised whenever observable state changes (status, index, filter…).
    /// May be raised from a background thread.
    /// </summary>
    event Action? StateChanged;
}
