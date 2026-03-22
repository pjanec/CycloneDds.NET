using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DdsMonitor.Plugins.Bdc;

/// <summary>
/// Live configuration for the BDC plugin. All fields are mutable so the
/// <see cref="BdcSettingsPanel"/> can bind to them directly.
/// When any property changes, <see cref="SettingsChanged"/> is raised so that
/// <see cref="EntityStore"/> can reset and re-aggregate.
/// </summary>
public sealed class BdcSettings : INotifyPropertyChanged
{
    private string _namespacePrefix = string.Empty;
    private string _entityIdPattern = @"(?i)\bEntityId\b";
    private string _partIdPattern = @"(?i)\bPartId\b";
    private string _masterTopicPattern = @"Master$";

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raised when any setting changes. <see cref="EntityStore"/> subscribes to this
    /// event to trigger a full re-aggregation pass.
    /// </summary>
    public event Action? SettingsChanged;

    /// <summary>
    /// Gets or sets the DDS topic namespace prefix used to filter incoming instance
    /// events.  Only topics whose <c>TopicMetadata.Namespace</c> starts with this
    /// string will be processed.  An empty string means "accept all namespaces".
    /// </summary>
    public string NamespacePrefix
    {
        get => _namespacePrefix;
        set => SetField(ref _namespacePrefix, value);
    }

    /// <summary>
    /// Gets or sets the regex pattern evaluated against
    /// <c>FieldMetadata.StructuredName</c> to locate the EntityId key field.
    /// </summary>
    public string EntityIdPattern
    {
        get => _entityIdPattern;
        set => SetField(ref _entityIdPattern, value);
    }

    /// <summary>
    /// Gets or sets the regex pattern evaluated against
    /// <c>FieldMetadata.StructuredName</c> to locate the optional PartId key field.
    /// </summary>
    public string PartIdPattern
    {
        get => _partIdPattern;
        set => SetField(ref _partIdPattern, value);
    }

    /// <summary>
    /// Gets or sets the regex pattern evaluated against
    /// <c>DescriptorIdentity.TopicName</c> to decide whether a descriptor is the
    /// entity's Master (i.e. determines <see cref="EntityState.Alive"/>).
    /// </summary>
    public string MasterTopicPattern
    {
        get => _masterTopicPattern;
        set => SetField(ref _masterTopicPattern, value);
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value))
            return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        SettingsChanged?.Invoke();
    }
}
