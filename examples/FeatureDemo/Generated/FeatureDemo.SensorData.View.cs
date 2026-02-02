using System;
using System.Runtime.InteropServices;
using CycloneDDS.Core;
using CycloneDDS.Runtime;

namespace FeatureDemo
{
    public ref struct SensorDataView
    {
        private unsafe readonly SensorData_Native* _ptr;

        internal unsafe SensorDataView(SensorData_Native* ptr)
        {
            _ptr = ptr;
        }

        public unsafe System.Int32 SensorId => _ptr->SensorId;
        public unsafe System.Double Value => _ptr->Value;
        public unsafe CycloneDDS.Schema.FixedString32View Location => new CycloneDDS.Schema.FixedString32View(&_ptr->Location);
        public unsafe System.Int64 Timestamp => _ptr->Timestamp;
    public SensorData ToManaged()
    {
        unsafe
        {
            var target = new SensorData();
            target.SensorId = this.SensorId;
            target.Value = this.Value;
            target.Location = this.Location.ToManaged();
            target.Timestamp = this.Timestamp;
            return target;
        }
    }
    }
}
