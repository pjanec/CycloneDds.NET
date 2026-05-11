using System.Collections.Generic;
using Avalonia.Controls;
using DdsMonitor.Avalonia.Core;
using DdsMonitor.Engine;

namespace DdsMonitor.Avalonia.StandardPlugin;

/// <summary>
/// ViewModel for the Send Sample panel.
/// Dynamically builds editor controls for each field in the topic payload.
/// </summary>
public sealed class SendSampleViewModel
{
    private readonly TopicMetadata _meta;
    private readonly IDdsBridge _ddsBridge;
    private object _payload;
    private string? _sendError;
    private int _validationErrorCount;

    /// <summary>Gets the list of dynamically built editor controls, one per payload field.</summary>
    public List<Control> BuiltControls { get; } = new();

    /// <summary>Gets the last send error, or <c>null</c> if the last send succeeded.</summary>
    public string? SendError
    {
        get => _sendError;
        private set => _sendError = value;
    }

    /// <summary>Gets a value indicating whether the Send button should be enabled.</summary>
    public bool SendEnabled => _validationErrorCount == 0;

    /// <summary>
    /// Creates a new <see cref="SendSampleViewModel"/>.
    /// </summary>
    /// <param name="meta">Topic metadata describing the payload type.</param>
    /// <param name="drawerRegistry">Registry that builds editor controls per field type.</param>
    /// <param name="ddsBridge">DDS bridge used to obtain a writer and publish the payload.</param>
    /// <param name="initialPayload">
    /// Optional pre-populated payload.  When provided, used directly as the mutable payload
    /// object; when <c>null</c>, a fresh instance is created via <see cref="Activator.CreateInstance"/>.
    /// </param>
    public SendSampleViewModel(
        TopicMetadata meta,
        IAvaloniaTypeDrawerRegistry drawerRegistry,
        IDdsBridge ddsBridge,
        object? initialPayload = null)
    {
        _meta = meta ?? throw new ArgumentNullException(nameof(meta));
        _ddsBridge = ddsBridge ?? throw new ArgumentNullException(nameof(ddsBridge));

        _payload = initialPayload ?? Activator.CreateInstance(meta.TopicType)!;

        BuildControls(meta, drawerRegistry);
    }

    private void BuildControls(TopicMetadata meta, IAvaloniaTypeDrawerRegistry drawerRegistry)
    {
        foreach (var field in meta.AllFields)
        {
            if (field.IsSynthetic) continue;

            var capturedField = field;
            object? currentValue;
            try { currentValue = capturedField.Getter(_payload); }
            catch { currentValue = null; }

            var ctx = new AvaloniaDrawerContext(
                label: capturedField.DisplayName,
                targetType: capturedField.ValueType,
                value: currentValue,
                onChange: newVal =>
                {
                    try { capturedField.Setter(_payload, newVal); }
                    catch { /* ignore setter failures in V1 */ }
                },
                onValidationError: err =>
                {
                    if (err != null)
                        Interlocked.Increment(ref _validationErrorCount);
                    else
                        Interlocked.Decrement(ref _validationErrorCount);
                });

            Control control;
            try
            {
                control = drawerRegistry.Build(ctx);
            }
            catch
            {
                // Skip fields with no registered drawer — only supported types render in V1
                continue;
            }

            BuiltControls.Add(control);
        }
    }

    /// <summary>Publishes the current payload to DDS.</summary>
    public void Send()
    {
        try
        {
            SendError = null;
            var writer = _ddsBridge.GetWriter(new TopicMetadata(_meta.TopicType));
            writer.Write(_payload);
        }
        catch (Exception ex)
        {
            SendError = $"DDS Publish Failed: {ex.Message}";
        }
    }
}
