using System.Collections.Generic;

namespace DdsMonitor.Avalonia.Core;

/// <summary>
/// Marks a ViewModel as capable of receiving its persisted component state.
/// </summary>
public interface IStatefulViewModel
{
    /// <summary>
    /// Called once during panel spawn with the restored component state dictionary.
    /// Implementations should read initial values and keep a reference to the dict
    /// for subsequent direct mutations (persisted by <see cref="IWindowManager"/> on close).
    /// </summary>
    void Initialize(IDictionary<string, object> componentState);
}
