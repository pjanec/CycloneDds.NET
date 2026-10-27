using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace CycloneDDS.Core.Tests;

/// <summary>
/// Every PowerShell script in the repository must be plain ASCII.
/// <para>Windows PowerShell 5.1 - the <c>powershell.exe</c> that ships with Windows - reads a script that has no UTF-8 byte-order
/// mark as Windows-1252. An em dash (<c>E2 80 94</c>) then arrives as three characters, and the third is a CURLY QUOTE, which
/// PowerShell treats as a string delimiter: the string ends early and the rest of the line, sometimes a whole block, is parsed
/// as something else. <c>build/test-package.ps1</c> lost its entire ddsmonitor check this way and reported
/// "FAILED (started=, http=)" with an empty log. PowerShell 7 reads UTF-8 and never shows it, so it passes in CI and fails on a
/// developer's Windows machine.</para>
/// </summary>
public sealed class PowerShellScriptTests
{
    private static readonly string[] SkippedDirectories =
        { "bin", "obj", "artifacts", "node_modules", ".git", "cyclonedds" };   // "cyclonedds" is the upstream submodule, not ours

    [Fact]
    public void EveryPowerShellScriptInTheRepository_IsPlainAscii()
    {
        var root = FindRepositoryRoot();
        var scripts = EnumerateScripts(root).ToList();
        Assert.NotEmpty(scripts);   // a wrong root must not make this test vacuous

        var offenders = new List<string>();
        foreach (var path in scripts)
        {
            var bytes = File.ReadAllBytes(path);
            int line = 1;
            for (int i = 0; i < bytes.Length; i++)
            {
                if (bytes[i] == (byte)'\n') line++;
                if (bytes[i] > 0x7F)
                {
                    var relative = Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');
                    offenders.Add(relative + ":" + line + "  (byte 0x" + bytes[i].ToString("X2") + ")");
                    break;   // first offence per file is enough to point at it
                }
            }
        }

        Assert.True(offenders.Count == 0,
            "PowerShell scripts must be ASCII-only (Windows PowerShell 5.1 reads a BOM-less file as Windows-1252, and the curly quote " +
            "inside an em dash ends the string early). Replace the character with plain ASCII - '-' for a dash, '[+]' for a check mark:\n  " +
            string.Join("\n  ", offenders));
    }

    private static IEnumerable<string> EnumerateScripts(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory))
        {
            var ext = Path.GetExtension(file);
            if (ext.Equals(".ps1", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".psm1", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".psd1", StringComparison.OrdinalIgnoreCase))
            {
                yield return file;
            }
        }

        foreach (var sub in Directory.EnumerateDirectories(directory))
        {
            if (SkippedDirectories.Contains(Path.GetFileName(sub), StringComparer.OrdinalIgnoreCase)) continue;
            foreach (var file in EnumerateScripts(sub)) yield return file;
        }
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "CycloneDDS.NET.sln"))) dir = dir.Parent;
        Assert.True(dir != null, "CycloneDDS.NET.sln not found above " + AppContext.BaseDirectory);
        return dir!.FullName;
    }
}
