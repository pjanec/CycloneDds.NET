using System;
using System.IO;

namespace DdsMonitor.Engine.Hosting;

/// <summary>
/// Makes the monitor's own folder part of the process <c>PATH</c> on Windows.
/// </summary>
/// <remarks>
/// Cyclone DDS loads its DDS Security plugins (<c>dds_security_*.dll</c>) by name when a secured participant is
/// created. Windows searches the folder of the started <em>executable</em>, then the system folders, then <c>PATH</c>.
/// For a normal application the plugins sit beside its executable. A <c>dotnet tool</c> is different: the
/// <c>ddsmonitor.exe</c> launcher lives in the tools folder while the assemblies and plugins live in the tool's
/// store folder, so the secured participant failed with "Could not load Authentication library".
/// Linux resolves the plugins relative to the Cyclone DDS library itself and needs nothing.
/// </remarks>
public static class NativeSearchPath
{
    /// <summary>
    /// Puts <paramref name="directory"/> at the front of <paramref name="path"/> unless it is already an entry.
    /// Returns <paramref name="path"/> unchanged when nothing needs adding.
    /// </summary>
    public static string WithDirectory(string? path, string directory, char separator)
    {
        var trimmed = directory.TrimEnd('\\', '/');
        if (trimmed.Length == 0)
            return path ?? string.Empty;
        if (string.IsNullOrEmpty(path))
            return trimmed;

        foreach (var entry in path.Split(separator, StringSplitOptions.RemoveEmptyEntries))
        {
            if (string.Equals(entry.Trim().TrimEnd('\\', '/'), trimmed, StringComparison.OrdinalIgnoreCase))
                return path;
        }
        return trimmed + separator + path;
    }

    /// <summary>Adds the application folder to this process's <c>PATH</c> (Windows only; no-op elsewhere).</summary>
    public static void AddApplicationDirectory()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var current = Environment.GetEnvironmentVariable("PATH");
        var updated = WithDirectory(current, AppContext.BaseDirectory, Path.PathSeparator);
        if (updated != current)
            Environment.SetEnvironmentVariable("PATH", updated);
    }
}
