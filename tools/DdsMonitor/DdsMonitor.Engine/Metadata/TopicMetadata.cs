using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using CycloneDDS.Schema;

namespace DdsMonitor.Engine;

/// <summary>
/// Metadata for a DDS topic type, including flattened fields and accessors.
/// </summary>
public sealed class TopicMetadata
{
    private const string DelayFieldName = "Delay [ms]";
    private const string SizeFieldName = "Size [B]";

    private static readonly Action<object, object?> SyntheticSetter = (_, __) =>
        throw new InvalidOperationException("Synthetic fields are read-only.");

    /// <summary>
    /// Initializes a new instance of the <see cref="TopicMetadata"/> class for the given topic type.
    /// </summary>
    public TopicMetadata(Type topicType)
    {
        TopicType = topicType ?? throw new ArgumentNullException(nameof(topicType));

        var topicAttribute = topicType.GetCustomAttribute<DdsTopicAttribute>();
        if (topicAttribute == null)
        {
            throw new InvalidOperationException(
                $"Type '{topicType.Name}' is missing [{nameof(DdsTopicAttribute)}] attribute.");
        }

        TopicName = topicAttribute.TopicName;
        ShortName = topicType.Name;
        Namespace = topicType.Namespace ?? string.Empty;

        var allFields = new List<FieldMetadata>();
        var keyFields = new List<FieldMetadata>();
        var visited = new HashSet<Type>();

        AppendFields(topicType, topicType, new List<MemberInfo>(), string.Empty, allFields, keyFields, visited);
        AppendSyntheticFields(allFields);

        AllFields = allFields;
        KeyFields = keyFields;
        IsKeyed = keyFields.Count > 0;
    }

    /// <summary>
    /// Gets the CLR topic type.
    /// </summary>
    public Type TopicType { get; }

    /// <summary>
    /// Gets the DDS topic name from the <see cref="DdsTopicAttribute"/>.
    /// </summary>
    public string TopicName { get; }

    /// <summary>
    /// Gets the short topic name (type name without namespace).
    /// </summary>
    public string ShortName { get; }

    /// <summary>
    /// Gets the namespace of the topic type.
    /// </summary>
    public string Namespace { get; }

    /// <summary>
    /// Gets a value indicating whether the topic is keyed.
    /// </summary>
    public bool IsKeyed { get; }

    /// <summary>
    /// Gets all fields, including synthetic fields appended at the end.
    /// </summary>
    public IReadOnlyList<FieldMetadata> AllFields { get; }

    /// <summary>
    /// Gets the subset of key fields.
    /// </summary>
    public IReadOnlyList<FieldMetadata> KeyFields { get; }

    private static void AppendFields(
        Type topicType,
        Type currentType,
        List<MemberInfo> memberPath,
        string prefix,
        ICollection<FieldMetadata> allFields,
        ICollection<FieldMetadata> keyFields,
        ISet<Type> visited)
    {
        if (!visited.Add(currentType))
        {
            return;
        }

        foreach (var member in GetPublicInstanceMembers(currentType))
        {
            var memberType = GetMemberType(member);
            var structuredName = string.IsNullOrEmpty(prefix)
                ? member.Name
                : $"{prefix}.{member.Name}";

            var nextPath = new List<MemberInfo>(memberPath) { member };

            if (IsFlattenable(memberType))
            {
                AppendFields(topicType, memberType, nextPath, structuredName, allFields, keyFields, visited);
                continue;
            }

            var getter = MemberAccessorFactory.CreateGetter(nextPath);
            var setter = MemberAccessorFactory.CreateSetter(nextPath);

            var fieldMetadata = new FieldMetadata(structuredName, structuredName, memberType, getter, setter, false);
            allFields.Add(fieldMetadata);

            if (member.GetCustomAttribute<DdsKeyAttribute>() != null)
            {
                keyFields.Add(fieldMetadata);
            }
        }

        visited.Remove(currentType);
    }

    private static IEnumerable<MemberInfo> GetPublicInstanceMembers(Type type)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;

        foreach (var field in type.GetFields(flags))
        {
            if (!field.IsStatic)
            {
                yield return field;
            }
        }

