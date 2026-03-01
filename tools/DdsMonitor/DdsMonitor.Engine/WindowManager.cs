using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DdsMonitor.Engine;

/// <summary>
/// Default in-memory window manager.
/// </summary>
public sealed class WindowManager : IWindowManager
{
    private const int FirstPanelIndex = 1;
    private const int ZIndexIncrement = 1;
    private const int DefaultZIndex = 1;
    private const double DefaultPanelX = 40;
    private const double DefaultPanelY = 40;
    private const double DefaultPanelWidth = 420;
    private const double DefaultPanelHeight = 300;

    private static readonly JsonSerializerOptions WorkspaceSerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly object _sync = new();
    private readonly List<PanelState> _activePanels = new();
    private readonly Dictionary<string, Type> _panelTypes = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public event Action<PanelState>? PanelClosed;

    /// <inheritdoc />
    public event Action? PanelsChanged;

    /// <inheritdoc />
    public IReadOnlyList<PanelState> ActivePanels
    {
        get
        {
            lock (_sync)
            {
                return _activePanels.ToArray();
            }
        }
    }

    /// <inheritdoc />
    public PanelState SpawnPanel(string componentTypeName, Dictionary<string, object>? initialState = null)
    {
        if (string.IsNullOrWhiteSpace(componentTypeName))
        {
            throw new ArgumentException("Component type name is required.", nameof(componentTypeName));
        }

        PanelState panel;

        lock (_sync)
        {
            var baseName = GetPanelBaseName(componentTypeName);
            var panelId = CreatePanelId(baseName);
            var resolvedTypeName = ResolveComponentTypeName(componentTypeName);

            panel = new PanelState
            {
                PanelId = panelId,
                Title = baseName,
                ComponentTypeName = resolvedTypeName,
                X = DefaultPanelX,
                Y = DefaultPanelY,
                Width = DefaultPanelWidth,
                Height = DefaultPanelHeight,
                ZIndex = GetNextZIndex(),
                ComponentState = initialState == null
                    ? new Dictionary<string, object>(StringComparer.Ordinal)
                    : new Dictionary<string, object>(initialState, StringComparer.Ordinal)
            };

            _activePanels.Add(panel);
        }

        PanelsChanged?.Invoke();

        return panel;
    }

    /// <inheritdoc />
    public void ClosePanel(string panelId)
    {
        if (string.IsNullOrWhiteSpace(panelId))
        {
            throw new ArgumentException("Panel identifier is required.", nameof(panelId));
        }

        lock (_sync)
        {
            for (var i = 0; i < _activePanels.Count; i++)
            {
                if (string.Equals(_activePanels[i].PanelId, panelId, StringComparison.Ordinal))
                {
                    _activePanels[i].IsHidden = true;
                    _activePanels[i].IsMinimized = false;
                    PanelClosed?.Invoke(_activePanels[i]);
                    PanelsChanged?.Invoke();
                    return;
                }
            }
        }
    }

    /// <inheritdoc />
    public void BringToFront(string panelId)
    {
        if (string.IsNullOrWhiteSpace(panelId))
        {
            throw new ArgumentException("Panel identifier is required.", nameof(panelId));
        }

        lock (_sync)
        {
            PanelState? panel = null;

            foreach (var entry in _activePanels)
            {
                if (string.Equals(entry.PanelId, panelId, StringComparison.Ordinal))
                {
                    panel = entry;
                    break;
                }
            }

            if (panel == null)
            {
                return;
            }

            panel.ZIndex = GetHighestZIndex() + ZIndexIncrement;
        }
    }

    /// <inheritdoc />
    public void ClearPanels()
    {
        lock (_sync)
        {
            _activePanels.Clear();
        }

        PanelsChanged?.Invoke();
    }

