using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CycloneDDS.Runtime;
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

        // ME1-T03: resolve topic name – use explicit name when provided, else derive from full type name
        TopicName = string.IsNullOrWhiteSpace(topicAttribute.TopicName)
            ? (topicType.FullName?.Replace('.', '_') ?? topicType.Name)
            : topicAttribute.TopicName;
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

        // ── ME1-T08: detect [DdsUnion] so arm fields get union metadata ──────────
        var isUnionType = currentType.GetCustomAttribute<DdsUnionAttribute>() != null;
        string? unionDiscriminatorPath = null;
        if (isUnionType)
        {
            // Pre-scan to find the discriminator field structured name.
            foreach (var m in GetPublicInstanceMembers(currentType))
            {
                if (m.GetCustomAttribute<DdsDiscriminatorAttribute>() != null)
                {
                    unionDiscriminatorPath = string.IsNullOrEmpty(prefix)
                        ? m.Name
                        : $"{prefix}.{m.Name}";
                    break;
                }
            }
        }

        foreach (var member in GetPublicInstanceMembers(currentType))
        {
            var memberType = GetMemberType(member);
            var structuredName = string.IsNullOrEmpty(prefix)
                ? member.Name
                : $"{prefix}.{member.Name}";

            var nextPath = new List<MemberInfo>(memberPath) { member };

            // ── ME1-T08 / ME1-C02: collect union arm / discriminator membership ─────
            // Must be determined BEFORE the InlineArray / FixedBuffer early-exits so that
            // inline-array union arms also receive the proper discriminator metadata.
            bool isDiscriminatorField = false;
            bool isUnionArm = false;
            object? activeWhenDiscriminatorValue = null;
            bool isDefaultUnionCase = false;
            string? dependentDiscriminatorPath = null;

            if (isUnionType)
            {
                isDiscriminatorField = member.GetCustomAttribute<DdsDiscriminatorAttribute>() != null;
                if (!isDiscriminatorField)
                {
                    var caseAttr = member.GetCustomAttribute<DdsCaseAttribute>();
                    var defaultCaseAttr = member.GetCustomAttribute<DdsDefaultCaseAttribute>();
                    if (caseAttr != null)
                    {
                        isUnionArm = true;
                        activeWhenDiscriminatorValue = caseAttr.Value;
                        dependentDiscriminatorPath = unionDiscriminatorPath;
                    }
                    else if (defaultCaseAttr != null)
                    {
                        isUnionArm = true;
                        isDefaultUnionCase = true;
                        dependentDiscriminatorPath = unionDiscriminatorPath;
                    }
                }
            }

            // ── Fixed-size C# buffer (public unsafe fixed T Name[N]) ─────────
            // Must be detected BEFORE IsFlattenable, because the compiler-generated
            // FixedBuffer nested struct would otherwise be incorrectly flattened.
            if (member is FieldInfo fieldInfo)
            {
                var fixedAttr = fieldInfo.GetCustomAttribute<FixedBufferAttribute>();
                if (fixedAttr != null)
                {
                    AppendFixedBufferField(
                        fieldInfo, fixedAttr, structuredName, memberPath, allFields);
                    continue;
                }

                // ME1-T02 / ME1-C02: [InlineArray(N)] struct field – treat as a fixed-size array.
                // Must be detected before IsFlattenable so the InlineArray struct is not
                // flattened as though it were a nested DDS struct.
                // Union metadata is passed through so inline-array union arms inherit
                // discriminator context (D05 fix).
                var inlineAttr = memberType.GetCustomAttribute<System.Runtime.CompilerServices.InlineArrayAttribute>();
                if (inlineAttr != null)
                {
                    AppendInlineArrayField(
                        fieldInfo, inlineAttr, structuredName, memberPath, allFields,
                        dependentDiscriminatorPath: dependentDiscriminatorPath,
                        activeWhenDiscriminatorValue: activeWhenDiscriminatorValue,
                        isDefaultUnionCase: isDefaultUnionCase,
                        isDiscriminatorField: isDiscriminatorField);
                    continue;
                }
            }

            // Union arm members (even if they happen to be flattenable structs) are
            // kept as atomic FieldMetadata so the union visibility logic works correctly.
            if (!isDiscriminatorField && !isUnionArm && IsFlattenable(memberType))
            {
                AppendFields(topicType, memberType, nextPath, structuredName, allFields, keyFields, visited);
                continue;
            }

            var getter = MemberAccessorFactory.CreateGetter(nextPath);
            var setter = MemberAccessorFactory.CreateSetter(nextPath);

            // ── Determine array metadata ──────────────────────────────────────
            bool isArrayField = false;
            Type? elementType = null;

            if (memberType.IsArray)
            {
                isArrayField = true;
                elementType = memberType.GetElementType();
            }
            else if (memberType.IsGenericType &&
                     memberType.GetGenericTypeDefinition() == typeof(List<>))
            {
                isArrayField = true;
                elementType = memberType.GetGenericArguments()[0];
            }

            var fieldMetadata = new FieldMetadata(
                structuredName,
                structuredName,
                memberType,
                getter,
                setter,
                isSynthetic: false,
                isWrapperField: false,
                isArrayField: isArrayField,
                isFixedSizeArray: false,
                elementType: elementType,
                fixedArrayLength: -1,
                dependentDiscriminatorPath: dependentDiscriminatorPath,
                activeWhenDiscriminatorValue: activeWhenDiscriminatorValue,
                isDefaultUnionCase: isDefaultUnionCase,
                isDiscriminatorField: isDiscriminatorField);

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

        // FixedStringN types from CycloneDDS.Schema are string-like leaf values.
        if (IsFixedStringType(type))
        {
            return true;
        }

        if (type.IsArray)
        {
            return true;
        }

        // Generic List<T> (DDS sequence) – treated as a single leaf field
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            return true;
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Returns true when <paramref name="type"/> is one of the CycloneDDS FixedStringN types
    /// (FixedString32, FixedString64, FixedString128, FixedString256).
    /// These are treated as string-like scalar leaf values rather than expanded structs.
    /// </summary>
    public static bool IsFixedStringType(Type type)
    {
        return type == typeof(FixedString32) ||
               type == typeof(FixedString64) ||
               type == typeof(FixedString128) ||
               type == typeof(FixedString256);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Fixed-size buffer support
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a single <see cref="FieldMetadata"/> that exposes a C# fixed-size buffer
    /// field as a <c>T[]</c> value (snapshot copy on read, element-by-element copy on write).
    /// </summary>
    private static void AppendFixedBufferField(
        FieldInfo bufField,
        FixedBufferAttribute fixedAttr,
        string structuredName,
        IReadOnlyList<MemberInfo> parentPath,
        ICollection<FieldMetadata> allFields)
    {
        var elementType = fixedAttr.ElementType;
        var length = fixedAttr.Length;
        var arrayType = elementType.MakeArrayType(); // T[]

        var getter = CreateFixedBufferGetter(parentPath, bufField, elementType, length);
        var setter = CreateFixedBufferSetter(parentPath, bufField, elementType, length);

        var meta = new FieldMetadata(
            structuredName,
            structuredName,
            arrayType,
            getter,
            setter,
            isSynthetic: false,
            isWrapperField: false,
            isArrayField: false,
            isFixedSizeArray: true,
            elementType: elementType,
            fixedArrayLength: length);

        allFields.Add(meta);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ME1-T02: [InlineArray] support
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a single <see cref="FieldMetadata"/> for a field whose type is decorated
    /// with <c>[System.Runtime.CompilerServices.InlineArray(N)]</c>.
    /// The value exposed is a <c>T[]</c> snapshot (read via pinned memory, written back element-by-element).
    /// Union metadata parameters are forwarded from the outer union-arm detection so that
    /// inline-array union arms correctly inherit discriminator context (ME1-C02 / D05 fix).
    /// </summary>
    private static void AppendInlineArrayField(
        FieldInfo inlineField,
        System.Runtime.CompilerServices.InlineArrayAttribute inlineAttr,
        string structuredName,
        IReadOnlyList<MemberInfo> parentPath,
        ICollection<FieldMetadata> allFields,
        string? dependentDiscriminatorPath = null,
        object? activeWhenDiscriminatorValue = null,
        bool isDefaultUnionCase = false,
        bool isDiscriminatorField = false)
    {
        var inlineType = inlineField.FieldType;
        var length = inlineAttr.Length;

        // The element type is the type of the single user-defined field inside the InlineArray struct.
        var elemFieldInfo = inlineType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(f => !f.IsStatic);

        if (elemFieldInfo == null || length <= 0)
        {
            return; // Malformed InlineArray; skip.
        }

        var elementType = elemFieldInfo.FieldType;
        var arrayType = elementType.MakeArrayType(); // T[]

        var getter = CreateInlineArrayGetter(parentPath, inlineField, elementType, length);
        var setter = CreateInlineArraySetter(parentPath, inlineField, elementType, length);

        var meta = new FieldMetadata(
            structuredName,
            structuredName,
            arrayType,
            getter,
            setter,
            isSynthetic: false,
            isWrapperField: false,
            isArrayField: false,
            isFixedSizeArray: true,
            elementType: elementType,
            fixedArrayLength: length,
            dependentDiscriminatorPath: dependentDiscriminatorPath,
            activeWhenDiscriminatorValue: activeWhenDiscriminatorValue,
            isDefaultUnionCase: isDefaultUnionCase,
            isDiscriminatorField: isDiscriminatorField);

        allFields.Add(meta);
    }

    private static Func<object, object?> CreateInlineArrayGetter(
        IReadOnlyList<MemberInfo> parentPath,
        FieldInfo inlineField,
        Type elementType,
        int length)
    {
        var parentAccessors = parentPath.Select(m => new MemberAccessor(m)).ToArray();
        int elemSize = Marshal.SizeOf(elementType);

        return target =>
        {
            // Navigate to parent struct.
            object boxedParent = target;
            foreach (var acc in parentAccessors)
            {
                var next = acc.Getter(boxedParent);
                if (next == null) return Array.CreateInstance(elementType, length);
                boxedParent = next;
            }

            // Get the boxed InlineArray struct value.
            var structValue = inlineField.GetValue(boxedParent);
            if (structValue == null) return Array.CreateInstance(elementType, length);

            var result = Array.CreateInstance(elementType, length);
            var handle = GCHandle.Alloc(structValue, GCHandleType.Pinned);
            try
            {
                var ptr = handle.AddrOfPinnedObject();
                for (int i = 0; i < length; i++)
                    result.SetValue(ReadMarshalElement(ptr + (i * elemSize), elementType), i);
            }
            finally
            {
                handle.Free();
            }
            return result;
        };
    }

    private static Action<object, object?> CreateInlineArraySetter(
        IReadOnlyList<MemberInfo> parentPath,
        FieldInfo inlineField,
        Type elementType,
        int length)
    {
        var parentAccessors = parentPath.Select(m => new MemberAccessor(m)).ToArray();
        int elemSize = Marshal.SizeOf(elementType);

        return (target, value) =>
        {
            if (value is not Array srcArray) return;

            // Navigate to parent, tracking the chain for back-propagation.
            var parentStack = new List<(object Obj, MemberAccessor Acc)>(parentAccessors.Length);
            object boxedParent = target;
            for (int pi = 0; pi < parentAccessors.Length; pi++)
            {
                var acc = parentAccessors[pi];
                parentStack.Add((boxedParent, acc));
                var next = acc.Getter(boxedParent);
                if (next == null) return;
                boxedParent = next;
            }

            // Get a boxed copy of the InlineArray struct, modify its memory, then write it back.
            var structValue = inlineField.GetValue(boxedParent);
            if (structValue == null) return;

            var handle = GCHandle.Alloc(structValue, GCHandleType.Pinned);
            try
            {
                var ptr = handle.AddrOfPinnedObject();
                int count = Math.Min(srcArray.Length, length);
                for (int i = 0; i < count; i++)
                    WriteMarshalElement(ptr + (i * elemSize), srcArray.GetValue(i), elementType);
            }
            finally
            {
                handle.Free();
            }

            // Write the modified InlineArray struct back into the parent field.
            inlineField.SetValue(boxedParent, structValue);

            // Back-propagate for top-level value-type fields.
            object updated = boxedParent;
            for (int pi = parentStack.Count - 1; pi >= 0; pi--)
            {
                var (parent, acc) = parentStack[pi];
                acc.Setter(parent, updated);
                updated = parent;
            }
        };
    }

    private static Func<object, object?> CreateFixedBufferGetter(
        IReadOnlyList<MemberInfo> parentPath,
        FieldInfo bufField,
        Type elementType,
        int length)
    {
        var parentAccessors = parentPath.Select(m => new MemberAccessor(m)).ToArray();
        int elemSize = Marshal.SizeOf(elementType);

        return target =>
        {
            // Navigate to the struct that directly contains the fixed buffer field.
            object boxedParent = target;
            foreach (var acc in parentAccessors)
            {
                var next = acc.Getter(boxedParent);
                if (next == null)
                {
                    return Array.CreateInstance(elementType, length);
                }

                boxedParent = next;
            }

            // GetValue returns a boxed copy of the compiler-generated FixedBuffer struct.
            var bufInstance = bufField.GetValue(boxedParent);
            if (bufInstance == null)
            {
                return Array.CreateInstance(elementType, length);
            }

            var result = Array.CreateInstance(elementType, length);
            var handle = GCHandle.Alloc(bufInstance, GCHandleType.Pinned);
            try
            {
                var ptr = handle.AddrOfPinnedObject();
                for (int i = 0; i < length; i++)
                {
                    result.SetValue(ReadMarshalElement(ptr + (i * elemSize), elementType), i);
                }
            }
            finally
            {
                handle.Free();
            }

            return result;
        };
    }

    private static Action<object, object?> CreateFixedBufferSetter(
        IReadOnlyList<MemberInfo> parentPath,
        FieldInfo bufField,
        Type elementType,
        int length)
    {
        var parentAccessors = parentPath.Select(m => new MemberAccessor(m)).ToArray();
        int elemSize = Marshal.SizeOf(elementType);

        return (target, value) =>
        {
            if (value is not Array srcArray)
            {
                return;
            }

            // Navigate to parent, tracking the chain for back-propagation.
            var parentStack = new List<(object Obj, MemberAccessor Acc)>(parentAccessors.Length);
            object boxedParent = target;

            for (int pi = 0; pi < parentAccessors.Length; pi++)
            {
                var acc = parentAccessors[pi];
                parentStack.Add((boxedParent, acc));
                var next = acc.Getter(boxedParent);
                if (next == null)
                {
                    return;
                }

                boxedParent = next;
            }

            // Get a boxed copy of the FixedBuffer struct, modify it, then write it back.
            var bufInstance = bufField.GetValue(boxedParent);
            if (bufInstance == null)
            {
                return;
            }

            var handle = GCHandle.Alloc(bufInstance, GCHandleType.Pinned);
            try
            {
                var ptr = handle.AddrOfPinnedObject();
                int count = Math.Min(srcArray.Length, length);
                for (int i = 0; i < count; i++)
                {
                    WriteMarshalElement(ptr + (i * elemSize), srcArray.GetValue(i), elementType);
                }
            }
            finally
            {
                handle.Free();
            }

            // Write the modified FixedBuffer struct back into the parent struct.
            bufField.SetValue(boxedParent, bufInstance);

            // Back-propagate for nested value-type fields so inner changes reach the root.
            object updated = boxedParent;
            for (int pi = parentStack.Count - 1; pi >= 0; pi--)
            {
                var (parent, acc) = parentStack[pi];
                acc.Setter(parent, updated);
                updated = parent;
            }
        };
    }

    /// <summary>Reads a primitive value of <paramref name="elementType"/> from unmanaged memory.</summary>
    public static object ReadMarshalElement(IntPtr ptr, Type elementType)
    {
        if (elementType == typeof(byte))   return Marshal.ReadByte(ptr);
        if (elementType == typeof(sbyte))  return (sbyte)Marshal.ReadByte(ptr);
        if (elementType == typeof(short))  return (short)Marshal.ReadInt16(ptr);
        if (elementType == typeof(ushort)) return (ushort)Marshal.ReadInt16(ptr);
        if (elementType == typeof(int))    return Marshal.ReadInt32(ptr);
        if (elementType == typeof(uint))   return (uint)Marshal.ReadInt32(ptr);
        if (elementType == typeof(long))   return Marshal.ReadInt64(ptr);
        if (elementType == typeof(ulong))  return (ulong)Marshal.ReadInt64(ptr);
        if (elementType == typeof(float))  return BitConverter.Int32BitsToSingle(Marshal.ReadInt32(ptr));
        if (elementType == typeof(double)) return BitConverter.Int64BitsToDouble(Marshal.ReadInt64(ptr));
        if (elementType == typeof(bool))   return Marshal.ReadByte(ptr) != 0;
        if (elementType == typeof(char))   return (char)Marshal.ReadInt16(ptr);

        throw new NotSupportedException($"Fixed-buffer element type '{elementType.Name}' is not supported.");
    }

    /// <summary>Writes a primitive <paramref name="value"/> of <paramref name="elementType"/> to unmanaged memory.</summary>
    public static void WriteMarshalElement(IntPtr ptr, object? value, Type elementType)
    {
        if (value == null) return;

        if (elementType == typeof(byte))   { Marshal.WriteByte(ptr, (byte)value);                                        return; }
        if (elementType == typeof(sbyte))  { Marshal.WriteByte(ptr, (byte)(sbyte)value);                                 return; }
        if (elementType == typeof(short))  { Marshal.WriteInt16(ptr, (short)value);                                      return; }
        if (elementType == typeof(ushort)) { Marshal.WriteInt16(ptr, (short)(ushort)value);                              return; }
        if (elementType == typeof(int))    { Marshal.WriteInt32(ptr, (int)value);                                        return; }
        if (elementType == typeof(uint))   { Marshal.WriteInt32(ptr, (int)(uint)value);                                  return; }
        if (elementType == typeof(long))   { Marshal.WriteInt64(ptr, (long)value);                                       return; }
        if (elementType == typeof(ulong))  { Marshal.WriteInt64(ptr, (long)(ulong)value);                                return; }
        if (elementType == typeof(float))  { Marshal.WriteInt32(ptr, BitConverter.SingleToInt32Bits((float)value));      return; }
        if (elementType == typeof(double)) { Marshal.WriteInt64(ptr, BitConverter.DoubleToInt64Bits((double)value));     return; }
        if (elementType == typeof(bool))   { Marshal.WriteByte(ptr, (bool)value ? (byte)1 : (byte)0);                   return; }
        if (elementType == typeof(char))   { Marshal.WriteInt16(ptr, (short)(char)value);                                return; }

        throw new NotSupportedException($"Fixed-buffer element type '{elementType.Name}' is not supported.");
    }

    private static void AppendSyntheticFields(ICollection<FieldMetadata> allFields)    {
        var delayGetter = new Func<object, object?>(input =>
        {
            var sample = (SampleData)input;
            var sourceTimestamp = new DateTime(sample.SampleInfo.SourceTimestamp, DateTimeKind.Utc);
            return (sample.Timestamp - sourceTimestamp).TotalMilliseconds;
        });

        var sizeGetter = new Func<object, object?>(input => ((SampleData)input).SizeBytes);
        var timestampGetter = new Func<object, object?>(input => ((SampleData)input).Timestamp);
        var ordinalGetter = new Func<object, object?>(input => ((SampleData)input).Ordinal);

        var topicGetter = new Func<object, object?>(input => ((SampleData)input).TopicMetadata.ShortName);
        var instanceStateGetter = new Func<object, object?>(input => (object)((SampleData)input).SampleInfo.InstanceState);

        // Wrapper fields: top-level SampleData properties exposed as filterable fields.
        allFields.Add(new FieldMetadata("Timestamp", "Timestamp", typeof(DateTime), timestampGetter, SyntheticSetter, isSynthetic: true, isWrapperField: true));
        allFields.Add(new FieldMetadata("Ordinal", "Ordinal", typeof(long), ordinalGetter, SyntheticSetter, isSynthetic: true, isWrapperField: true));
        allFields.Add(new FieldMetadata("Topic", "Topic", typeof(string), topicGetter, SyntheticSetter, isSynthetic: true, isWrapperField: true));
        allFields.Add(new FieldMetadata("InstanceState", "InstanceState", typeof(DdsInstanceState), instanceStateGetter, SyntheticSetter, isSynthetic: true, isWrapperField: true));

        // Display-only synthetic fields (not filterable via the standard field picker).
        allFields.Add(new FieldMetadata(DelayFieldName, DelayFieldName, typeof(double), delayGetter, SyntheticSetter, isSynthetic: true));
        allFields.Add(new FieldMetadata(SizeFieldName, SizeFieldName, typeof(int), sizeGetter, SyntheticSetter, isSynthetic: true));
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
