using System;
using CycloneDDS.Schema;

namespace CycloneDDS.Runtime
{
    /// <summary>
    /// Per-entity QoS settings that override the type-level <see cref="DdsQosAttribute"/>.
    /// Every property is optional; <c>null</c> keeps the attribute's (or the DDS default) value.
    /// </summary>
    public abstract class DdsEntityOptions
    {
        /// <summary>Overrides <see cref="DdsQosAttribute.LivelinessKind"/>.</summary>
        public DdsLivelinessKind? LivelinessKind { get; set; }

        /// <summary>
        /// Overrides <see cref="DdsQosAttribute.LivelinessLeaseDurationMs"/>;
        /// <see cref="System.Threading.Timeout.InfiniteTimeSpan"/> means infinite.
        /// </summary>
        public TimeSpan? LivelinessLeaseDuration { get; set; }

        /// <summary>
        /// Overrides <see cref="DdsQosAttribute.DeadlineMs"/>;
        /// <see cref="System.Threading.Timeout.InfiniteTimeSpan"/> means infinite.
        /// </summary>
        public TimeSpan? Deadline { get; set; }
    }

    /// <summary>
    /// Per-writer QoS settings, passed to the <see cref="DdsWriter{T}"/> constructor.
    /// </summary>
    public sealed class DdsWriterOptions : DdsEntityOptions
    {
        /// <summary>
        /// The writer's ownership strength. With <see cref="DdsOwnershipKind.Exclusive"/> ownership
        /// the strongest alive writer owns each instance (e.g. primary 100, backup 50).
        /// </summary>
        public int? OwnershipStrength { get; set; }

        /// <summary>Overrides <see cref="DdsQosAttribute.AutodisposeUnregisteredInstances"/>.</summary>
        public bool? AutodisposeUnregisteredInstances { get; set; }
    }

    /// <summary>
    /// Per-reader QoS settings, passed to the <see cref="DdsReader{T}"/> constructor.
    /// </summary>
    /// <remarks>
    /// There is no autopurge (reader data lifecycle) setting: Cyclone accepts that policy but
    /// does not act on it. It frees a reader's instance by itself once all its samples have
    /// been taken and no writer is left.
    /// </remarks>
    public sealed class DdsReaderOptions : DdsEntityOptions
    {
    }
}
