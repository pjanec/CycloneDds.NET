using System;
using System.Collections.Generic;
using CycloneDDS.Schema;

namespace AtomicTests
{
    // ========================================================================
    // SECTION 13: COMPLEX INTEGRATION SCENARIOS
    // ========================================================================

    [DdsTopic("OffsetKeyTopic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct OffsetKeyTopic
    {
        [DdsManaged]
        [MaxLength(32)]
        public string GroupName;

        [DdsKey]
        public int SensorId;

        [DdsManaged]
        public float[] CalibrationData;

        [DdsKey]
        public short InstanceSubId;

        public Point3D FinalPos;
    }

    [DdsTopic("RobotStateTopic")]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct RobotStateTopic
    {
        [DdsKey]
        [DdsManaged]
        [MaxLength(64)]
        public string RobotId;

        public ulong TimestampNs;

        public SimpleEnum OperationalMode;

        [DdsManaged]
        [ArrayLength(9)]
        public double[] TransformMatrix;

        [DdsManaged]
        public Point2D[] CurrentPath;

        public SimpleUnion CurrentAction;

        [DdsOptional]
        public Container? CargoHold;
        
        [DdsOptional]
        public double? BatteryVoltage;
    }

    [DdsTopic("IoTDeviceMutableTopic")]
    [DdsExtensibility(DdsExtensibilityKind.Mutable)]
    public partial struct IoTDeviceMutableTopic
    {
        [DdsKey]
        [DdsId(10)]
        public int DeviceSerial;

        [DdsId(50)]
        public float Temperature;

        [DdsId(60)]
        [DdsOptional]
        [DdsManaged]
        [MaxLength(128)]
        public string LocationLabel;

        [DdsId(70)]
        [DdsManaged]
        // Workaround for CodeGen bug with Enum Arrays: Use int[] instead of ColorEnum[]
        public int[] StatusLeds;

        [DdsId(80)]
        public Point3D LastPingGeo;
    }

    [DdsTopic("AlignmentCheckTopic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct AlignmentCheckTopic
    {
        [DdsKey]
        public int Id;
        
        public byte B1; 
        
        public double D1; 
        
        public short S1;
        
        public byte C1; 
        
        public int L1;

        [DdsManaged]
        [ArrayLength(3)]
        public byte[] Blob;
        
        public int CheckValue;
    }
}
