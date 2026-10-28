using System.Collections.Generic;

namespace DdsMonitor.Engine;

// ── ME1-T11: Headless-mode enum ──────────────────────────────────────────────

/// <summary>
/// Controls the execution mode for <c>HeadlessRunnerService</c>.
/// </summary>
public enum HeadlessMode
{
    /// <summary>Normal interactive UI mode; the Blazor server starts as usual.</summary>
    None,

    /// <summary>
    /// Record mode: listen for live DDS samples and stream them as JSON to
    /// <see cref="DdsSettings.HeadlessFilePath"/> until the process is interrupted.
    /// </summary>
    Record,

    /// <summary>
    /// Replay mode: load a previously recorded JSON file and publish every sample
    /// back onto the live DDS network at the configured <see cref="DdsSettings.ReplayRate"/>,
    /// then exit.
    /// </summary>
    Replay,
}

/// <summary>File format of a headless recording.</summary>
public enum RecordFormat
{
    /// <summary>One indented JSON array of samples (the default; the format the UI exports and imports).</summary>
    Json,

    /// <summary>
    /// JSON Lines: one compact, self-contained JSON object per line. The file is readable while it
    /// is being written (only whole lines are flushed) and a crash costs at most the last line.
    /// </summary>
    JsonLines,
}

/// <summary>What a headless recording prints to the console.</summary>
public enum RecordConsoleOutput
{
    /// <summary>One <c>[RECORD]</c> line per sample (the default).</summary>
    PerSample,

    /// <summary>A short status line every few seconds and a per-topic summary at the end, on stderr.</summary>
    Status,

    /// <summary>Nothing but warnings and errors.</summary>
    Quiet,
}

/// <summary>
/// Configuration for a single DDS participant (domain + optional partition).
/// </summary>
public sealed class ParticipantConfig
{
    /// <summary>Gets or sets the DDS domain identifier for this participant.</summary>
    public uint DomainId { get; set; } = 0;

    /// <summary>Gets or sets the optional partition name for this participant's readers.</summary>
    public string PartitionName { get; set; } = string.Empty;
}

/// <summary>
/// Configuration values for DDS Monitor hosting.
/// </summary>
public sealed class DdsSettings
{
    /// <summary>
    /// Gets the configuration section name.
    /// </summary>
    public const string SectionName = "DdsSettings";

    /// <summary>
    /// Gets the default DDS domain identifier.
    /// </summary>
    public const int DefaultDomainId = 0;

    /// <summary>
    /// Gets the default UI refresh frequency in Hz.
    /// </summary>
    public const int DefaultUiRefreshHz = 30;

    /// <summary>
    /// Gets or sets the list of participant configurations.
    /// When non-empty this takes precedence over the legacy <see cref="DomainId"/> field.
    /// Defaults to a single participant on domain 0.
    /// </summary>
    public List<ParticipantConfig> Participants { get; set; } = new()
    {
        new ParticipantConfig { DomainId = 0, PartitionName = string.Empty }
    };

    /// <summary>
    /// Gets or sets the DDS domain identifier (legacy single-participant configuration).
    /// Kept for backward compatibility with <c>--DdsSettings:DomainId=N</c> CLI usage.
    /// When <see cref="Participants"/> contains more than the default single entry, this
    /// property is ignored.
    /// </summary>
    public int DomainId { get; set; } = DefaultDomainId;

    /// <summary>
    /// Gets or sets the UI refresh frequency in Hz.
    /// </summary>
    public int UiRefreshHz { get; set; } = DefaultUiRefreshHz;

    /// <summary>
    /// Gets or sets a value indicating whether the monitor should generate self-send samples.
    /// </summary>
    public bool SelfSendEnabled { get; set; }

    /// <summary>
    /// Gets or sets the self-send sample rate per topic in Hz.
    /// </summary>
    public int SelfSendRateHz { get; set; } = 2;

    /// <summary>
    /// Gets or sets the number of distinct key values generated for self-send topics.
    /// </summary>
    public int SelfSendKeyCount { get; set; } = 6;

    /// <summary>
    /// Gets or sets an optional startup filter expression applied to all incoming samples.
    /// Samples not matching this expression are dropped before the global ordinal is incremented.
    /// Supports CLI-safe alphabetical operators (<c>ge</c>, <c>le</c>, <c>gt</c>, <c>lt</c>,
    /// <c>eq</c>, <c>ne</c>) in addition to the standard symbolic operators.
    /// </summary>
    public string? FilterExpression { get; set; }

    // ── ME1-T11: Headless record / replay ────────────────────────────────────

    /// <summary>
    /// Gets or sets the execution mode for the headless runner.
    /// Default is <see cref="HeadlessMode.None"/> (interactive Blazor UI).
    /// </summary>
    public HeadlessMode HeadlessMode { get; set; } = HeadlessMode.None;

    /// <summary>
    /// Gets or sets the file path used as the source (<c>Replay</c>) or destination
    /// (<c>Record</c>) for headless operation. When recording, <c>-</c> writes to stdout
    /// (logs then go to stderr).
    /// </summary>
    public string HeadlessFilePath { get; set; } = string.Empty;

    // ── Headless record options (all opt-in; the defaults keep the original behaviour) ──

    /// <summary>File format of a recording. Default: <see cref="RecordFormat.Json"/>.</summary>
    public RecordFormat RecordFormat { get; set; } = RecordFormat.Json;

    /// <summary>Stop the recording after this many seconds. 0 (default): record until interrupted.</summary>
    public double RecordDurationSeconds { get; set; }

    /// <summary>Stop the recording after this many samples. 0 (default): no limit.</summary>
    public long RecordMaxSamples { get; set; }

    /// <summary>Console output while recording. Default: <see cref="RecordConsoleOutput.PerSample"/>.</summary>
    public RecordConsoleOutput RecordConsole { get; set; } = RecordConsoleOutput.PerSample;

    /// <summary>
    /// Optional path of a small JSON summary (per-topic counts, failed topics, running/stopped),
    /// rewritten every second while recording and once at the end. Empty (default): none.
    /// </summary>
    public string RecordSummaryFile { get; set; } = string.Empty;

    /// <summary>
    /// <see cref="RecordFormat.JsonLines"/> only: start a new numbered file
    /// (<c>name.0001.jsonl</c>, <c>name.0002.jsonl</c>, ...) once the current one reaches this many MB.
    /// 0 (default): a single file.
    /// </summary>
    public int RecordMaxFileSizeMB { get; set; }

    /// <summary>
    /// Write fixed-size byte buffers (e.g. <c>fixed byte Payload[65536]</c>) as one base64 string
    /// instead of an array of numbers. Import and replay read both forms. Default: false.
    /// </summary>
    public bool RecordBytesAsBase64 { get; set; }

    /// <summary>
    /// Gets or sets the replay speed multiplier relative to the original capture rate.
    /// A value of <c>2.0</c> plays at twice the original speed.  Default: <c>1.0</c>.
    /// </summary>
    public float ReplayRate { get; set; } = 1.0f;
}
