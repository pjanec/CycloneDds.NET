using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using CycloneDDS.Core;
using CycloneDDS.Schema;

namespace RoundtripTests
{
    public enum Color
    {
        RED,
        GREEN,
        BLUE,
        YELLOW
    }

    public enum Priority
    {
        LOW,
        MEDIUM,
        HIGH,
        CRITICAL
    }

    [DdsStruct]
    public partial struct Point2D
    {
        public double X { get; set; }
        public double Y { get; set; }
    }

    [DdsStruct]
    public partial struct Point3D
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }

    [DdsStruct]
    public partial struct Timestamp
    {
        public long Seconds { get; set; }
        public uint Nanoseconds { get; set; }
    }

    [DdsStruct]
    public partial struct Location
    {
        [DdsKey]
        public int Building { get; set; }
        [DdsKey]
        public short Floor { get; set; }
        public int Room { get; set; }
    }

    [DdsTopic("AllPrimitives")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct AllPrimitives
    {
        [DdsKey]
        public int Id { get; set; }

        public bool Bool_field { get; set; }
        public byte Char_field { get; set; }
        public byte Octet_field { get; set; }
        public short Short_field { get; set; }
        public ushort Ushort_field { get; set; }
        public int Long_field { get; set; }
        public uint Ulong_field { get; set; }
        public long Llong_field { get; set; }
        public ulong Ullong_field { get; set; }
        public float Float_field { get; set; }
        public double Double_field { get; set; }
    }

    [DdsTopic("CompositeKey")]
    [DdsManaged]
    public partial struct CompositeKey
    {
        [DdsKey]
        public string Region { get; set; } = string.Empty;
        [DdsKey]
        public int Zone { get; set; }
        [DdsKey]
        public short Sector { get; set; }

        public string Name { get; set; } = string.Empty;
        public double Value { get; set; }
        public Priority Priority { get; set; }

        public CompositeKey() 
        {
            Region = string.Empty;
            Name = string.Empty;
        }
    }

    [DdsTopic("NestedKeyTopic")]
    [DdsManaged]
    public partial struct NestedKeyTopic
    {
        [DdsKey]
        public Location Location { get; set; } = new Location();

        public string Description { get; set; } = string.Empty;
        public double Temperature { get; set; }
        public Timestamp Last_updated { get; set; } = new Timestamp();

        public NestedKeyTopic()
        {
            Location = new Location();
            Description = string.Empty;
            Last_updated = new Timestamp();
        }
    }

    [DdsTopic("SequenceTopic")]
    [DdsManaged]
    public partial struct SequenceTopic
    {
        [DdsKey]
        public int Id { get; set; }

        public List<int> Unbounded_long_seq { get; set; }
        public List<int> Bounded_long_seq { get; set; }
        public List<double> Unbounded_double_seq { get; set; }
        public List<string> String_seq { get; set; }
        public List<Color> Color_seq { get; set; }

        public SequenceTopic()
        {
            Unbounded_long_seq = new List<int>();
            Bounded_long_seq = new List<int>();
            Unbounded_double_seq = new List<double>();
            String_seq = new List<string>();
            Color_seq = new List<Color>();
        }
    }
}
