using System;
using System.Diagnostics;
using System.IO;

namespace CycloneDDS.Compiler.Common
{
    public class IdlcRunner
    {
        public string? IdlcPathOverride { get; set; }

        public string FindIdlc()
        {
            if (!string.IsNullOrEmpty(IdlcPathOverride))
            {
                if (File.Exists(IdlcPathOverride)) return IdlcPathOverride;
                throw new FileNotFoundException($"idlc.exe not found at override path: {IdlcPathOverride}");
            }

            // Check current directory (where DLLs are)
            string currentDir = AppDomain.CurrentDomain.BaseDirectory;
            string localIdlc = Path.Combine(currentDir, "idlc.exe");
            if (File.Exists(localIdlc)) return localIdlc;

            // Check NuGet package location relative to tools/ (tools/ -> ../runtimes/win-x64/native/)
            try 
            {
               string nugetNativePath = Path.Combine(currentDir, "..", "runtimes", "win-x64", "native", "idlc.exe");
               if (File.Exists(nugetNativePath)) return Path.GetFullPath(nugetNativePath);
            }
            catch {}

            // DEV: Check workspace location (for tests/dev)
            // Iterate up 6 levels looking for cyclonedds/install/bin/idlc.exe OR cyclone-compiled/bin/idlc.exe
            var searchDir = new DirectoryInfo(currentDir);
            for (int i = 0; i < 6; i++)
            {
                if (searchDir == null) break;
                
                string checkPath = Path.Combine(searchDir.FullName, "cyclonedds", "install", "bin", "idlc.exe");
                if (File.Exists(checkPath)) return checkPath;
                
                string repoPath = Path.Combine(searchDir.FullName, "cyclone-compiled", "bin", "idlc.exe");
                if (File.Exists(repoPath)) return repoPath;

                repoPath = Path.Combine(searchDir.FullName, "artifacts", "native", "win-x64", "idlc.exe");
                if (File.Exists(repoPath)) return repoPath;

                searchDir = searchDir.Parent;
            }

            // Check environment variable
            string? cycloneHome = Environment.GetEnvironmentVariable("CYCLONEDDS_HOME");
            if (!string.IsNullOrEmpty(cycloneHome))
            {
                string path = Path.Combine(cycloneHome, "bin", "idlc.exe");
                if (File.Exists(path))
                    return path;
                
                // Try without bin?
                path = Path.Combine(cycloneHome, "idlc.exe");
                if (File.Exists(path))
                    return path;
            }
            
            // Check PATH
            string? pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (pathEnv != null)
            {
                foreach (var dir in pathEnv.Split(Path.PathSeparator))
                {
                    try 
                    {
                        string path = Path.Combine(dir, "idlc.exe");
                        if (File.Exists(path))
                            return path;
                    }
                    catch { /* Ignore invalid paths in PATH */ }
                }
            }
            
            throw new FileNotFoundException("idlc.exe not found. Set CYCLONEDDS_HOME or add to PATH.");
        }

        public IdlcResult RunIdlc(string idlFilePath, string outputDir, string? includePath = null)
        {
            string idlcPath = FindIdlc();
            
            // Ensure output directory exists
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = idlcPath,
                Arguments = GetArguments(idlFilePath, outputDir, includePath),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            
            using var process = Process.Start(startInfo);
            if (process == null)
            {
                throw new Exception("Failed to start idlc process.");
            }
            
            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            
            process.WaitForExit();
            
            return new IdlcResult
            {
                ExitCode = process.ExitCode,
                StandardOutput = stdout,
                StandardError = stderr,
                GeneratedFiles = FindGeneratedFiles(outputDir, idlFilePath)
            };
        }

        public string GetArguments(string idlFilePath, string outputDir, string? includePath)
        {
            var args = $"-l json -o \"{outputDir}\"";
            if (!string.IsNullOrEmpty(includePath))
            {
                args += $" -I \"{includePath}\"";
            }
            args += $" \"{idlFilePath}\"";
            return args;
        }
        
        private string[] FindGeneratedFiles(string outputDir, string idlFile)
        {
            // idlc -l json generates: <basename>.json
            string baseName = Path.GetFileNameWithoutExtension(idlFile);
            var jsonFile = Path.Combine(outputDir, baseName + ".json");
            
            var files = new System.Collections.Generic.List<string>();
            if (File.Exists(jsonFile)) files.Add(jsonFile);
            
            return files.ToArray();
        }
    }
}
