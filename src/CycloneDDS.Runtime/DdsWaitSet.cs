using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using CycloneDDS.Runtime.Interop;

namespace CycloneDDS.Runtime
{
    /// <summary>
    /// Zero-allocation DDS WaitSet that allows a monitoring loop to sleep on multiple readers
    /// simultaneously on a single OS thread.
    /// </summary>
    public sealed class DdsWaitSet : IDisposable
    {
        private readonly DdsEntityHandle _waitsetHandle;
        private readonly DdsEntityHandle _guardCondition;

        // Maps IDdsReader → (ReadCondition handle, GCHandle used as attach_arg)
        private readonly Dictionary<IDdsReader, (DdsEntityHandle Condition, GCHandle Gc)> _conditions = new();

        // Maps attach_arg (= GCHandle pointer) → IDdsReader for O(1) reverse lookup
        private readonly Dictionary<IntPtr, IDdsReader> _attachMap = new();

        // Sentinel passed as attach_arg for the guard — lets Wait() skip guard wake-ups
        private readonly IntPtr _guardAttachArg = new IntPtr(-1);

        private readonly object _lock = new();
        private bool _disposed;

        /// <summary>Creates a new WaitSet associated with <paramref name="participant"/>.</summary>
        public DdsWaitSet(DdsParticipant participant)
        {
            _waitsetHandle = new DdsEntityHandle(DdsApi.dds_create_waitset(participant.NativeEntity));
            _guardCondition = new DdsEntityHandle(DdsApi.dds_create_guardcondition(participant.NativeEntity));
            DdsApi.dds_waitset_attach(_waitsetHandle.NativeHandle, _guardCondition.NativeHandle, _guardAttachArg);
        }

        /// <summary>
        /// Attaches <paramref name="reader"/> to this WaitSet. Idempotent — attaching the same reader
        /// twice has no effect.
        /// </summary>
        public void Attach(IDdsReader reader)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            if (reader is not IInternalDdsEntity entity)
                throw new ArgumentException("Reader must implement IInternalDdsEntity.", nameof(reader));

            lock (_lock)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(DdsWaitSet));
                if (_conditions.ContainsKey(reader)) return;

                var conditionEntity = DdsApi.dds_create_readcondition(entity.NativeEntity, uint.MaxValue);
                var condition = new DdsEntityHandle(conditionEntity);

                var gc = GCHandle.Alloc(reader);
                var attachArg = GCHandle.ToIntPtr(gc);

                DdsApi.dds_waitset_attach(_waitsetHandle.NativeHandle, condition.NativeHandle, attachArg);

                _conditions[reader] = (condition, gc);
                _attachMap[attachArg] = reader;
            }
        }

        /// <summary>
        /// Detaches <paramref name="reader"/> from this WaitSet. No-op if the reader is not attached.
        /// </summary>
        public void Detach(IDdsReader reader)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));

            lock (_lock)
            {
                if (_disposed) return;
                if (!_conditions.TryGetValue(reader, out var entry)) return;

                DdsApi.dds_waitset_detach(_waitsetHandle.NativeHandle, entry.Condition.NativeHandle);

                var attachArg = GCHandle.ToIntPtr(entry.Gc);
                _attachMap.Remove(attachArg);

                entry.Condition.Dispose();
                entry.Gc.Free();

                _conditions.Remove(reader);
            }
        }

        /// <summary>
        /// Blocks until at least one attached reader has data, the <paramref name="timeout"/> expires,
        /// or <paramref name="cancellationToken"/> is cancelled. Zero-allocation hot path.
        /// </summary>
        /// <param name="triggeredReaders">
        /// Caller-allocated span. Triggered readers are written into it from index 0.
        /// </param>
        /// <param name="timeout">
        /// How long to wait. Pass <see cref="Timeout.InfiniteTimeSpan"/> to wait indefinitely.
        /// </param>
        /// <param name="cancellationToken">Optional token to interrupt the wait.</param>
        /// <returns>Number of triggered readers written into <paramref name="triggeredReaders"/>.</returns>
        public int Wait(Span<IDdsReader> triggeredReaders, TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DdsWaitSet));

            int capacity = triggeredReaders.Length + 1; // +1 to accommodate guard
            IntPtr[] buffer = ArrayPool<IntPtr>.Shared.Rent(capacity);

            try
            {
                // Pre-trigger the guard immediately if already cancelled
                if (cancellationToken.IsCancellationRequested)
                {
                    DdsApi.dds_set_guardcondition(_guardCondition.NativeHandle, true);
                    throw new OperationCanceledException(cancellationToken);
                }

                long nativeTimeout = (timeout == Timeout.InfiniteTimeSpan || timeout.Ticks < 0)
                    ? DdsApi.DDS_INFINITY
                    : (long)timeout.TotalNanoseconds;

                CancellationTokenRegistration reg = default;
                if (cancellationToken.CanBeCanceled)
                {
                    reg = cancellationToken.Register(static state =>
                    {
                        var self = (DdsWaitSet)state!;
                        if (!self._disposed)
                            DdsApi.dds_set_guardcondition(self._guardCondition.NativeHandle, true);
                    }, this);
                }

                int nTriggered;
                try
                {
                    nTriggered = DdsApi.dds_waitset_wait(
                        _waitsetHandle.NativeHandle,
                        buffer,
                        (UIntPtr)capacity,
                        nativeTimeout);
                }
                finally
                {
                    reg.Dispose();
                }

                if (cancellationToken.IsCancellationRequested)
                    throw new OperationCanceledException(cancellationToken);

                if (nTriggered <= 0)
                    return 0;

                int count = 0;
                for (int i = 0; i < nTriggered && count < triggeredReaders.Length; i++)
                {
                    IntPtr arg = buffer[i];
                    if (arg == _guardAttachArg) continue; // skip guard sentinel

                    lock (_lock)
                    {
                        if (_attachMap.TryGetValue(arg, out IDdsReader? reader))
                        {
                            triggeredReaders[count++] = reader;
                        }
                    }
                }

                return count;
            }
            finally
            {
                ArrayPool<IntPtr>.Shared.Return(buffer);
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed) return;
                _disposed = true;

                // Detach and dispose the guard condition
                DdsApi.dds_waitset_detach(_waitsetHandle.NativeHandle, _guardCondition.NativeHandle);
                _guardCondition.Dispose();

                // Detach and dispose all reader conditions
                foreach (var kvp in _conditions)
                {
                    DdsApi.dds_waitset_detach(_waitsetHandle.NativeHandle, kvp.Value.Condition.NativeHandle);
                    kvp.Value.Condition.Dispose();
                    kvp.Value.Gc.Free();
                }
                _conditions.Clear();
                _attachMap.Clear();

                _waitsetHandle.Dispose();
            }

            GC.SuppressFinalize(this);
        }
    }
}
