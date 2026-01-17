using System;
using Xunit;
using CycloneDDS.Runtime;

namespace CycloneDDS.Runtime.Tests
{
    public class DdsParticipantTests
    {
        [Fact]
        public void CreateParticipant_Domain0_Succeeds()
        {
            using var participant = new DdsParticipant(0);
            Assert.False(participant.IsDisposed);
            Assert.Equal(0u, participant.DomainId);
            Assert.True(participant.NativeEntity.IsValid);
        }

        [Fact]
        public void CreateParticipant_Domain100_Succeeds()
        {
            // Note: This relies on local machine configuration allowing domain 100
            // but usually it works.
            using var participant = new DdsParticipant(100);
            Assert.Equal(100u, participant.DomainId);
        }

        [Fact]
        public void Dispose_IsIdempotent()
        {
            var participant = new DdsParticipant(0);
            participant.Dispose();
            Assert.True(participant.IsDisposed);
            
            // Should not throw
            participant.Dispose();
            Assert.True(participant.IsDisposed);
        }

        [Fact]
        public void AccessEntity_AfterDispose_ThrowsObjectDisposedException()
        {
            var participant = new DdsParticipant(0);
            participant.Dispose();
            
            Assert.Throws<ObjectDisposedException>(() => _ = participant.NativeEntity);
        }
    }
}
