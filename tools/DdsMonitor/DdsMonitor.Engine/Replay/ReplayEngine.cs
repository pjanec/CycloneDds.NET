using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DdsMonitor.Engine.Import;

namespace DdsMonitor.Engine.Replay;

/// <summary>
/// Controls the playback of a previously imported JSON sample stream, routing
/// each sample to either the local <see cref="ISampleStore"/> (GUI investigation)
/// or the live DDS network via <see cref="IDdsBridge.GetWriter"/>.
///
/// Filtering (DMON-048):
///   A caller (typically the Replay Samples Panel) may register a filter predicate
///   via <see cref="SetFilter"/>.  The engine materialises a <c>_filteredSamples</c>
///   list from the raw loaded data and drives all playback, seeking, and stepping
///   operations against that subset.  Removing the filter restores the full set.
///
/// Playback modes (DMON-049):
///   <see cref="ReplayPlaybackMode.Frames"/> - the scrubber position is a zero-based
///   index into the filtered sequence.
///   <see cref="ReplayPlaybackMode.Time"/>   - the scrubber position is a linear
///   interpolation of <see cref="TotalDuration"/>.
///   Mode only affects what the UI reports and how <see cref="SeekToTime"/> maps
///   user input; the internal playback loop always advances sample-by-sample and
///   uses real inter-sample timestamps for pacing.
/// </summary>
public sealed class ReplayEngine : IReplayEngine, IDisposable
{
    private readonly IImportService _importService;
    private readonly ISampleStore _store;
    private readonly IDdsBridge _bridge;

    private List<SampleData> _samples = new();
    private List<SampleData> _filteredSamples = new();
    private Func<SampleData, bool>? _filterPredicate;
    private CancellationTokenSource? _playCts;
    private int _currentIndex;
    private bool _disposed;

    public ReplayEngine(IImportService importService, ISampleStore store, IDdsBridge bridge)
    {
        _importService = importService ?? throw new ArgumentNullException(nameof(importService));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
    }

    public ReplayStatus Status { get; private set; } = ReplayStatus.Idle;
    public double SpeedMultiplier { get; set; } = 1.0;
    public bool Loop { get; set; }
    public ReplayPlaybackMode PlaybackMode { get; set; } = ReplayPlaybackMode.Frames;
    public int TotalSamples => _samples.Count;
    public int FilteredTotalCount => _filteredSamples.Count;
    public int CurrentIndex => Volatile.Read(ref _currentIndex);

    public DateTime StartTime => _samples.Count > 0 ? _samples[0].Timestamp : DateTime.MinValue;
    public DateTime EndTime   => _samples.Count > 0 ? _samples[^1].Timestamp : DateTime.MinValue;
    public TimeSpan TotalDuration => _samples.Count > 1 ? EndTime - StartTime : TimeSpan.Zero;

    public DateTime CurrentTimestamp
    {
        get
        {
            var idx = Volatile.Read(ref _currentIndex);
            return idx < _filteredSamples.Count ? _filteredSamples[idx].Timestamp : DateTime.MinValue;
        }
    }

    public TimeSpan CurrentRelativeTime
    {
        get
        {
            var ts = CurrentTimestamp;
            return ts == DateTime.MinValue ? TimeSpan.Zero : ts - StartTime;
        }
    }

    public DateTime NextSampleTimestamp
    {
        get
        {
            var idx = Volatile.Read(ref _currentIndex);
            return idx < _filteredSamples.Count ? _filteredSamples[idx].Timestamp : DateTime.MinValue;
        }
    }

    public SampleData? NextSample
    {
        get
        {
            var idx = Volatile.Read(ref _currentIndex);
            return idx < _filteredSamples.Count ? _filteredSamples[idx] : null;
        }
    }

    public event Action? StateChanged;

