using System;
using System.Reflection;
using System.Threading;
using CycloneDDS.Runtime.Interop;
using CycloneDDS.Schema;

namespace CycloneDDS.Runtime
{
    /// <summary>
    /// Builds the native QoS for a reader or writer from <see cref="DdsQosAttribute"/> on the
    /// topic type plus optional per-entity overrides. One QoS serves the topic, reader and
    /// writer alike: Cyclone only takes the policies that apply to each entity kind.
    /// </summary>
    internal static class DdsQosBuilder
    {
        /// <summary>Creates a new native QoS; the caller deletes it.</summary>
        internal static IntPtr Create(Type topicType, DdsEntityOptions? options)
        {
            IntPtr qos = DdsApi.dds_create_qos();
            var qosAttr = topicType.GetCustomAttribute<DdsQosAttribute>();

            if (qosAttr != null)
            {
                // Default max_blocking_time is 100ms (100 million nanoseconds)
                long maxBlockingTime = 100 * 1000 * 1000;
                DdsApi.dds_qset_reliability(qos, (int)qosAttr.Reliability, maxBlockingTime);
                DdsApi.dds_qset_durability(qos, (int)qosAttr.Durability);
                int depth = qosAttr.HistoryDepth;
                if (qosAttr.HistoryKind == DdsHistoryKind.KeepAll)
                {
                    depth = -1; // DDS_LENGTH_UNLIMITED
                    DdsApi.dds_qset_resource_limits(qos, -1, -1, -1);
                }
                DdsApi.dds_qset_history(qos, (int)qosAttr.HistoryKind, depth);

                // The policies below are only set when they differ from the DDS default.
                if (!qosAttr.AutodisposeUnregisteredInstances)
                    DdsApi.dds_qset_writer_data_lifecycle(qos, false);
                if (qosAttr.Ownership != DdsOwnershipKind.Shared)
                    DdsApi.dds_qset_ownership(qos, (int)qosAttr.Ownership);
                if (qosAttr.LivelinessKind != DdsLivelinessKind.Automatic || qosAttr.LivelinessLeaseDurationMs >= 0)
                    DdsApi.dds_qset_liveliness(qos, (int)qosAttr.LivelinessKind, MillisecondsToDuration(qosAttr.LivelinessLeaseDurationMs));
                if (qosAttr.DeadlineMs >= 0)
                    DdsApi.dds_qset_deadline(qos, MillisecondsToDuration(qosAttr.DeadlineMs));
            }

            if (options != null)
            {
                if (options.LivelinessKind != null || options.LivelinessLeaseDuration != null)
                {
                    var kind = options.LivelinessKind ?? qosAttr?.LivelinessKind ?? DdsLivelinessKind.Automatic;
                    long lease = options.LivelinessLeaseDuration is TimeSpan l
                        ? ToDuration(l)
                        : MillisecondsToDuration(qosAttr?.LivelinessLeaseDurationMs ?? -1);
                    DdsApi.dds_qset_liveliness(qos, (int)kind, lease);
                }
                if (options.Deadline is TimeSpan deadline)
                    DdsApi.dds_qset_deadline(qos, ToDuration(deadline));

                if (options is DdsWriterOptions w)
                {
                    if (w.OwnershipStrength is int strength)
                        DdsApi.dds_qset_ownership_strength(qos, strength);
                    if (w.AutodisposeUnregisteredInstances is bool autodispose)
                        DdsApi.dds_qset_writer_data_lifecycle(qos, autodispose);
                }
            }

            return qos;
        }

        /// <summary>
        /// Converts to a native dds_duration_t (nanoseconds);
        /// <see cref="Timeout.InfiniteTimeSpan"/>, any negative value or an overflow is infinite.
        /// </summary>
        internal static long ToDuration(TimeSpan timeout) =>
            (timeout == Timeout.InfiniteTimeSpan || timeout.Ticks < 0 || timeout.Ticks > long.MaxValue / 100)
                ? DdsApi.DDS_INFINITY
                : timeout.Ticks * 100;

        private static long MillisecondsToDuration(int milliseconds) =>
            milliseconds < 0 ? DdsApi.DDS_INFINITY : milliseconds * 1_000_000L;
    }
}
