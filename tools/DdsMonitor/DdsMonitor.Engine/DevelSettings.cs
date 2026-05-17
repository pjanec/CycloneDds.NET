using System;

namespace DdsMonitor.Engine;

/// <summary>
/// Runtime-mutable developer / debug settings.
/// Unlike <see cref="DdsSettings"/> these can be toggled live from the UI.
/// </summary>
public sealed class DevelSettings
{
    private bool _selfSendEnabled;
    private int _selfSendRateHz = 2;

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

    /// <summary>
    /// Gets or sets the live self-send rate in samples per second (per topic).
    /// Overrides <see cref="DdsSettings.SelfSendRateHz"/> when non-zero.
    /// Changing this while self-sending is active takes effect immediately.
    /// </summary>
    public int SelfSendRateHz
    {
        get => _selfSendRateHz;
        set
        {
            if (_selfSendRateHz == value)
            {
                return;
            }

            _selfSendRateHz = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
