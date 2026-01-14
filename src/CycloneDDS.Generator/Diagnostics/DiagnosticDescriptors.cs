using Microsoft.CodeAnalysis;

namespace CycloneDDS.Generator.Diagnostics
{
    internal static class DiagnosticDescriptors
    {
        private const string Category = "CycloneDDS.Generator";

        public static readonly DiagnosticDescriptor TopicDiscovered = new(
            id: DiagnosticIds.TopicDiscovered,
            title: "DDS Topic Discovered",
            messageFormat: "Discovered topic '{0}' for type '{1}'",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Info,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor TopicNameMissing = new(
            id: DiagnosticIds.TopicNameMissing,
            title: "Topic Name Missing",
            messageFormat: "Type '{0}' has [DdsTopic] but topic name is null or empty",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);
    }
}
