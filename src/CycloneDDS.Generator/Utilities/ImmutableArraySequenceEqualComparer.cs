using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace CycloneDDS.Generator.Utilities
{
    internal class ImmutableArraySequenceEqualComparer<T> : IEqualityComparer<ImmutableArray<T>>
    {
        public bool Equals(ImmutableArray<T> x, ImmutableArray<T> y)
        {
            if (x.IsDefault && y.IsDefault) return true;
            if (x.IsDefault || y.IsDefault) return false;
            return x.SequenceEqual(y);
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
