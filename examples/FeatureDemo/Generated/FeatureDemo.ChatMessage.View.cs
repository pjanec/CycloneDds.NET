using System;
using System.Runtime.InteropServices;
using CycloneDDS.Core;
using CycloneDDS.Runtime;

namespace FeatureDemo
{
    public ref struct ChatMessageView
    {
        private unsafe readonly ChatMessage_Native* _ptr;

        internal unsafe ChatMessageView(ChatMessage_Native* ptr)
        {
            _ptr = ptr;
        }

        public unsafe System.Int64 MessageId => _ptr->MessageId;
        public unsafe CycloneDDS.Schema.FixedString32View User => new CycloneDDS.Schema.FixedString32View(&_ptr->User);
        public unsafe CycloneDDS.Schema.FixedString128View Content => new CycloneDDS.Schema.FixedString128View(&_ptr->Content);
        public unsafe System.Int64 Timestamp => _ptr->Timestamp;
    public ChatMessage ToManaged()
    {
        unsafe
        {
            var target = new ChatMessage();
            target.MessageId = this.MessageId;
            target.User = this.User.ToManaged();
            target.Content = this.Content.ToManaged();
            target.Timestamp = this.Timestamp;
            return target;
        }
    }
    }
}
