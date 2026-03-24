namespace DdsMonitor.Engine.Hosting;

/// <summary>
/// Configuration for browser-lifecycle-aware shutdown behaviour (ME1-T10).
/// </summary>
public sealed class BrowserLifecycleOptions
{
    /// <summary>
    /// Gets or sets the number of seconds the application waits for a browser connection
    /// after startup before shutting down automatically.
    /// Default: 15 seconds.
    /// </summary>
    public int ConnectTimeout { get; set; } = 15;

    /// <summary>
    /// Gets or sets the number of seconds the application waits after all browser tabs
    /// have disconnected before shutting down.
    /// Default: 5 seconds.
    /// </summary>
    public int DisconnectTimeout { get; set; } = 5;

    /// <summary>
    /// When <c>true</c> the application runs indefinitely without waiting for a browser
    /// to connect and without shutting down when the browser disconnects.
    /// Set automatically when <c>--NoBrowser true</c> is used so the app survives tabs
    /// being closed or the browser being killed.
    /// </summary>
    public bool KeepAlive { get; set; }
}
