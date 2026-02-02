using System;
using System.Runtime.InteropServices;
using CycloneDDS.Core;
using CycloneDDS.Runtime;

namespace FeatureDemo
{
    public ref struct FlightPositionView
    {
        private unsafe readonly FlightPosition_Native* _ptr;

        internal unsafe FlightPositionView(FlightPosition_Native* ptr)
        {
            _ptr = ptr;
        }

        public unsafe CycloneDDS.Schema.FixedString32View FlightId => new CycloneDDS.Schema.FixedString32View(&_ptr->FlightId);
        public unsafe System.Double Latitude => _ptr->Latitude;
        public unsafe System.Double Longitude => _ptr->Longitude;
        public unsafe System.Double Altitude => _ptr->Altitude;
        public unsafe System.Int64 Timestamp => _ptr->Timestamp;
    public FlightPosition ToManaged()
    {
        unsafe
        {
            var target = new FlightPosition();
            target.FlightId = this.FlightId.ToManaged();
            target.Latitude = this.Latitude;
            target.Longitude = this.Longitude;
            target.Altitude = this.Altitude;
            target.Timestamp = this.Timestamp;
            return target;
        }
    }
    }
}