        foreach (var property in type.GetProperties(flags))
        {
            if (property.GetMethod != null && property.GetIndexParameters().Length == 0)
            {
                yield return property;
            }
        }
    }

    private static Type GetMemberType(MemberInfo member)
    {
        return member switch
        {
            FieldInfo field => field.FieldType,
            PropertyInfo property => property.PropertyType,
            _ => throw new InvalidOperationException($"Unsupported member type: {member.MemberType}.")
        };
    }

    private static bool IsFlattenable(Type type)
    {
        if (IsLeafType(type))
        {
            return false;
        }

        if (!type.IsValueType)
        {
            return false;
        }

        return HasPublicMembers(type);
    }

    private static bool HasPublicMembers(Type type)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
        return type.GetFields(flags).Length > 0 ||
               type.GetProperties(flags).Any(property => property.GetMethod != null && property.GetIndexParameters().Length == 0);
    }

    private static bool IsLeafType(Type type)
    {
        if (type.IsPrimitive || type.IsEnum)
        {
            return true;
        }

        if (type == typeof(string) ||
            type == typeof(decimal) ||
            type == typeof(DateTime) ||
            type == typeof(DateTimeOffset) ||
            type == typeof(TimeSpan) ||
            type == typeof(Guid))
        {
            return true;
        }

        if (type.IsArray)
        {
            return true;
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            return true;
        }

        return false;
    }

    private static void AppendSyntheticFields(ICollection<FieldMetadata> allFields)
    {
        var delayGetter = new Func<object, object?>(input =>
        {
            var sample = (SampleData)input;
            var sourceTimestamp = new DateTime(sample.SampleInfo.SourceTimestamp, DateTimeKind.Utc);
            return (sample.Timestamp - sourceTimestamp).TotalMilliseconds;
        });

        var sizeGetter = new Func<object, object?>(input => ((SampleData)input).SizeBytes);

        allFields.Add(new FieldMetadata(DelayFieldName, DelayFieldName, typeof(double), delayGetter, SyntheticSetter, true));
        allFields.Add(new FieldMetadata(SizeFieldName, SizeFieldName, typeof(int), sizeGetter, SyntheticSetter, true));
    }

    private sealed class MemberAccessor
    {
        public MemberAccessor(MemberInfo member)
        {
            Member = member;
            MemberType = GetMemberType(member);
            Getter = CreateGetter(member);
            Setter = CreateSetter(member);
        }

        public MemberInfo Member { get; }

        public Type MemberType { get; }

        public Func<object, object?> Getter { get; }

        public Action<object, object?> Setter { get; }

        private static Func<object, object?> CreateGetter(MemberInfo member)
        {
            var declaringType = member.DeclaringType ?? throw new InvalidOperationException("Member has no declaring type.");
            var targetParameter = Expression.Parameter(typeof(object), "target");
            var typedTarget = Expression.Convert(targetParameter, declaringType);

            Expression access = member switch
            {
                FieldInfo field => Expression.Field(typedTarget, field),
                PropertyInfo property => Expression.Property(typedTarget, property),
                _ => throw new InvalidOperationException($"Unsupported member type: {member.MemberType}.")
            };

            var box = Expression.Convert(access, typeof(object));
            return Expression.Lambda<Func<object, object?>>(box, targetParameter).Compile();
        }

        private static Action<object, object?> CreateSetter(MemberInfo member)
        {
            return member switch
            {
                FieldInfo field => CreateFieldSetter(field),
                PropertyInfo property => CreatePropertySetter(property),
                _ => throw new InvalidOperationException($"Unsupported member type: {member.MemberType}.")
            };
        }

        private static Action<object, object?> CreateFieldSetter(FieldInfo field)
        {
            if (field.IsInitOnly)
            {
                return (_, __) => throw new InvalidOperationException($"Field '{field.Name}' is readonly.");
            }

            if (field.DeclaringType?.IsValueType == true)
            {
                return (target, value) => field.SetValue(target, value);
            }

            var targetParameter = Expression.Parameter(typeof(object), "target");
            var valueParameter = Expression.Parameter(typeof(object), "value");
            var typedTarget = Expression.Convert(targetParameter, field.DeclaringType!);
            var typedValue = Expression.Convert(valueParameter, field.FieldType);
            var assign = Expression.Assign(Expression.Field(typedTarget, field), typedValue);
            return Expression.Lambda<Action<object, object?>>(assign, targetParameter, valueParameter).Compile();
        }

        private static Action<object, object?> CreatePropertySetter(PropertyInfo property)
        {
            if (property.SetMethod == null)
            {
                return (_, __) => throw new InvalidOperationException($"Property '{property.Name}' does not have a setter.");
            }

            if (property.DeclaringType?.IsValueType == true)
            {
                return (target, value) => property.SetValue(target, value);
            }

            var targetParameter = Expression.Parameter(typeof(object), "target");
            var valueParameter = Expression.Parameter(typeof(object), "value");
            var typedTarget = Expression.Convert(targetParameter, property.DeclaringType!);
            var typedValue = Expression.Convert(valueParameter, property.PropertyType);
            var assign = Expression.Assign(Expression.Property(typedTarget, property), typedValue);
            return Expression.Lambda<Action<object, object?>>(assign, targetParameter, valueParameter).Compile();
        }
    }

    private static class MemberAccessorFactory
    {
        public static Func<object, object?> CreateGetter(IReadOnlyList<MemberInfo> memberPath)
        {
            var accessors = memberPath.Select(member => new MemberAccessor(member)).ToArray();
            return target =>
            {
                if (target == null)
                {
                    throw new ArgumentNullException(nameof(target));
                }

                object? current = target;
                foreach (var accessor in accessors)
                {
                    current = accessor.Getter(current!);
                    if (current == null)
                    {
                        return null;
                    }
                }

                return current;
            };
        }

        public static Action<object, object?> CreateSetter(IReadOnlyList<MemberInfo> memberPath)
        {
            var accessors = memberPath.Select(member => new MemberAccessor(member)).ToArray();

            return (target, value) =>
            {
                if (target == null)
                {
                    throw new ArgumentNullException(nameof(target));
                }

                var parentStack = new List<(object Parent, MemberAccessor Accessor)>();
                object current = target;

                for (var index = 0; index < accessors.Length - 1; index++)
                {
                    var accessor = accessors[index];
                    parentStack.Add((current, accessor));
                    var next = accessor.Getter(current);
                    if (next == null)
                    {
                        throw new InvalidOperationException("Encountered null while traversing field path.");
                    }

                    current = next;
                }

                accessors[^1].Setter(current, value);

                object updated = current;
                for (var index = parentStack.Count - 1; index >= 0; index--)
                {
                    var (parent, accessor) = parentStack[index];
                    accessor.Setter(parent, updated);
                    updated = parent;
                }
            };
        }
    }
}
