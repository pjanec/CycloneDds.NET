using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace CycloneDDS.CodeGen.Utilities
{
    /// <summary>
    /// A wrapper for ImmutableArray that implements value-based equality.
    /// Use this in your records instead of ImmutableArray.
    /// </summary>
    internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T>
        where T : IEquatable<T>
    {
        private readonly ImmutableArray<T> _array;

        public EquatableArray(ImmutableArray<T> array)
        {
            _array = array;
        }

        public static implicit operator EquatableArray<T>(ImmutableArray<T> array) => new(array);
        public static implicit operator ImmutableArray<T>(EquatableArray<T> array) => array._array;

        // Helper to create empty for initialization
        public static EquatableArray<T> Empty => new(ImmutableArray<T>.Empty);

        public bool Equals(EquatableArray<T> other)
        {
            // Handle default/uninitialized arrays safely
            if (_array.IsDefault && other._array.IsDefault) return true;
            if (_array.IsDefault || other._array.IsDefault) return false;
            
            return _array.SequenceEqual(other._array);
        }

        public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

        public override int GetHashCode()
        {
            if (_array.IsDefaultOrEmpty) return 0;
            
            int hashCode = 0;
            foreach (var item in _array)
            {
                hashCode = (hashCode * 397) ^ (item?.GetHashCode() ?? 0);
            }
            return hashCode;
        }

        public IEnumerator<T> GetEnumerator() => (_array.IsDefault ? Enumerable.Empty<T>() : _array).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
