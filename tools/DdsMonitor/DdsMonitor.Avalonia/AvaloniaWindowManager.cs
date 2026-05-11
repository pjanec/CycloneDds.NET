using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using DdsMonitor.Avalonia.Core;
using DdsMonitor.Engine;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace DdsMonitor.Avalonia;

/// <summary>
/// Avalonia implementation of <see cref="IWindowManager"/>.
/// Spawns, tracks, and manages floating panel windows.
/// Focused when already open; geometry is persisted on close.
/// </summary>
public sealed class AvaloniaWindowManager : IWindowManager
{
    private readonly IAvaloniaViewRegistry _viewRegistry;
    private readonly IServiceProvider _services;
    private readonly IEventBroker _eventBroker;

    private readonly object _lock = new();
    private readonly List<PanelState> _activePanels = new();
    private readonly Dictionary<string, Window> _openWindows = new();
    private readonly Dictionary<string, object> _viewModels = new(StringComparer.Ordinal);
    private readonly List<string> _excludedTopics = new();

    public AvaloniaWindowManager(
        IAvaloniaViewRegistry viewRegistry,
        IServiceProvider services,
        IEventBroker eventBroker)
    {
        _viewRegistry = viewRegistry;
        _services = services;
        _eventBroker = eventBroker;
    }

    // ── IWindowManager ────────────────────────────────────────────────────────

    public event Action<PanelState>? PanelClosed;
    public event Action? PanelsChanged;

    public IReadOnlyList<PanelState> ActivePanels
    {
        get { lock (_lock) return _activePanels.ToList(); }
    }

    public IReadOnlyList<string> ExcludedTopics
    {
        get { lock (_lock) return _excludedTopics.ToList(); }
    }

    public void SetExcludedTopics(IEnumerable<string> topicTypeNames)
    {
        ArgumentNullException.ThrowIfNull(topicTypeNames);
        lock (_lock)
        {
            _excludedTopics.Clear();
            _excludedTopics.AddRange(topicTypeNames);
        }
    }

    /// <summary>
    /// Spawns a panel window for the given component type name.
    /// If a panel with the same ID is already open, focuses it instead of creating a new one.
    /// The <paramref name="componentTypeName"/> must be the fully-qualified CLR type name of a
    /// ViewModel type registered with <see cref="IAvaloniaViewRegistry"/>.
    /// </summary>
    public PanelState SpawnPanel(string componentTypeName, Dictionary<string, object>? initialState = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(componentTypeName);

        var panelId = componentTypeName;

        lock (_lock)
        {
            // If already open, focus and return existing state
            if (_openWindows.ContainsKey(panelId))
            {
                BringToFront(panelId);
                return _activePanels.First(p => p.PanelId == panelId);
            }
        }

        var panelState = new PanelState
        {
            PanelId = panelId,
            Title = componentTypeName,
            ComponentTypeName = componentTypeName,
            ComponentState = initialState ?? new Dictionary<string, object>(StringComparer.Ordinal),
        };

        // Restore geometry from component state if available
        if (panelState.ComponentState.TryGetValue("__window", out var geo))
        {
            Dictionary<string, object>? geoDict = null;
            if (geo is Dictionary<string, object> nativeDict)
            {
                geoDict = nativeDict;
            }
            else if (geo is JsonElement je && je.ValueKind == JsonValueKind.Object)
            {
                // Deserialized from JSON — values come in as JsonElement; convert to native dict.
                geoDict = je.Deserialize<Dictionary<string, object>>() ?? new();
                panelState.ComponentState["__window"] = geoDict;
            }

            if (geoDict is not null)
            {
                if (geoDict.TryGetValue("X", out var x)) panelState.X = ToDouble(x);
                if (geoDict.TryGetValue("Y", out var y)) panelState.Y = ToDouble(y);
                if (geoDict.TryGetValue("Width", out var w)) panelState.Width = ToDouble(w);
                if (geoDict.TryGetValue("Height", out var h)) panelState.Height = ToDouble(h);
            }
        }

        // Create window on UI thread
        Dispatcher.UIThread.Post(() => OpenPanelWindow(panelState));

        return panelState;
    }

    private void OpenPanelWindow(PanelState panelState)
    {
        // Resolve the ViewModel type and build the view
        Control content;
        object? vmToTrack = null;
        try
        {
            var vmType = Type.GetType(panelState.ComponentTypeName)
                ?? AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a =>
                    {
                        try { return a.GetType(panelState.ComponentTypeName); }
                        catch { return null; }
                    })
                    .FirstOrDefault(t => t != null);

            if (vmType is not null)
            {
                // Prefer a registered DI factory; fall back to reflection-based creation.
                var vm = _services.GetService(vmType)
                    ?? ActivatorUtilities.CreateInstance(_services, vmType);

                if (vm is IStatefulViewModel stateful)
                {
                    stateful.Initialize(panelState.ComponentState);
                }

                content = _viewRegistry.BuildView(vm);
                vmToTrack = vm;
            }
            else
            {
                content = new TextBlock { Text = $"Unknown panel: {panelState.ComponentTypeName}" };
            }
        }
        catch (Exception ex)
        {
            content = new TextBlock { Text = $"Error loading panel: {ex.Message}" };
        }

        var window = new Window
        {
            Title = panelState.Title,
            Content = content,
            Width = panelState.Width > 0 ? panelState.Width : 600,
            Height = panelState.Height > 0 ? panelState.Height : 400,
        };

