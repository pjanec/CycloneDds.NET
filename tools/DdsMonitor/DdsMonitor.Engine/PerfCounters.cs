using System;
using System.Threading;

namespace DdsMonitor.Engine;

/// <summary>
/// Lightweight singleton that accumulates per-second performance metrics for the
/// hot sample-ingestion path.  All methods are lock-free and safe to call from
/// any thread.  The 1-second snapshot is updated by <see cref="Snapshot"/> which
/// is called by the bandwidth timer in MainLayout.
/// </summary>
public sealed class PerfCounters
{
    private long _samplesIngested;
    private long _bytesIngested;
    private long _instanceStoreProcessed;
    private long _journalTrimCount;

    // Snapshot values updated once per second.
    private long _snapshotSamplesPerSec;
    private long _snapshotBytesPerSec;
    private long _snapshotInstanceOpsPerSec;
    private long _lastSamplesIngested;
    private long _lastBytesIngested;
    private long _lastInstanceOps;

    /// <summary>Increments the sample-ingested counter (call once per sample in DdsIngestionService).</summary>
    public void IncrementSamplesIngested(long bytes)
    {
        Interlocked.Increment(ref _samplesIngested);
        Interlocked.Add(ref _bytesIngested, bytes);
    }

    /// <summary>Increments the instance-store-processed counter (call once per keyed sample).</summary>
    public void IncrementInstanceStoreOps()
    {
        Interlocked.Increment(ref _instanceStoreProcessed);
    }

    /// <summary>Increments the journal-trim counter.</summary>
    public void IncrementJournalTrims()
    {
        Interlocked.Increment(ref _journalTrimCount);
    }

    /// <summary>Refreshes the per-second snapshot.  Call once per second from any thread.</summary>
    public void Tick()
    {
        var s = Interlocked.Read(ref _samplesIngested);
        var b = Interlocked.Read(ref _bytesIngested);
        var i = Interlocked.Read(ref _instanceStoreProcessed);

        Volatile.Write(ref _snapshotSamplesPerSec, s - _lastSamplesIngested);
        Volatile.Write(ref _snapshotBytesPerSec, b - _lastBytesIngested);
        Volatile.Write(ref _snapshotInstanceOpsPerSec, i - _lastInstanceOps);

        _lastSamplesIngested = s;
        _lastBytesIngested = b;
        _lastInstanceOps = i;
    }

    // ── Read-only snapshot properties (safe to call from any thread) ────────

    public long SamplesPerSec => Volatile.Read(ref _snapshotSamplesPerSec);
    public long BytesPerSec => Volatile.Read(ref _snapshotBytesPerSec);
    public long InstanceOpsPerSec => Volatile.Read(ref _snapshotInstanceOpsPerSec);
    public long TotalSamplesIngested => Interlocked.Read(ref _samplesIngested);
    public long TotalJournalTrims => Interlocked.Read(ref _journalTrimCount);
}