    public async Task LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        Stop();
        var loaded = new List<SampleData>();
        await foreach (var sample in _importService.ImportAsync(filePath, cancellationToken))
            loaded.Add(sample);
        _samples = loaded;
        RebuildFilter();
        Volatile.Write(ref _currentIndex, 0);
        NotifyStateChanged();
    }

    public void Play(ReplayTarget target)
    {
        if (Status == ReplayStatus.Playing || _filteredSamples.Count == 0) return;
        Status = ReplayStatus.Playing;
        NotifyStateChanged();
        _playCts = new CancellationTokenSource();
        _ = Task.Run(() => RunPlaybackAsync(target, _playCts.Token));
    }

    public void Pause()
    {
        if (Status != ReplayStatus.Playing) return;
        _playCts?.Cancel();
        Status = ReplayStatus.Paused;
        NotifyStateChanged();
    }

    public void Stop()
    {
        _playCts?.Cancel();
        _playCts = null;
        Status = ReplayStatus.Idle;
        Volatile.Write(ref _currentIndex, 0);
        NotifyStateChanged();
    }

    public void Step(ReplayTarget target)
    {
        if (Status == ReplayStatus.Playing) return;
        var idx = Volatile.Read(ref _currentIndex);
        if (idx >= _filteredSamples.Count) return;
        DispatchSingleSample(_filteredSamples[idx], target);
        Volatile.Write(ref _currentIndex, idx + 1);
        NotifyStateChanged();
    }

    public void SeekToFrame(int frameIndex)
    {
        var clamped = Math.Clamp(frameIndex, 0, Math.Max(0, _filteredSamples.Count - 1));
        Volatile.Write(ref _currentIndex, clamped);
        NotifyStateChanged();
    }

    public void SeekToTime(TimeSpan relativeTime)
    {
        if (_filteredSamples.Count == 0) return;
        var targetTs = StartTime + relativeTime;
        var lo = 0;
        var hi = _filteredSamples.Count - 1;
        while (lo < hi)
        {
            var mid = (lo + hi) / 2;
            if (_filteredSamples[mid].Timestamp < targetTs)
                lo = mid + 1;
            else
                hi = mid;
        }
        var best = lo;
        if (lo > 0)
        {
            var diffPrev = Math.Abs((_filteredSamples[lo - 1].Timestamp - targetTs).Ticks);
            var diffCurr = Math.Abs((_filteredSamples[lo].Timestamp - targetTs).Ticks);
            if (diffPrev < diffCurr)
                best = lo - 1;
        }
        Volatile.Write(ref _currentIndex, best);
        NotifyStateChanged();
    }

    public void SetFilter(Func<SampleData, bool>? filterPredicate)
    {
        _filterPredicate = filterPredicate;
        RebuildFilter();
        Volatile.Write(ref _currentIndex, 0);
        NotifyStateChanged();
    }

    private async Task RunPlaybackAsync(ReplayTarget target, CancellationToken token)
    {
        var writers = new Dictionary<Type, IDynamicWriter>();
        try
        {
            do
            {
                while (!token.IsCancellationRequested)
                {
                    var idx = Volatile.Read(ref _currentIndex);
                    if (idx >= _filteredSamples.Count) break;

                    var sample = _filteredSamples[idx];
                    DispatchSample(sample, target, writers);

                    var nextIdx = idx + 1;
                    if (nextIdx < _filteredSamples.Count)
                    {
                        var rawDelay = _filteredSamples[nextIdx].Timestamp - sample.Timestamp;
                        var speed = Math.Max(0.01, SpeedMultiplier);
                        var scaledTicks = (long)(rawDelay.Ticks / speed);
                        if (scaledTicks > 0)
                        {
                            try { await Task.Delay(TimeSpan.FromTicks(scaledTicks), token); }
                            catch (OperationCanceledException) { return; }
                        }
                    }

                    Volatile.Write(ref _currentIndex, idx + 1);
                    NotifyStateChanged();
                }

                if (Loop && !token.IsCancellationRequested)
                {
                    Volatile.Write(ref _currentIndex, 0);
                    NotifyStateChanged();
                }
                else break;
            }
            while (!token.IsCancellationRequested);
        }
        catch (OperationCanceledException) { }
        finally
        {
            foreach (var w in writers.Values) w.Dispose();
            if (Status == ReplayStatus.Playing)
            {
                Status = ReplayStatus.Idle;
                NotifyStateChanged();
            }
        }
    }

    private void DispatchSingleSample(SampleData sample, ReplayTarget target)
    {
        if (target == ReplayTarget.LocalStore)
        {
            _store.Append(sample);
        }
        else
        {
            var writer = _bridge.GetWriter(sample.TopicMetadata);
            try { writer.Write(sample.Payload); }
            finally { writer.Dispose(); }
        }
    }

    private void DispatchSample(SampleData sample, ReplayTarget target, Dictionary<Type, IDynamicWriter> writers)
    {
        if (target == ReplayTarget.LocalStore)
        {
            _store.Append(sample);
        }
        else
        {
            var topicType = sample.TopicMetadata.TopicType;
            if (!writers.TryGetValue(topicType, out var writer))
            {
                writer = _bridge.GetWriter(sample.TopicMetadata);
                writers[topicType] = writer;
            }
            writer.Write(sample.Payload);
        }
    }

    private void RebuildFilter()
    {
        if (_filterPredicate == null)
        {
            _filteredSamples = _samples;
            return;
        }
        var result = new List<SampleData>(_samples.Count);
        foreach (var s in _samples)
        {
            if (_filterPredicate(s)) result.Add(s);
        }
        _filteredSamples = result;
    }

    private void NotifyStateChanged() => StateChanged?.Invoke();

    /// <summary>
    /// Returns a read-only snapshot of the full unfiltered loaded sample list.
    /// Called by <c>ReplayPanel</c> to populate a browsing <c>SamplesPanel</c>.
    /// This is a concrete-type method (not on <see cref="IReplayEngine"/>) because
    /// it is only accessed via a direct downcast to avoid polluting the interface.
    /// </summary>
    public IReadOnlyList<SampleData> GetSnapshotForBrowsing() => _samples.AsReadOnly();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _playCts?.Cancel();
        _playCts?.Dispose();
        _playCts = null;
    }
}