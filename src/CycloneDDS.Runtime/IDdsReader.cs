using System;
using CycloneDDS.Runtime.Interop;

namespace CycloneDDS.Runtime
{
    /// <summary>
    /// Type-erased public handle to any <see cref="DdsReader{T}"/>, usable by consumer code without generics.
    /// </summary>
    public interface IDdsReader
    {
        /// <summary>The managed type of the topic data.</summary>
        Type DataType { get; }
    }

    /// <summary>
    /// Internal interface that exposes the raw native DDS entity handle.
    /// Used inside <see cref="DdsWaitSet"/> without leaking native handles to public consumers.
    /// </summary>
    internal interface IInternalDdsEntity
    {
        DdsApi.DdsEntity NativeEntity { get; }
    }
}
