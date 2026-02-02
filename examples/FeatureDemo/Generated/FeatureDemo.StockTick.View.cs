using System;
using System.Runtime.InteropServices;
using CycloneDDS.Core;
using CycloneDDS.Runtime;

namespace FeatureDemo
{
    public ref struct StockTickView
    {
        private unsafe readonly StockTick_Native* _ptr;

        internal unsafe StockTickView(StockTick_Native* ptr)
        {
            _ptr = ptr;
        }

        public unsafe System.Int64 TickId => _ptr->TickId;
        public unsafe CycloneDDS.Schema.FixedString32View Symbol => new CycloneDDS.Schema.FixedString32View(&_ptr->Symbol);
        public unsafe System.Double Price => _ptr->Price;
        public unsafe System.Int32 Volume => _ptr->Volume;
        public unsafe System.Int64 Timestamp => _ptr->Timestamp;
    public StockTick ToManaged()
    {
        unsafe
        {
            var target = new StockTick();
            target.TickId = this.TickId;
            target.Symbol = this.Symbol.ToManaged();
            target.Price = this.Price;
            target.Volume = this.Volume;
            target.Timestamp = this.Timestamp;
            return target;
        }
    }
    }
}
