using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using CycloneDDS.Runtime;
using Imp;
using Xunit;

namespace CycloneDDS.Runtime.Tests
{
    /// <summary>
    /// Imported/ImportedTypes.cs is the IDL importer's output for Imported/ImportedTypes.idl. The code
    /// generator turns it back into IDL; that IDL must give idlc the same serializer ops and type
    /// information as the original (the same wire format and type identity), and every member must
    /// survive a write/read.
    /// </summary>
    public unsafe class ImportedIdlTests
    {
        private static readonly string ProjectDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));

        [Fact]
        public void RegeneratedIdl_GivesIdlcTheSameOpsAndTypeInformation()
        {
            string original = Path.Combine(ProjectDir, "Imported", "ImportedTypes.idl");
            string generatedDir = Path.Combine(ProjectDir, "obj", "Generated");
            string regenerated = Path.Combine(generatedDir, "ImportedTypes.idl");
            Assert.True(File.Exists(regenerated), $"{regenerated} not found");

            var (origOps, origInfo) = CompileWithIdlc(original, generatedDir);
            var (regenOps, regenInfo) = CompileWithIdlc(regenerated, generatedDir);

            foreach (var topic in new[] { "Imp_AllKinds", "Imp_Mutable", "Imp_Unions", "Imp_WithTypedefs" })
                Assert.Contains(topic, origOps.Keys);
            foreach (var type in origOps.Keys)
            {
                Assert.True(regenOps.ContainsKey(type), $"{type} missing from the regenerated IDL");
                Assert.True(origOps[type] == regenOps[type], $"{type}: ops differ\noriginal:    {origOps[type]}\nregenerated: {regenOps[type]}");
                // Typedefs are resolved, so the type information names the resolved types (wire format unchanged).
                if (type != "Imp_WithTypedefs")
                    Assert.True(origInfo[type] == regenInfo[type], $"{type}: type information differs");
            }
            Assert.NotEqual(origInfo["Imp_WithTypedefs"], regenInfo["Imp_WithTypedefs"]);
        }

        /// <summary>Runs idlc to C and returns each topic type's ops and TYPE_INFO_CDR bytes.</summary>
        private static (Dictionary<string, string> Ops, Dictionary<string, string> TypeInfo) CompileWithIdlc(string idl, string includeDir)
        {
            string idlc = Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "idlc.exe" : "idlc");
            string outDir = Path.Combine(Path.GetTempPath(), "ImportedIdl_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(outDir);
            try
            {
                var psi = new ProcessStartInfo(idlc) { RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false };
                foreach (var arg in new[] { "-I", includeDir, "-o", outDir, idl }) psi.ArgumentList.Add(arg);
                using (var p = Process.Start(psi)!)
                {
                    string err = p.StandardError.ReadToEnd();
                    p.StandardOutput.ReadToEnd();
                    p.WaitForExit();
                    Assert.True(p.ExitCode == 0, $"idlc failed on {idl}: {err}");
                }

                string c = File.ReadAllText(Path.Combine(outDir, Path.GetFileNameWithoutExtension(idl) + ".c"));
                var ops = Regex.Matches(c, @"static const uint32_t (\w+)_ops \[\] =\s*\{(.*?)\};", RegexOptions.Singleline)
                    .ToDictionary(m => m.Groups[1].Value, m => Regex.Replace(Regex.Replace(m.Groups[2].Value, @"/\*.*?\*/", ""), @"\s+", ""));
                var info = Regex.Matches(c, @"#define TYPE_INFO_CDR_(\w+) \(const unsigned char \[\]\)\{(.*?)\}\s*\n", RegexOptions.Singleline)
                    .ToDictionary(m => m.Groups[1].Value, m => Regex.Replace(m.Groups[2].Value, @"[\s\\]", ""));
                return (ops, info);
            }
            finally
            {
                try { Directory.Delete(outDir, true); } catch { }
            }
        }

        private static T RoundTrip<T>(T sample) where T : struct
        {
            string topic = "Imported_" + Guid.NewGuid().ToString("N");
            using var participant = new DdsParticipant();
            using var reader = new DdsReader<T>(participant, topic);
            using var writer = new DdsWriter<T>(participant, topic);
            Assert.True(writer.WaitForReaderAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult());
            writer.Write(sample);
            for (int i = 0; i < 50; i++)
            {
                using (var loan = reader.Take())
                    foreach (var s in loan)
                        if (s.IsValid) return s.Data;
                Thread.Sleep(100);
            }
            throw new TimeoutException("no sample");
        }

        [Fact]
        public void AllKinds_EveryMemberRoundTrips()
        {
            var s = new AllKinds
            {
                id = 1, b = true, c = (byte)'q', o = 200, i8 = -5, u8 = 250, sh = -300, us = 60000, l = -70000, ul = 4000000000,
                ll = -5_000_000_000, ull = 18_000_000_000_000_000_000, f = 1.5f, d = -2.25,
                u = "unbounded", s5 = "five5", col = Color.BLUE, sm = Small.Y, md = Mid.M2,
                sl = new List<int> { 1, 2 }, bsl = new List<int> { 3 }, si = new List<Inner> { new Inner { a = 4, s = "in" } },
                sms = new List<Small> { Small.Y, Small.X }, bcs = new List<Color> { Color.GREEN, Color.BLUE },
                chars = new List<byte> { (byte)'h', (byte)'i' }, u8s = new List<byte> { 9, 255 }, bools = new List<bool> { true, false, true },
                words = new List<string> { "a", "bc" }, bwords = new List<string> { "x" },
                inner = new Inner { a = 42, s = "nested" },
                ol = 11, os = "opt", oi = new Inner { a = 12, s = "oi" }, osq = new List<int> { 13 }, oc = Color.RED,
            };
            for (int i = 0; i < 16; i++) s.name[i] = (byte)('A' + i);
            s.flags[0] = true; s.flags[2] = true;
            for (int i = 0; i < 6; i++) s.m[i] = i + 0.5;
            for (int i = 0; i < 4; i++) s.raw[i] = (byte)(250 + i);

            var r = RoundTrip(s);

            Assert.Equal((true, (byte)'q', (byte)200, (sbyte)-5, (byte)250), (r.b, r.c, r.o, r.i8, r.u8));
            Assert.Equal(((short)-300, (ushort)60000, -70000, 4000000000u), (r.sh, r.us, r.l, r.ul));
            Assert.Equal((-5_000_000_000L, 18_000_000_000_000_000_000UL, 1.5f, -2.25), (r.ll, r.ull, r.f, r.d));
            for (int i = 0; i < 16; i++) Assert.Equal((byte)('A' + i), r.name[i]);
            Assert.Equal((true, false, true), (r.flags[0], r.flags[1], r.flags[2]));
            for (int i = 0; i < 6; i++) Assert.Equal(i + 0.5, r.m[i]);
            for (int i = 0; i < 4; i++) Assert.Equal((byte)(250 + i), r.raw[i]);
            Assert.Equal(("unbounded", "five5"), (r.u, r.s5));
            Assert.Equal((Color.BLUE, Small.Y, Mid.M2), (r.col, r.sm, r.md));
            Assert.Equal(new[] { 1, 2 }, r.sl);
            Assert.Equal(new[] { 3 }, r.bsl);
            Assert.Equal((4, "in"), (r.si.Single().a, r.si.Single().s));
            Assert.Equal(new[] { Small.Y, Small.X }, r.sms);
            Assert.Equal(new[] { Color.GREEN, Color.BLUE }, r.bcs);
            Assert.Equal(new byte[] { (byte)'h', (byte)'i' }, r.chars);
            Assert.Equal(new byte[] { 9, 255 }, r.u8s);
            Assert.Equal(new[] { true, false, true }, r.bools);
            Assert.Equal(new[] { "a", "bc" }, r.words);
            Assert.Equal(new[] { "x" }, r.bwords);
            Assert.Equal((42, "nested"), (r.inner.a, r.inner.s));
            Assert.Equal((11, "opt", 12, "oi", Color.RED), (r.ol, r.os, r.oi!.Value.a, r.oi.Value.s, r.oc));
            Assert.Equal(new[] { 13 }, r.osq);
        }

        [Fact]
        public void Mutable_RoundTrips()
        {
            var r = RoundTrip(new Mutable { id = 1, x = 2.5, name = "m" });
            Assert.Equal((1, 2.5, "m"), (r.id, r.x, r.name));
        }

        [Theory]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(9)]
        public void Unions_EveryDiscriminatorKindRoundTrips(int longCase)
        {
            var u = new Unions
            {
                id = longCase,
                lu = longCase == 9 ? new LongU { _d = 9, d = 1.25 } : new LongU { _d = longCase, s = "two-or-three" },
                bu = longCase == 2 ? new BoolU { _d = true, t = 7 } : new BoolU { _d = false, f = 3.5 },
                cu = new ColorU { _d = Color.BLUE, b = "blue" },
                chu = longCase == 9 ? new CharU { _d = (byte)'z', z = 0.75 } : new CharU { _d = (byte)(longCase == 2 ? 'a' : 'b'), ab = 66 },
                u8u = new Uint8U { _d = 200, c = (byte)'d' },
                mu = new MutableU { _d = Small.Y, y = "why" },
            };

            var r = RoundTrip(u);

            if (longCase == 9) Assert.Equal((9, 1.25), (r.lu._d, r.lu.d));
            else Assert.Equal((longCase, "two-or-three"), (r.lu._d, r.lu.s));
            if (longCase == 2) Assert.Equal((true, 7), (r.bu._d, r.bu.t));
            else Assert.Equal((false, 3.5), (r.bu._d, r.bu.f));
            Assert.Equal((Color.BLUE, "blue"), (r.cu._d, r.cu.b));
            if (longCase == 9) Assert.Equal(((byte)'z', 0.75), (r.chu._d, r.chu.z));
            else Assert.Equal(((byte)(longCase == 2 ? 'a' : 'b'), 66), (r.chu._d, r.chu.ab));
            Assert.Equal(((byte)200, (byte)'d'), (r.u8u._d, r.u8u.c));
            Assert.Equal((Small.Y, "why"), (r.mu._d, r.mu.y));
        }

        [Fact]
        public void WithTypedefs_RoundTrips()
        {
            var s = new WithTypedefs { id = 1, ls = new List<int> { 1, 2, 3, 4 }, nm = "eight888" };
            for (int i = 0; i < 4; i++) s.mat[i] = i * 10;
            var r = RoundTrip(s);
            Assert.Equal(new[] { 1, 2, 3, 4 }, r.ls);
            Assert.Equal("eight888", r.nm);
            for (int i = 0; i < 4; i++) Assert.Equal(i * 10, r.mat[i]);
        }
    }
}
