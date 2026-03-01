using System;
using System.Collections.Generic;
using System.Reflection;
using CycloneDDS.Runtime;

namespace DdsMonitor.Engine;

/// <summary>
/// Coordinates DDS readers and writers for dynamic topic types.
/// </summary>
public sealed class DdsBridge : IDdsBridge
{
    private const int EmptyCount = 0;

    private readonly object _sync = new();
    private readonly Dictionary<Type, IDynamicReader> _activeReaders = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="DdsBridge"/> class.
    /// </summary>
    public DdsBridge(string? initialPartition = null)
    {
        Participant = new DdsParticipant();
        CurrentPartition = initialPartition;
    }

    /// <inheritdoc />
    public DdsParticipant Participant { get; }

    /// <inheritdoc />
    public string? CurrentPartition { get; private set; }

    /// <inheritdoc />
    public IReadOnlyDictionary<Type, IDynamicReader> ActiveReaders
    {
        get
        {
            lock (_sync)
            {
                return new Dictionary<Type, IDynamicReader>(_activeReaders);
            }
        }
    }

    /// <inheritdoc />
    public IDynamicReader Subscribe(TopicMetadata meta)
    {
        return TrySubscribe(meta, out var reader, out var errorMessage)
            ? reader!
            : new NullDynamicReader(meta, errorMessage);
    }

    /// <inheritdoc />
    public bool TrySubscribe(TopicMetadata meta, out IDynamicReader? reader, out string? errorMessage)
    {
        if (meta == null)
        {
            throw new ArgumentNullException(nameof(meta));
        }

        lock (_sync)
        {
            ThrowIfDisposed();

            reader = null;
            errorMessage = null;

            if (_activeReaders.TryGetValue(meta.TopicType, out var existing))
            {
                reader = existing;
                return true;
            }

            try
            {
                reader = CreateReader(meta);
                reader.Start(CurrentPartition);
                _activeReaders[meta.TopicType] = reader;
                return true;
            }
            catch (Exception ex) when (TryGetDescriptorError(ex, out var message))
            {
                reader?.Dispose();
                reader = null;
                errorMessage = message;
                return false;
            }
        }
    }

    /// <inheritdoc />
    public void Unsubscribe(TopicMetadata meta)
    {
        if (meta == null)
        {
            throw new ArgumentNullException(nameof(meta));
        }

        lock (_sync)
        {
            ThrowIfDisposed();

            if (!_activeReaders.TryGetValue(meta.TopicType, out var reader))
            {
                return;
            }

            _activeReaders.Remove(meta.TopicType);
            reader.Dispose();
        }
    }

    /// <inheritdoc />
    public IDynamicWriter GetWriter(TopicMetadata meta)
    {
        if (meta == null)
        {
            throw new ArgumentNullException(nameof(meta));
        }

        lock (_sync)
        {
            ThrowIfDisposed();
            return CreateWriter(meta);
        }
    }

    /// <inheritdoc />
    public void ChangePartition(string? newPartition)
    {
        lock (_sync)
        {
            ThrowIfDisposed();

            if (_activeReaders.Count == EmptyCount)
            {
                CurrentPartition = newPartition;
                return;
            }

            var metas = new List<TopicMetadata>(_activeReaders.Count);
            foreach (var reader in _activeReaders.Values)
            {
                metas.Add(reader.TopicMetadata);
                reader.Dispose();
            }

            _activeReaders.Clear();
            CurrentPartition = newPartition;

            foreach (var meta in metas)
            {
                var reader = CreateReader(meta);
                reader.Start(CurrentPartition);
                _activeReaders[meta.TopicType] = reader;
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            foreach (var reader in _activeReaders.Values)
            {
                reader.Dispose();
            }

            _activeReaders.Clear();
            Participant.Dispose();
        }
    }

    private IDynamicReader CreateReader(TopicMetadata meta)
    {
        var readerType = typeof(DynamicReader<>).MakeGenericType(meta.TopicType);
        var instance = Activator.CreateInstance(readerType, Participant, meta, CurrentPartition);

        if (instance == null)
        {
            throw new InvalidOperationException($"Unable to create reader for '{meta.TopicType.Name}'.");
        }

        return (IDynamicReader)instance;
    }

    private IDynamicWriter CreateWriter(TopicMetadata meta)
    {
        var writerType = typeof(DynamicWriter<>).MakeGenericType(meta.TopicType);
        var instance = Activator.CreateInstance(writerType, Participant, meta, CurrentPartition);

        if (instance == null)
        {
            throw new InvalidOperationException($"Unable to create writer for '{meta.TopicType.Name}'.");
        }

        return (IDynamicWriter)instance;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(DdsBridge));
        }
    }

    private static bool TryGetDescriptorError(Exception exception, out string? message)
    {
        if (exception is TargetInvocationException invocationException && invocationException.InnerException != null)
        {
            return TryGetDescriptorError(invocationException.InnerException, out message);
        }

        if (exception is InvalidOperationException invalidOperationException)
        {
            message = invalidOperationException.Message;
            return true;
        }

        message = null;
        return false;
    }
}
