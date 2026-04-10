using System;
using System.IO;
using System.Xml;

namespace CycloneDDS.Runtime.Tracking
{
    /// <summary>
    /// Reads selected settings from the CycloneDDS XML configuration file
    /// located via the <c>CYCLONEDDS_URI</c> environment variable.
    /// All values are resolved once and cached for the lifetime of the process.
    /// </summary>
    internal static class CycloneDdsXmlConfig
    {
        private static readonly Lazy<string?> _networkInterfaceAddress =
            new Lazy<string?>(ResolveNetworkInterfaceAddress, isThreadSafe: true);

        /// <summary>
        /// Gets the <c>address</c> attribute of the first
        /// <c>CycloneDDS/Domain/General/Interfaces/NetworkInterface</c> element,
        /// or <c>null</c> if the config file cannot be located or the element is absent.
        /// </summary>
        public static string? NetworkInterfaceAddress => _networkInterfaceAddress.Value;

        private static string? ResolveNetworkInterfaceAddress()
        {
            try
            {
                var uri = Environment.GetEnvironmentVariable("CYCLONEDDS_URI");
                if (string.IsNullOrWhiteSpace(uri))
                    return null;

                // Strip optional file:// scheme.
                if (uri.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                    uri = uri.Substring(7);

                // If it looks like a file path, try to load it; otherwise treat as inline XML.
                string? xmlContent = null;
                if (uri.StartsWith("<", StringComparison.Ordinal))
                {
                    xmlContent = uri;
                }
                else if (File.Exists(uri))
                {
                    xmlContent = File.ReadAllText(uri);
                }
                else
                {
                    return null;
                }

                var doc = new XmlDocument();
                doc.LoadXml(xmlContent);

                var node = doc.SelectSingleNode(
                    "/CycloneDDS/Domain/General/Interfaces/NetworkInterface/@address");

                return node?.Value;
            }
            catch
            {
                // Configuration errors must never crash the application.
                return null;
            }
        }
    }
}
