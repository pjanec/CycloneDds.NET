using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DdsMonitor.Engine;

/// <summary>
/// Reads the saved <see cref="WorkspaceDocument.ExcludedTopics"/> list from the workspace file
/// and initialises <see cref="IDdsBridge.InitializeExplicitlyUnsubscribed"/> before the first
/// auto-subscribe pass.
///
/// Used in both interactive and headless modes.  In headless mode the file is opened read-only
/// and no writes are made, so interactive sessions are not affected.
/// </summary>
public sealed class TopicSubscriptionBootstrapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Applies the initial excluded-topics set to <paramref name="ddsBridge"/> based on the
    /// workspace file, CLI patterns from <paramref name="appSettings"/>, and the loaded topics
    /// in <paramref name="topicRegistry"/>.
    /// </summary>
    /// <param name="ddsBridge">The bridge whose explicit-unsubscribe set is to be initialised.</param>
    /// <param name="topicRegistry">The registry of all known topic types.</param>
    /// <param name="appSettings">CLI settings that may override saved state.</param>
    /// <param name="workspaceFilePath">
    ///   Path to the workspace JSON file.  May be null or non-existent – in that case only CLI
    ///   options are applied.
    /// </param>
    public static void Apply(
        IDdsBridge ddsBridge,
        ITopicRegistry topicRegistry,
        AppSettings appSettings,
        string? workspaceFilePath)
    {
        if (ddsBridge == null) throw new ArgumentNullException(nameof(ddsBridge));
        if (topicRegistry == null) throw new ArgumentNullException(nameof(topicRegistry));
        if (appSettings == null) throw new ArgumentNullException(nameof(appSettings));

        IEnumerable<string>? savedExcludes = null;

        // Only use saved exclusions when no CLI overrides are present.
        bool hasCli = (appSettings.IncludeTopics != null && appSettings.IncludeTopics.Length > 0)
                   || (appSettings.ExcludeTopics != null && appSettings.ExcludeTopics.Length > 0);

        if (!hasCli && !string.IsNullOrWhiteSpace(workspaceFilePath) && File.Exists(workspaceFilePath))
        {
            try
            {
                var json = File.ReadAllText(workspaceFilePath);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    var wsDoc = JsonSerializer.Deserialize<WorkspaceDocument>(json, JsonOptions);
                    savedExcludes = wsDoc?.ExcludedTopics;
                }
            }
            catch
            {
                // Gracefully ignore corrupt workspace files.
            }
        }

        var allTopics = topicRegistry.AllTopics;
        var excluded = TopicFilterService.ComputeExcluded(
            allTopics,
            hasCli ? appSettings.IncludeTopics : null,
            hasCli ? appSettings.ExcludeTopics : null,
            savedExcludes);

        ddsBridge.InitializeExplicitlyUnsubscribed(excluded);
    }
}