        if (panelState.X != 0 || panelState.Y != 0)
        {
            window.Position = new PixelPoint((int)panelState.X, (int)panelState.Y);
        }

        window.Closed += (_, _) => OnWindowClosed(panelState);

        lock (_lock)
        {
            _openWindows[panelState.PanelId] = window;
            _activePanels.Add(panelState);
            if (vmToTrack is not null)
                _viewModels[panelState.PanelId] = vmToTrack;
        }

        PanelsChanged?.Invoke();
        window.Show();
    }

    private void OnWindowClosed(PanelState panelState)
    {
        Window? win;
        object? vm;
        lock (_lock)
        {
            _openWindows.TryGetValue(panelState.PanelId, out win);
            _openWindows.Remove(panelState.PanelId);
            _activePanels.RemoveAll(p => p.PanelId == panelState.PanelId);
            _viewModels.TryGetValue(panelState.PanelId, out vm);
            _viewModels.Remove(panelState.PanelId);
        }

        if (win is not null)
        {
            // Persist geometry to ComponentState
            panelState.ComponentState["__window"] = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["X"] = win.Position.X,
                ["Y"] = win.Position.Y,
                ["Width"] = win.Width,
                ["Height"] = win.Height,
            };
        }

        if (vm is IDisposable disposable)
            disposable.Dispose();

        PanelClosed?.Invoke(panelState);
        PanelsChanged?.Invoke();

        _eventBroker.Publish(new WorkspaceSaveRequestedEvent());
    }

    public void ClosePanel(string panelId)
    {
        Window? win;
        lock (_lock)
        {
            _openWindows.TryGetValue(panelId, out win);
        }

        if (win is not null)
        {
            Dispatcher.UIThread.Post(() => win.Close());
        }
    }

    public void BringToFront(string panelId)
    {
        Window? win;
        lock (_lock)
        {
            _openWindows.TryGetValue(panelId, out win);
        }

        if (win is not null)
        {
            Dispatcher.UIThread.Post(() =>
            {
                win.WindowState = WindowState.Normal;
                win.Activate();
                win.Focus();
            });
        }
    }

    public void ShowPanel(string panelId)
    {
        Window? win;
        PanelState? state;
        lock (_lock)
        {
            _openWindows.TryGetValue(panelId, out win);
            state = _activePanels.FirstOrDefault(p => p.PanelId == panelId);
        }

        if (win is not null && state is not null)
        {
            Dispatcher.UIThread.Post(() =>
            {
                win.WindowState = WindowState.Normal;
                win.IsVisible = true;
                win.Activate();
                win.Focus();
            });

            if (state.IsHidden)
            {
                state.IsHidden = false;
                PanelsChanged?.Invoke();
            }
        }
    }

    public void ClearPanels()
    {
        List<string> panelIds;
        lock (_lock)
        {
            panelIds = _openWindows.Keys.ToList();
        }

        foreach (var id in panelIds)
        {
            ClosePanel(id);
        }
    }

    // ── Panel type registry ───────────────────────────────────────────────────

    private readonly Dictionary<string, Type> _registeredPanelTypes = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, Type> RegisteredPanelTypes
    {
        get { lock (_lock) return new Dictionary<string, Type>(_registeredPanelTypes, StringComparer.Ordinal); }
    }

    public void RegisterPanelType(string typeName, Type panelType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);
        ArgumentNullException.ThrowIfNull(panelType);
        lock (_lock)
        {
            _registeredPanelTypes[typeName] = panelType;
        }
    }

    // ── Workspace persistence ─────────────────────────────────────────────────

    public void SaveWorkspace(string filePath)
    {
        var json = SaveWorkspaceToJson();
        File.WriteAllText(filePath, json);
    }

    public string SaveWorkspaceToJson()
    {
        List<PanelState> panels;
        lock (_lock)
        {
            // Capture current window geometry before serializing
            foreach (var kvp in _openWindows)
            {
                var panelState = _activePanels.FirstOrDefault(p => p.PanelId == kvp.Key);
                if (panelState is not null)
                {
                    var win = kvp.Value;
                    panelState.ComponentState["__window"] = new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["X"] = win.Position.X,
                        ["Y"] = win.Position.Y,
                        ["Width"] = win.Width,
                        ["Height"] = win.Height,
                    };
                }
            }
            panels = _activePanels.ToList();
        }

        return System.Text.Json.JsonSerializer.Serialize(panels);
    }

    public void LoadWorkspace(string filePath)
    {
        if (!File.Exists(filePath)) return;
        var json = File.ReadAllText(filePath);
        LoadWorkspaceFromJson(json);
    }

    public void LoadWorkspaceFromJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        var panels = System.Text.Json.JsonSerializer.Deserialize<List<PanelState>>(json);
        if (panels is null) return;

        foreach (var panel in panels)
        {
            SpawnPanel(panel.ComponentTypeName, panel.ComponentState);
        }
    }

    /// <summary>
    /// Converts a geometry value (either a native <see cref="double"/> or a JSON-deserialized
    /// <see cref="JsonElement"/> number) to a <see cref="double"/>.
    /// </summary>
    private static double ToDouble(object value) =>
        value is JsonElement je ? je.GetDouble() : Convert.ToDouble(value);
}
