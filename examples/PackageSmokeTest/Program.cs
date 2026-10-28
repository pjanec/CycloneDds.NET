using System;
using System.Threading;
using System.Runtime.InteropServices;
using CycloneDDS.Runtime;
using CycloneDDS.Schema;

namespace PackageSmokeTest
{
    // Declaring a [DdsTopic] forces the package's code generator (idlc) to run at
    // build time — so a successful build already proves the packaged tooling works
    // on this OS. Main() then proves the runtime native (libddsc.so / ddsc.dll)
    // works by doing a real publish -> subscribe round-trip.
    [DdsTopic("PackageSmokeTest_SmokeSample")]
    public partial struct SmokeSample
    {
        [DdsKey] public int Id;
        public double Value;
        public FixedString32 Label;
    }

    public static class Program
    {
        public static int Main(string[] args)
        {
            if (args.Length == 3 && args[0] == "publish")
                return Publish(uint.Parse(args[1]), double.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture));

            Console.WriteLine($"[smoke] {RuntimeInformation.OSDescription} / {RuntimeInformation.ProcessArchitecture}");
            try
            {
                using var participant = new DdsParticipant();
                using var writer = new DdsWriter<SmokeSample>(participant);
                using var reader = new DdsReader<SmokeSample>(participant);

                // Wait for the reader/writer to discover each other so the first
                // sample isn't dropped. This also exercises the matched-status
                // listener callback (the ABI fix that was broken on Linux).
                if (!writer.WaitForReaderAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult())
                {
                    Console.Error.WriteLine("[smoke] FAIL: reader/writer did not discover each other");
                    return 1;
                }

                var sent = new SmokeSample
                {
                    Id = 42,
                    Value = 3.14159,
                    Label = new FixedString32("hello-cyclonedds")
                };
                writer.Write(sent);

                SmokeSample? received = null;
                for (int attempt = 0; attempt < 50 && received is null; attempt++)
                {
                    using (var loan = reader.Take(maxSamples: 10))
                    {
                        foreach (var sample in loan)
                        {
                            if (sample.IsValid) { received = sample.Data; break; }
                        }
                    }
                    if (received is null) Thread.Sleep(100);
                }

                if (received is null)
                {
                    Console.Error.WriteLine("[smoke] FAIL: no sample received within timeout");
                    return 1;
                }

                var got = received.Value;
                bool ok = got.Id == sent.Id
                          && Math.Abs(got.Value - sent.Value) < 1e-9
                          && got.Label.ToString() == sent.Label.ToString();

                if (!ok)
                {
                    Console.Error.WriteLine(
                        $"[smoke] FAIL: round-trip mismatch: Id={got.Id} Value={got.Value} Label={got.Label}");
                    return 1;
                }

                Console.WriteLine($"[smoke] PASS: round-trip Id={got.Id} Value={got.Value} Label={got.Label}");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[smoke] FAIL: {ex}");
                return 1;
            }
        }

        /// <summary>
        /// <c>publish &lt;domain&gt; &lt;seconds&gt;</c>: writes a SmokeSample every 100 ms, so the package
        /// test can record it with the ddsmonitor tool (which loads this assembly's topic type).
        /// CYCLONEDDS_URI applies as for any Cyclone DDS application (the DDS Security check uses it).
        /// </summary>
        private static int Publish(uint domainId, double seconds)
        {
            using var participant = new DdsParticipant(domainId);
            using var writer = new DdsWriter<SmokeSample>(participant);
            var end = DateTime.UtcNow.AddSeconds(seconds);
            int id = 0;
            while (DateTime.UtcNow < end)
            {
                writer.Write(new SmokeSample { Id = id++, Value = id * 0.5, Label = new FixedString32("published") });
                Thread.Sleep(100);
            }
            Console.WriteLine($"[smoke] published {id} samples on domain {domainId}");
            return 0;
        }
    }
}
