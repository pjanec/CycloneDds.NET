namespace DdsMonitor.Components;

/// <summary>
/// Represents a single entry (file or directory) shown in the <see cref="FileDialog"/> listing.
/// </summary>
internal sealed record FileEntry(string Name, string FullPath, bool IsDirectory, long SizeBytes);
