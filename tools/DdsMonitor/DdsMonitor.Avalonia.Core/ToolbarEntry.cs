using System;
using System.Collections.Generic;

namespace DdsMonitor.Avalonia.Core;

/// <summary>
/// An entry in the application toolbar, registered by the shell or plugins.
/// </summary>
public sealed record ToolbarEntry(
    string Id,
    Action Action,
    string? IconKey,
    string Tooltip);
