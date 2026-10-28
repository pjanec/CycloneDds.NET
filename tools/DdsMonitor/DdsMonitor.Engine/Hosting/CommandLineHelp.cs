using System;
using System.Linq;

namespace DdsMonitor.Engine.Hosting;

/// <summary>
/// The text of <c>ddsmonitor --help</c>: a compact reference of every command-line argument and
/// the intended way of recording, so that a person or an AI agent can use the tool from the
/// installed package alone. A test keeps it in step with <see cref="DdsSettings"/> and
/// <see cref="AppSettings"/>; the README has the long form.
/// </summary>
public static class CommandLineHelp
{
    /// <summary>The arguments that print the help and exit.</summary>
    public static bool IsHelpRequest(string[] args) =>
        args.Any(a => a is "--help" or "-h" or "-?" or "/?");

    /// <summary>The arguments that print the version and exit.</summary>
    public static bool IsVersionRequest(string[] args) =>
        args.Any(a => a is "--version");

    public const string ReadmeUrl = "https://github.com/pjanec/CycloneDds.NET/blob/main/tools/DdsMonitor/README.md";

    public static string Text(string version) => $$"""
        ddsmonitor {{version}} - DDS monitor for CycloneDDS.NET topics: a browser UI, or headless record/replay.

        USAGE
          ddsmonitor [--Section:Key=Value ...]       arrays: --Section:Key:0=a --Section:Key:1=b
          ddsmonitor --help | --version

        TOPICS
          Topics are known from the assemblies holding their [DdsTopic] types, not from the network:
          point at the DLLs or build folders (otherwise only the saved list and the built-in SelfTest.*).
          --AppSettings:TopicSources:0=<dll|dir>     assemblies to scan (overrides the saved list for this run)
          --AppSettings:IncludeTopics:0=<pattern>    subscribe only to matching types ("MyApp.*"; CLR full name)
          --AppSettings:ExcludeTopics:0=<pattern>    do not subscribe to matching types

        DDS
          --DdsSettings:DomainId=<n>                 domain (default 0)
          --DdsSettings:Participants:0:DomainId=<n>  one or more participants (overrides DomainId)
          --DdsSettings:Participants:0:PartitionName=<name>
          --DdsSettings:FilterExpression=<expr>      drop samples not matching, e.g. "Payload.Id gt 10"
                                                     (ops: == != > < >= <= or eq ne gt lt ge le)
          CYCLONEDDS_URI=<xml|file://...>            (environment) Cyclone config, e.g. DDS Security

        HEADLESS RECORD (no UI)
          --DdsSettings:HeadlessMode=Record
          --DdsSettings:HeadlessFilePath=<path|->    capture file; "-" = stdout (logs go to stderr)
          --DdsSettings:RecordFormat=JsonLines|Json  default JsonLines: one record per line, readable
                                                     while recording; Json = one array, readable once closed
          --DdsSettings:RecordDurationSeconds=<s>    stop after s seconds (default: until Ctrl+C/SIGTERM)
          --DdsSettings:RecordMaxSamples=<n>         stop after n samples
          --DdsSettings:RecordSummaryFile=<path>     per-topic counts, failed topics, Running/StopReason;
                                                     rewritten every second
          --DdsSettings:RecordConsole=Quiet|Status|PerSample   default Quiet (warnings/errors only)
          --DdsSettings:RecordMaxFileSizeMB=<mb>     JsonLines: roll over to name.0001.jsonl, name.0002.jsonl...
          --DdsSettings:RecordBytesAsBase64=<bool>   byte buffers as base64 (default true)
          Record line: {"Ordinal","TopicName","TopicTypeName","Timestamp","DomainId","PartitionName",
                        "Sender","InstanceState","Payload"}. Ordinals increase through the capture.

        HEADLESS REPLAY
          --DdsSettings:HeadlessMode=Replay --DdsSettings:HeadlessFilePath=<capture>
          --DdsSettings:ReplayRate=<x>               speed factor (default 1.0); FilterExpression applies

        RECORDING FOR SCRIPTS AND AI AGENTS
          ddsmonitor --DdsSettings:HeadlessMode=Record --DdsSettings:HeadlessFilePath=cap.jsonl \
                     --DdsSettings:RecordSummaryFile=cap.summary.json \
                     --AppSettings:TopicSources:0=./bin/MyTopics.dll --DdsSettings:RecordDurationSeconds=120 &
          jq '.Topics[] | {TopicName, Count, ReadError}' cap.summary.json   # arriving? (Count 0 = nothing yet)
          jq -c 'select(.Ordinal > 1200)' cap.jsonl                         # new since the last look
          jq -c 'select(.TopicName == "my/topic") | .Payload' cap.jsonl     # one topic
          The last line may be partial while recording: skip a line without a trailing newline.
          Exit codes: 0 ok, 2 run failed (bad arguments), 3 some topic could not be recorded.

        UI (default when HeadlessMode is not set)
          --NoBrowser true | --AppSettings:NoBrowser=true   serve only, do not open a browser
          --BrowserPort=<port>                       HTTP port (default: a free one)
          --BrowserLifecycle:ConnectTimeout=<s>      exit if no browser connects in s seconds (15)
          --BrowserLifecycle:DisconnectTimeout=<s>   exit s seconds after the last tab closes (5)
          --BrowserLifecycle:KeepAlive=true          ignore browser state
          --DdsSettings:UiRefreshHz=<n>              UI refresh limit (30)
          --AppSettings:WorkspaceFile=<path>         workspace (layout, filters) to load
          --AppSettings:ConfigFolder=<dir>           folder for saved state (workspace, assembly sources)

        TESTING
          --DdsSettings:SelfSendEnabled=true         publish demo samples (SelfTest.* topics)
          --DdsSettings:SelfSendRateHz=<n>  --DdsSettings:SelfSendKeyCount=<n>

        More: {{ReadmeUrl}}
        """;
}
