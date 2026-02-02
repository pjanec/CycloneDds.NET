using System;
using System.Runtime.InteropServices;
using CycloneDDS.Core;
using CycloneDDS.Runtime;

namespace FeatureDemo
{
    public ref struct SystemLogView
    {
        private unsafe readonly SystemLog_Native* _ptr;

        internal unsafe SystemLogView(SystemLog_Native* ptr)
        {
            _ptr = ptr;
        }

        public unsafe System.Int64 LogId => _ptr->LogId;
        public unsafe FeatureDemo.LogLevel Level => (FeatureDemo.LogLevel)_ptr->Level;
        public unsafe CycloneDDS.Schema.FixedString64View Component => new CycloneDDS.Schema.FixedString64View(&_ptr->Component);
        public unsafe CycloneDDS.Schema.FixedString128View Message => new CycloneDDS.Schema.FixedString128View(&_ptr->Message);
        public unsafe System.Int64 Timestamp => _ptr->Timestamp;
    public SystemLog ToManaged()
    {
        unsafe
        {
            var target = new SystemLog();
            target.LogId = this.LogId;
            target.Level = this.Level;
            target.Component = this.Component.ToManaged();
            target.Message = this.Message.ToManaged();
            target.Timestamp = this.Timestamp;
            return target;
        }
    }
    }
}