    /// <inheritdoc />
    public void RegisterPanelType(string typeName, Type blazorComponentType)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            throw new ArgumentException("Type name is required.", nameof(typeName));
        }

        if (blazorComponentType == null)
        {
            throw new ArgumentNullException(nameof(blazorComponentType));
        }

        lock (_sync)
        {
            _panelTypes[typeName] = blazorComponentType;
        }
    }

    /// <inheritdoc />
    public void SaveWorkspace(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path is required.", nameof(filePath));
        }

        PanelState[] snapshot;

        lock (_sync)
        {
            snapshot = _activePanels.ToArray();
        }

        var filtered = FilterPersistableState(snapshot);
        var json = JsonSerializer.Serialize(filtered, WorkspaceSerializerOptions);
        File.WriteAllText(filePath, json);
    }

    /// <inheritdoc />
    public void LoadWorkspace(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path is required.", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Workspace file not found.", filePath);
        }

        var json = File.ReadAllText(filePath);
        var loaded = JsonSerializer.Deserialize<List<PanelState>>(json, WorkspaceSerializerOptions) ?? new List<PanelState>();

        foreach (var panel in loaded)
        {
            panel.ComponentState ??= new Dictionary<string, object>(StringComparer.Ordinal);
        }

        lock (_sync)
        {
            _activePanels.Clear();
            _activePanels.AddRange(loaded);
        }

        PanelsChanged?.Invoke();
    }

    private static List<PanelState> FilterPersistableState(IEnumerable<PanelState> panels)
    {
        var result = new List<PanelState>();

        foreach (var panel in panels)
        {
            if (panel == null)
            {
                continue;
            }

            var clone = new PanelState
            {
                PanelId = panel.PanelId,
                Title = panel.Title,
                ComponentTypeName = panel.ComponentTypeName,
                X = panel.X,
                Y = panel.Y,
                Width = panel.Width,
                Height = panel.Height,
                ZIndex = panel.ZIndex,
                IsMinimized = panel.IsMinimized,
                IsHidden = panel.IsHidden
            };

            var filteredState = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var entry in panel.ComponentState)
            {
                if (TrySanitizeValue(entry.Value, out var sanitized))
                {
                    filteredState[entry.Key] = sanitized!;
                }
            }

            clone.ComponentState = filteredState;
            result.Add(clone);
        }

        return result;
    }

    private static bool TrySanitizeValue(object? value, out object? sanitized)
    {
        if (value == null)
        {
            sanitized = null;
            return true;
        }

        switch (value)
        {
            case string or bool or byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal:
                sanitized = value;
                return true;
            case DateTime or DateTimeOffset:
                sanitized = value;
                return true;
        }

        if (value is string[] stringArray)
        {
            sanitized = stringArray;
            return true;
        }

        if (value is double[] doubleArray)
        {
            sanitized = doubleArray;
            return true;
        }

        if (value is int[] intArray)
        {
            sanitized = intArray;
            return true;
        }

        if (value is Dictionary<string, double> doubleMap)
        {
            sanitized = new Dictionary<string, double>(doubleMap, StringComparer.Ordinal);
            return true;
        }

        if (value is Dictionary<string, string> stringMap)
        {
            sanitized = new Dictionary<string, string>(stringMap, StringComparer.Ordinal);
            return true;
        }

        if (value is Dictionary<string, object> objectMap)
        {
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var entry in objectMap)
            {
                if (TrySanitizeValue(entry.Value, out var inner))
                {
                    result[entry.Key] = inner!;
                }
            }

            sanitized = result;
            return result.Count > 0;
        }

        if (value is IEnumerable<object> list)
        {
            var result = new List<object>();
            foreach (var item in list)
            {
                if (TrySanitizeValue(item, out var inner))
                {
                    result.Add(inner!);
                }
            }

            sanitized = result;
            return result.Count > 0;
        }

        sanitized = null;
        return false;
    }

    private int GetNextZIndex()
    {
        var currentMax = GetHighestZIndex();
        return currentMax < DefaultZIndex ? DefaultZIndex : currentMax + ZIndexIncrement;
    }

    private int GetHighestZIndex()
    {
        var max = DefaultZIndex;

        foreach (var panel in _activePanels)
        {
            if (panel.ZIndex > max)
            {
                max = panel.ZIndex;
            }
        }

        return max;
    }

    private string ResolveComponentTypeName(string componentTypeName)
    {
        if (_panelTypes.TryGetValue(componentTypeName, out var registered))
        {
            return registered.AssemblyQualifiedName ?? registered.FullName ?? componentTypeName;
        }

        var resolved = Type.GetType(componentTypeName);
        return resolved?.AssemblyQualifiedName ?? resolved?.FullName ?? componentTypeName;
    }

    private string GetPanelBaseName(string componentTypeName)
    {
        if (_panelTypes.TryGetValue(componentTypeName, out var registered))
        {
            return registered.Name;
        }

        var resolved = Type.GetType(componentTypeName);
        return resolved?.Name ?? componentTypeName;
    }

    private string CreatePanelId(string baseName)
    {
        var index = FirstPanelIndex;
        while (PanelIdExists(baseName, index))
        {
            index += ZIndexIncrement;
        }

        return $"{baseName}.{index}";
    }

    private bool PanelIdExists(string baseName, int index)
    {
        var candidate = $"{baseName}.{index}";

        foreach (var panel in _activePanels)
        {
            if (string.Equals(panel.PanelId, candidate, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
