using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace CycloneDDS.CodeGen.Utilities
{
    // Constrain T to IEquatable<T> to ensure we use the custom value comparison
    internal class ImmutableArraySequenceEqualComparer<T> : IEqualityComparer<ImmutableArray<T>>
        where T : IEquatable<T>
    {
        public bool Equals(ImmutableArray<T> x, ImmutableArray<T> y)
        {
            // Handle default (uninitialized) arrays
            if (x.IsDefault && y.IsDefault) return true;
            if (x.IsDefault || y.IsDefault) return false;

            // Fast length check
            if (x.Length != y.Length) return false;

            // Manual loop to guarantee IEquatable<T>.Equals is called
            for (int i = 0; i < x.Length; i++)
            {
                if (!x[i].Equals(y[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public int GetHashCode(ImmutableArray<T> obj)
        {
            if (obj.IsDefault) return 0;

            int hash = 17;
            foreach (var item in obj)
            {
                hash = hash * 31 + (item?.GetHashCode() ?? 0);
            }
            return hash;
        }
    }
}
