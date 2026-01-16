using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace CycloneDDS.Schema;

/// <summary>
/// A bounded sequence of items with a fixed maximum capacity.
/// <para><b>WARNING:</b> This is a struct wrapping a reference type. 
/// Copying the struct creates a shallow copy that shares the underlying storage.
/// Mutations to the copied struct will affect the original.</para>
/// </summary>
/// <typeparam name="T">The type of elements in the sequence.</typeparam>
public struct BoundedSeq<T> : IEnumerable<T>
{
    // We use List<T> as backing storage to handle the count/items management safely 
    // across struct copies (which share the reference).
    private readonly List<T> _storage;
    private readonly int _capacity;

    /// <summary>
    /// Initializes a new instance of the <see cref="BoundedSeq{T}"/> struct with the specified capacity.
    /// </summary>
    /// <param name="capacity">The maximum number of elements the sequence can hold.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when capacity is less than 0.</exception>
    public BoundedSeq(int capacity)
    {
        if (capacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be non-negative.");
        }
        _capacity = capacity;
        _storage = new List<T>(capacity);
    }

    /// <summary>
    /// Gets the maximum capacity of the sequence.
    /// </summary>
    public int Capacity => _capacity;

    /// <summary>
    /// Gets the current number of elements in the sequence.
    /// </summary>
    public int Count => _storage?.Count ?? 0;

    /// <summary>
    /// Adds an item to the sequence.
    /// </summary>
    /// <param name="item">The item to add.</param>
    /// <exception cref="InvalidOperationException">Thrown when the sequence is full.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the sequence is not initialized.</exception>
    public void Add(T item)
    {
        if (_storage == null)
        {
             throw new InvalidOperationException("BoundedSeq is not initialized. Use the constructor to set capacity.");
        }

        if (_storage.Count >= _capacity)
        {
            throw new InvalidOperationException("Sequence is full.");
        }
        _storage.Add(item);
    }

    /// <summary>
    /// Removes all items from the sequence.
    /// </summary>
    public void Clear()
    {
        _storage?.Clear();
    }

    /// <summary>
    /// Returns a Span view of the sequence elements.
    /// </summary>
    /// <returns>A Span&lt;T&gt; covering the valid elements.</returns>
    public Span<T> AsSpan()
    {
        if (_storage == null) return Span<T>.Empty;
#if NET5_0_OR_GREATER
        return CollectionsMarshal.AsSpan(_storage);
#else
        if (_storage == null) return Span<T>.Empty;
        return new Span<T>(_storage.ToArray());
#endif
    }

    /// <summary>
    /// Gets or sets the element at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the element.</param>
    /// <returns>The element at the specified index.</returns>
    public T this[int index]
    {
        get
        {
            if (_storage == null) throw new IndexOutOfRangeException();
            return _storage[index];
        }
        set
        {
            if (_storage == null) throw new IndexOutOfRangeException();
            _storage[index] = value;
        }
    }

    /// <summary>
    /// Returns an enumerator that iterates through the sequence.
    /// </summary>
    /// <returns>An enumerator for the sequence.</returns>
    public IEnumerator<T> GetEnumerator()
    {
        return (_storage ?? Enumerable.Empty<T>()).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
