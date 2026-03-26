using CycloneDDS.Schema;

namespace DdsMonitor.Blazor.Tests;

/// <summary>
/// Minimal DDS topic types used by Blazor.Tests to create real <see cref="DdsMonitor.Engine.TopicMetadata"/>
/// instances without needing CycloneDDS code-generation.  DEBT-011 alignment: tests must register
/// viewers keyed by the topic type (<c>TopicMetadata.TopicType</c>), matching the production
/// <c>DetailPanel.RenderTreeView</c> lookup.
/// </summary>
[DdsTopic("FooTopic")]
public struct FooTopicType { }

[DdsTopic("BarTopic")]
public struct BarTopicType { }
