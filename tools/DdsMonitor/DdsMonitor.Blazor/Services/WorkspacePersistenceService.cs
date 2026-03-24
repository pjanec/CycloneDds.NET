using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DdsMonitor.Engine;

namespace DdsMonitor.Services;

/// <summary>
/// Handles debounced workspace persistence and import/export.
/// </summary>
public sealed class WorkspacePersistenceService : IDisposable
{
    private static readonly TimeSpan SaveDelay = TimeSpan.FromSeconds(2);

    private readonly IWindowManager _windowManager;
    private readonly IWorkspaceState _workspaceState;
    private readonly IDdsBridge _ddsBridge;
    private readonly ITopicRegistry _topicRegistry;
    private readonly AppSettings _appSettings;
    private readonly DebouncedAction _debouncer;

    // Exclusion names loaded from the workspace file at startup.  Names that cannot be
    // resolved to a loaded topic type are preserved in every subsequent save so that
    // exclusions for temporarily-unloaded assemblies survive across restarts.
    private IReadOnlyList<string> _loadedExclusionNames = Array.Empty<string>();

    public WorkspacePersistenceService(
        IWindowManager windowManager,
        IWorkspaceState workspaceState,
        IDdsBridge ddsBridge,
        ITopicRegistry topicRegistry,
        AppSettings appSettings)
    {
        _windowManager = windowManager ?? throw new ArgumentNullException(nameof(windowManager));
        _workspaceState = workspaceState ?? throw new ArgumentNullException(nameof(workspaceState));
        _ddsBridge = ddsBridge ?? throw new ArgumentNullException(nameof(ddsBridge));
        _topicRegistry = topicRegistry ?? throw new ArgumentNullException(nameof(topicRegistry));
        _appSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
        _debouncer = new DebouncedAction(SaveDelay, SaveNow);
    }

    public void RequestSave()
    {
        _debouncer.Trigger();
    }

    public void SaveNow()
    {
        // CLI options put the session in "override mode".  Do not persist the bridge's
        // (CLI-derived) subscription state to the workspace file, as it would corrupt the
        // saved state for the next non-CLI interactive session.
        if (HasCliOptions)
            return;

        var filePath = _workspaceState.WorkspaceFilePath;
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        try
        {
            // Sync the current excluded-topics state from DdsBridge into WindowManager
            // so the workspace file captures the latest subscription state.
            SyncExcludedTopicsToWindowManager();
            _windowManager.SaveWorkspace(filePath);
        }
        catch
        {
            // Ignore persistence errors to avoid breaking the UI loop.
        }
    }

    public void LoadDefault()
    {
        var filePath = _workspaceState.WorkspaceFilePath;
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return;
        }

        _windowManager.LoadWorkspace(filePath);

        // Snapshot the exclusion names before any debounced save can overwrite them.
        _loadedExclusionNames = _windowManager.ExcludedTopics;

        // When CLI options control the subscription set, skip loading saved exclusions.
        // AutoSubscribeAll() in TopicExplorerPanel will apply the CLI filters instead.
        if (HasCliOptions)
            return;

        // After loading the workspace, push the persisted excluded-topic names into
        // DdsBridge so that the auto-subscribe pass (in TopicExplorerPanel) respects them.
        ApplyExcludedTopicsToBridge();
    }

    public string ExportWorkspaceJson()
    {
        SyncExcludedTopicsToWindowManager();
        return _windowManager.SaveWorkspaceToJson();
    }

    public void ImportWorkspaceJson(string json)
    {
        _windowManager.LoadWorkspaceFromJson(json);
        _loadedExclusionNames = _windowManager.ExcludedTopics;
        if (!HasCliOptions)
            ApplyExcludedTopicsToBridge();
    }

    public void Dispose()
    {
        // Flush any pending debounced save before tearing down so that subscription
        // state changed just before the browser closes is not silently lost.
        _debouncer.Flush();
        _debouncer.Dispose();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// True when the user passed <c>--AppSettings:IncludeTopics</c> or
    /// <c>--AppSettings:ExcludeTopics</c> on the command line.
    /// </summary>
    private bool HasCliOptions =>
        (_appSettings.IncludeTopics is { Length: > 0 }) ||
        (_appSettings.ExcludeTopics is { Length: > 0 });

    private void SyncExcludedTopicsToWindowManager()
    {
        // Translate the DdsBridge.ExplicitlyUnsubscribedTopicTypes (Set<Type>) to
        // a list of fully-qualified CLR type names for JSON persistence.
        var bridgeExcluded = _ddsBridge.ExplicitlyUnsubscribedTopicTypes
            .Select(t => t.FullName ?? t.Name)
            .ToHashSet(StringComparer.Ordinal);

        // Preserve exclusion names from the originally-loaded workspace that could not be
        // matched against any currently-loaded topic type.  This round-trips exclusions for
        // assemblies that failed to load (e.g. a DLL that is temporarily unavailable),
        // so the saved state is not silently erased just because the topics are not loaded.
        var allTopicFqns = _topicRegistry.AllTopics
            .Select(t => t.TopicType.FullName ?? t.TopicType.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var savedName in _loadedExclusionNames)
        {
            // An exclusion is "unresolvable" when no currently-loaded topic matches the
            // pattern.  Keep it if it isn't already covered by the bridge-derived list.
            bool matchesLoadedTopic = allTopicFqns.Any(
                fqn => TopicFilterService.GlobMatch(fqn, savedName));
            if (!matchesLoadedTopic && !bridgeExcluded.Contains(savedName))
            {
                bridgeExcluded.Add(savedName);
            }
        }

        _windowManager.SetExcludedTopics(bridgeExcluded.ToList());
    }

    private void ApplyExcludedTopicsToBridge()
    {
        // The workspace may have been loaded with an old (legacy) format that has no
        // ExcludedTopics; in that case the list is empty and nothing changes.
        var savedNames = _windowManager.ExcludedTopics;
        if (savedNames.Count == 0)
            return;

        // Use name-based matching against the live topic registry instead of reflection-
        // based type resolution.  Topics are loaded into isolated AssemblyLoadContexts by
        // TopicDiscoveryService; Type.GetType() / GetAssemblies() may return a Type from a
        // different ALC instance than the one stored in ITopicRegistry, causing HashSet
        // lookups in DdsBridge to silently miss even when the names are identical.
        // ComputeExcluded matches by FullName (glob), so it always returns the exact Type
        // object that the registry - and therefore DdsBridge - will encounter later.
        var excluded = TopicFilterService.ComputeExcluded(
            _topicRegistry.AllTopics,
            cliIncludes: null,
            cliExcludes: null,
            savedExcludes: savedNames);

        _ddsBridge.InitializeExplicitlyUnsubscribed(excluded);
    }
}
