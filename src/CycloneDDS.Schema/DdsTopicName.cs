using System;
using System.Collections.Generic;
using System.Reflection;

namespace CycloneDDS.Schema;

/// <summary>
/// The single rule that turns a topic type into its DDS topic name. Shared by the
/// runtime (<c>DdsWriter</c>/<c>DdsReader</c>), the code generator and DdsMonitor so
/// all of them agree on the spelling.
/// </summary>
/// <remarks>
/// An explicit, non-whitespace <see cref="DdsTopicAttribute.TopicName"/> always wins.
/// Otherwise the name is the namespace, the containing types and the type name, joined
/// with <c>_</c>: <c>A.B.Outer.Inner</c> becomes <c>A_B_Outer_Inner</c>.
/// </remarks>
public static class DdsTopicName
{
    /// <summary>
    /// Returns the DDS topic name of <typeparamref name="T"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The type has no <see cref="DdsTopicAttribute"/>.</exception>
    public static string Of<T>() => Of(typeof(T));

    /// <summary>
    /// Returns the DDS topic name of <paramref name="type"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The type has no <see cref="DdsTopicAttribute"/>.</exception>
    public static string Of(Type type)
    {
        if (type == null) throw new ArgumentNullException(nameof(type));

        var attr = type.GetCustomAttribute<DdsTopicAttribute>();
        if (attr == null)
            throw new InvalidOperationException($"Type {type.Name} is missing [DdsTopic] attribute. You must specify topicName manually.");

        if (!string.IsNullOrWhiteSpace(attr.TopicName))
            return attr.TopicName!;

        var typePath = new List<string>();
        for (var t = type; t != null; t = t.DeclaringType)
            typePath.Insert(0, t.Name);

        return Of(type.Namespace, typePath);
    }

    /// <summary>
    /// Builds the default (derived) topic name from a namespace and a type path
    /// (outermost containing type first, the topic type last).
    /// </summary>
    /// <param name="ns">The dotted namespace; <c>null</c> or empty for the global namespace.</param>
    /// <param name="typePath">The containing types followed by the type itself.</param>
    public static string Of(string? ns, IEnumerable<string> typePath)
    {
        if (typePath == null) throw new ArgumentNullException(nameof(typePath));

        var parts = new List<string>();
        if (!string.IsNullOrEmpty(ns))
            parts.AddRange(ns!.Split('.'));
        parts.AddRange(typePath);

        return string.Join("_", parts);
    }
}
