using System;
using System.Runtime.InteropServices;
using CycloneDDS.Core;
using CycloneDDS.Runtime;

namespace FeatureDemo
{
    public ref struct DemoControlView
    {
        private unsafe readonly DemoControl_Native* _ptr;

        internal unsafe DemoControlView(DemoControl_Native* ptr)
        {
            _ptr = ptr;
        }

        public unsafe System.Byte NodeId => _ptr->NodeId;
        public unsafe FeatureDemo.ControlCommand Command => (FeatureDemo.ControlCommand)_ptr->Command;
        public unsafe System.Int32 ScenarioId => _ptr->ScenarioId;
        public unsafe System.Int64 Timestamp => _ptr->Timestamp;
    public DemoControl ToManaged()
    {
        unsafe
        {
            var target = new DemoControl();
            target.NodeId = this.NodeId;
            target.Command = this.Command;
            target.ScenarioId = this.ScenarioId;
            target.Timestamp = this.Timestamp;
            return target;
        }
    }
    }
}
