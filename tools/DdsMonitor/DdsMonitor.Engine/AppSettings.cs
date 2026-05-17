using System;

namespace DdsMonitor.Engine;

/// <summary>
/// Application-level startup options (workspace layout and configuration folder).
/// These are set via command-line arguments:
///   --AppSettings:WorkspaceFile=/path/to/layout.json
///   --AppSettings:ConfigFolder=/path/to/config/dir
/// </summary>
public sealed class AppSettings
{
    /// <summary>
    /// Gets the configuration section name.
    /// </summary>
    public const string SectionName = "AppSettings";

    /// <summary>
    /// Gets or sets an optional path to a saved layout file to load on startup.
    /// When set, this overrides the default per-user workspace location.
    /// </summary>
    public string? WorkspaceFile { get; set; }

    /// <summary>
    /// Gets or sets an optional folder path for storing application configuration files
    /// (workspace, topic colours, etc.).  Overrides the default <c>%APPDATA%\DdsMonitor</c>
    /// location when provided.
    /// </summary>
    public string? ConfigFolder { get; set; }

    /// <summary>
    /// When true the application will not attempt to open the system browser
    /// automatically after starting the Blazor host. Supports CLI usage
    /// `--NoBrowser true` or `--AppSettings:NoBrowser=true`.
    /// </summary>
    public bool NoBrowser { get; set; }

    /// <summary>
    /// Optional list of fully-qualified CLR type names (or glob patterns such as
    /// <c>FeatureDemo.Scenarios.*</c>) to <em>explicitly include</em> in the initial
    /// subscription set.  When non-empty the initial set starts empty and is extended
    /// only by topics matching these patterns; any saved exclusion list is ignored.
    /// Supports CLI usage <c>--AppSettings:IncludeTopics:0=Ns.TypeA --AppSettings:IncludeTopics:1=Ns.*</c>.
    /// </summary>
    public string[] IncludeTopics { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Optional list of fully-qualified CLR type names (or glob patterns) to explicitly
    /// exclude from the initial subscription set.  When <see cref="IncludeTopics"/> is also
    /// provided, excludes are applied after the include pass.  When used alone the saved
    /// exclusion list is ignored.
    /// Supports CLI usage <c>--AppSettings:ExcludeTopics:0=Ns.TypeB</c>.
    /// </summary>
    public string[] ExcludeTopics { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Optional list of directory or DLL paths to scan for topic assemblies.
    /// When non-empty, overrides the persisted <c>assembly-sources.json</c> list for this
    /// launch without modifying the saved file.  Use this to run the monitor against a
    /// specific build output without changing the per-user configuration.
    /// Supports CLI usage <c>--AppSettings:TopicSources:0=C:\MyApp\bin\Debug\net8.0</c>.
    /// </summary>
    public string[] TopicSources { get; set; } = Array.Empty<string>();
}
