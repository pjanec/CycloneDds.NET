using System;

namespace DdsMonitor.Engine;

/// <summary>
/// Runtime-mutable developer / debug settings.
/// Unlike <see cref="DdsSettings"/> these can be toggled live from the UI.
/// </summary>
public sealed class DevelSettings
{
    private bool _selfSendEnabled;

    /// <summary>
    /// Raised whenever any devel setting changes.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// Gets or sets a value indicating whether self-sending of mock DDS samples is active.
    /// Defaults to <c>false</c>.
    /// </summary>
    public bool SelfSendEnabled
    {
        get => _selfSendEnabled;
        set
        {
            if (_selfSendEnabled == value)
            {
                return;
            }

            _selfSendEnabled = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
