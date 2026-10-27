using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using CycloneDDS.Runtime.Interop;
using Xunit;

namespace CycloneDDS.Runtime.Tests
{
    [CollectionDefinition("Network Domains", DisableParallelization = true)]
    public class NetworkDomainsCollection { }

    /// <summary>
    /// DDS Security with the shipped plugins and the test fixtures in Resources/Security.
    /// Each "peer" is its own Cyclone domain instance talking to the others over the network
    /// (wire domain 7), so authentication, access control and encryption really run.
    /// Runs alone: real network traffic disturbs timing-sensitive tests.
    /// </summary>
    [Collection("Network Domains")]
    public class DdsSecurityTests
    {
        private const uint WireDomain = 7;
        private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Resources", "Security");

        [Fact]
        public void SecuredPeers_ExchangeData_UnsecuredPeerIsShutOut()
        {
            EnsureOpenSslLoadable();
            string topic = "Secured_" + Guid.NewGuid().ToString("N");

            using var writerDomain = new TestDomain(WireDomain, SecurityConfig(identityCert: "identity_cert.pem"));
            using var securedDomain = new TestDomain(WireDomain + 1, SecurityConfig(identityCert: "identity_cert.pem"));
            using var plainDomain = new TestDomain(WireDomain + 2, Config(security: ""));

            using var pWriter = new DdsParticipant(writerDomain.Id);
            using var pSecured = new DdsParticipant(securedDomain.Id);
            using var pPlain = new DdsParticipant(plainDomain.Id);

            using var securedReader = new DdsReader<TestMessage>(pSecured, topic);
            using var plainReader = new DdsReader<TestMessage>(pPlain, topic);
            using var writer = new DdsWriter<TestMessage>(pWriter, topic);

            // Authentication handshake + key exchange take a moment.
            Assert.True(WaitUntil(() => securedReader.CurrentStatus.CurrentCount > 0, TimeSpan.FromSeconds(20)),
                "secured reader never matched the secured writer");

            bool securedGotIt = WaitUntil(() =>
            {
                writer.Write(new TestMessage { Id = 1, Value = 42 });
                using var scope = securedReader.Take();
                return scope.Count > 0 && scope[0].Value == 42;
            }, TimeSpan.FromSeconds(10));
            Assert.True(securedGotIt, "secured reader did not receive data");

            // The unauthenticated peer must neither match nor receive anything.
            Thread.Sleep(1000);
            Assert.Equal(0u, plainReader.CurrentStatus.CurrentCount);
            using (var plain = plainReader.Take())
                Assert.Equal(0, plain.Count);
        }

        [Fact]
        public void InvalidIdentity_ParticipantCreationFails()
        {
            EnsureOpenSslLoadable();
            // The CA certificate is not an identity issued by the CA: authentication must refuse it.
            using var domain = new TestDomain(WireDomain + 3, SecurityConfig(identityCert: "ca_cert.pem"));

            Assert.Throws<DdsException>(() => new DdsParticipant(domain.Id));
        }

        private static string SecurityConfig(string identityCert) => Config(security: $@"
      <Security>
        <Authentication>
          <IdentityCertificate>{FileUri(identityCert)}</IdentityCertificate>
          <IdentityCA>{FileUri("ca_cert.pem")}</IdentityCA>
          <PrivateKey>{FileUri("identity_key.pem")}</PrivateKey>
        </Authentication>
        <AccessControl>
          <PermissionsCA>{FileUri("ca_cert.pem")}</PermissionsCA>
          <Governance>{FileUri("governance.p7s")}</Governance>
          <Permissions>{FileUri("permissions.p7s")}</Permissions>
        </AccessControl>
      </Security>");

        private static string Config(string security) => $@"<CycloneDDS><Domain id=""any"">
      <Discovery><ExternalDomainId>{WireDomain}</ExternalDomainId></Discovery>{security}
    </Domain></CycloneDDS>";

        private static string FileUri(string name) => "file:" + Path.Combine(Fixtures, name).Replace('\\', '/');

        /// <summary>
        /// The security plugins import OpenSSL 3, which the package does not ship. On Windows,
        /// if it is not already resolvable (PATH / system dirs), preload it from the default
        /// install location: once loaded, Windows reuses it for the plugins' imports.
        /// </summary>
        private static void EnsureOpenSslLoadable()
        {
            if (!OperatingSystem.IsWindows()) return; // Linux: the distribution's libcrypto.so.3
            if (NativeLibrary.TryLoad("libcrypto-3-x64.dll", out _) && NativeLibrary.TryLoad("libssl-3-x64.dll", out _)) return;

            foreach (var dir in new[] { Environment.GetEnvironmentVariable("OPENSSL_BIN"), @"C:\Program Files\OpenSSL\bin", @"C:\Program Files\OpenSSL-Win64\bin" })
            {
                if (string.IsNullOrEmpty(dir)) continue;
                string crypto = Path.Combine(dir, "libcrypto-3-x64.dll"), ssl = Path.Combine(dir, "libssl-3-x64.dll");
                if (File.Exists(crypto) && File.Exists(ssl))
                {
                    NativeLibrary.Load(crypto);
                    NativeLibrary.Load(ssl);
                    return;
                }
            }
            Assert.Fail("OpenSSL 3 runtime not found; install it (https://slproweb.com/products/Win32OpenSSL.html) or set OPENSSL_BIN to its bin folder.");
        }

        private static bool WaitUntil(Func<bool> condition, TimeSpan timeout)
        {
            var end = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < end)
            {
                if (condition()) return true;
                Thread.Sleep(50);
            }
            return condition();
        }

        /// <summary>An explicitly configured Cyclone domain instance, deleted on dispose.</summary>
        private sealed class TestDomain : IDisposable
        {
            private readonly int _handle;
            public uint Id { get; }

            public TestDomain(uint id, string config)
            {
                Id = id;
                _handle = dds_create_domain(id, config);
                Assert.True(_handle > 0, $"dds_create_domain({id}) failed: {_handle}");
            }

            public void Dispose() => DdsApi.dds_delete(new DdsApi.DdsEntity { Handle = _handle });

            [DllImport("ddsc")]
            private static extern int dds_create_domain(uint domainId, [MarshalAs(UnmanagedType.LPStr)] string config);
        }
    }
}
