using System;
using System.Collections.Generic;
using System.Linq;
using CycloneDDS.Compiler.Common.IdlJson;

namespace CycloneDDS.IdlImporter;

/// <summary>
/// How one IDL member is written in C#: the type and the attributes that make the code generator
/// reproduce the member's IDL exactly (same wire format, same type identity).
/// </summary>
public sealed class MemberMapping
{
    /// <summary>C# type; for a fixed buffer, the element type.</summary>
    public string CsType { get; init; } = "";
    /// <summary>[DdsManaged]: strings and sequences.</summary>
    public bool IsManaged { get; init; }
    /// <summary>A fixed-size array, emitted as <c>fixed T name[n]</c> (the struct must be unsafe).</summary>
    public bool IsFixedBuffer { get; init; }
    /// <summary>Array dimensions (several: [ArrayLength(d1, d2)]).</summary>
    public IReadOnlyList<int> Dimensions { get; init; } = Array.Empty<int>();
    /// <summary>Sequence bound ([DdsBound]); 0 = unbounded.</summary>
    public int SequenceBound { get; init; }
    /// <summary>String bound ([MaxLength]); 0 = unbounded.</summary>
    public int StringBound { get; init; }
    /// <summary>[DdsChar]: the byte is an IDL char.</summary>
    public bool IsChar { get; init; }
    /// <summary>[DdsUInt8]: the byte is an IDL uint8 (not octet).</summary>
    public bool IsUInt8 { get; init; }

    public int ElementCount => Dimensions.Aggregate(1, (a, b) => a * b);
}

/// <summary>
/// An IDL construct the code generator cannot reproduce exactly. Importing it anyway would give C#
/// whose wire format or type identity differs from the IDL, so the import stops with this message.
/// </summary>
public sealed class UnsupportedIdlException : Exception
{
    public UnsupportedIdlException(string message) : base(message) { }
}

/// <summary>
/// Maps IDL types to C# types with appropriate attributes, from the IDL type spec the idlc JSON
/// plugin writes for each member ("IdlType", e.g. <c>sequence&lt;Mod::Point, 10&gt;</c>).
/// </summary>
public class TypeMapper
{
    private static readonly Dictionary<string, string> _primitiveMapping = new Dictionary<string, string>
    {
        { "boolean", "bool" },
        { "octet", "byte" },
        { "short", "short" },
        { "unsigned short", "ushort" },
        { "long", "int" },
        { "unsigned long", "uint" },
        { "long long", "long" },
        { "unsigned long long", "ulong" },
        { "float", "float" },
        { "double", "double" },
        { "string", "string" },
        { "int8", "sbyte" },
        { "uint8", "byte" },
        { "int16", "short" },
        { "uint16", "ushort" },
        { "int32", "int" },
        { "uint32", "uint" },
        { "int64", "long" },
        { "uint64", "ulong" },
        // idlc C names
        { "int32_t", "int" },
        { "uint32_t", "uint" },
        { "int16_t", "short" },
        { "uint16_t", "ushort" },
        { "int64_t", "long" },
        { "uint64_t", "ulong" },
        { "int8_t", "sbyte" },
        { "uint8_t", "byte" },
        { "bool", "bool" }
    };

    private sealed record Typedef(string IdlType, IReadOnlyList<int> Dimensions);

    private readonly Dictionary<string, Typedef> _typedefs = new();
    private readonly Dictionary<string, string> _kinds = new();

    public void RegisterType(JsonTypeDefinition type)
    {
        string scopedName = type.ScopedName ?? type.Name;
        bool isTypedef = type.Kind is "alias" or "typedef" || (type.Kind == "sequence" && type.Members.Count == 0);

        if (isTypedef && type.IdlType != null)
            _typedefs[scopedName] = new Typedef(type.IdlType, type.Dimensions ?? new List<int>());
        else
            _kinds[scopedName] = type.Kind;
    }

    /// <summary>
    /// Maps an IDL primitive type name to its C# equivalent.
    /// </summary>
    public string MapPrimitive(string idlType)
    {
        if (_primitiveMapping.TryGetValue(idlType, out var csType))
            return csType;
        throw new NotImplementedException($"Type mapping not yet implemented for: {idlType} (IDLIMP-003)");
    }

    /// <summary>Maps a struct or union member.</summary>
    /// <param name="member">The member, as the idlc JSON plugin describes it.</param>
    /// <param name="owner">Scoped name of the struct/union, for error messages.</param>
    public MemberMapping MapMember(JsonMember member, string owner = "")
    {
        string where = string.IsNullOrEmpty(owner) ? member.Name : $"{owner}.{member.Name}";
        if (member.IdlType == null)
            throw new InvalidOperationException(
                $"Member '{where}' has no IdlType: the idlc JSON plugin is older than this importer. Use the idlc shipped with it.");
        if (member.IsExternal)
            throw Unsupported(where, "@external");

        var dims = new List<int>(member.Dimensions ?? new List<int>());
        var spec = Resolve(IdlTypeSpec.Parse(member.IdlType), dims);

        if (dims.Count > 0)
            return MapArray(spec, dims, where, member.IsOptional);

        var mapping = MapSingle(spec, where);
        if (member.IsOptional && !mapping.IsManaged)
            mapping = With(mapping, csType: mapping.CsType + "?");
        return mapping;
    }

    /// <summary>The C# type of a union discriminator, from the union's IdlType.</summary>
    public MemberMapping MapDiscriminator(JsonTypeDefinition union)
    {
        if (union.IdlType == null)
            throw new InvalidOperationException(
                $"Union '{union.Name}' has no IdlType: the idlc JSON plugin is older than this importer. Use the idlc shipped with it.");
        var dims = new List<int>();
        return MapSingle(Resolve(IdlTypeSpec.Parse(union.IdlType), dims), union.Name + " discriminator");
    }

    /// <summary>The C# underlying type of an enum with this @bit_bound.</summary>
    public static string EnumUnderlyingType(JsonTypeDefinition type) => type.BitBound switch
    {
        null or 32 => "int",
        16 => "short",
        8 => "byte",
        var bits => throw new UnsupportedIdlException(
            $"Enum '{type.ScopedName ?? type.Name}' has @bit_bound({bits}); C# enums can only give 8, 16 or 32 " +
            "(byte, short, int). Change the IDL to one of those.")
    };

    /// <summary>Follows typedefs to the type they name, collecting array dimensions (a member's own come first).</summary>
    private IdlTypeSpec Resolve(IdlTypeSpec spec, List<int> dims)
    {
        for (int depth = 0; depth < 32 && spec.Kind == IdlTypeKind.Named; depth++)
        {
            if (!_typedefs.TryGetValue(spec.Name, out var typedef))
                return spec;
            dims.AddRange(typedef.Dimensions);
            spec = IdlTypeSpec.Parse(typedef.IdlType);
        }
        return spec;
    }

    private MemberMapping MapSingle(IdlTypeSpec spec, string where)
    {
        switch (spec.Kind)
        {
            case IdlTypeKind.String:
                return new MemberMapping { CsType = "string", IsManaged = true, StringBound = spec.Bound };
            case IdlTypeKind.Sequence:
                return MapSequence(spec, where);
            default:
                var (cs, isChar, isUInt8) = MapElement(spec, where, "a member");
                return new MemberMapping { CsType = cs, IsChar = isChar, IsUInt8 = isUInt8 };
        }
    }

    private MemberMapping MapSequence(IdlTypeSpec spec, string where)
    {
        var dims = new List<int>();
        var element = Resolve(spec.Element!, dims);
        if (dims.Count > 0)
            throw Unsupported(where, "a sequence of arrays");
        if (element.Kind == IdlTypeKind.Sequence)
            throw Unsupported(where, "a sequence of sequences",
                "wrap the inner sequence in a struct: struct Row { sequence<T> items; }; sequence<Row>");
        if (element.Kind == IdlTypeKind.String)
        {
            if (element.Bound > 0)
                throw Unsupported(where, $"a sequence of bounded strings (string<{element.Bound}>)",
                    "make the element an unbounded string (the sequence itself may stay bounded)");
            return new MemberMapping { CsType = "List<string>", IsManaged = true, SequenceBound = spec.Bound };
        }

        var (cs, isChar, isUInt8) = MapElement(element, where, "a sequence element");
        return new MemberMapping { CsType = $"List<{cs}>", IsManaged = true, SequenceBound = spec.Bound, IsChar = isChar, IsUInt8 = isUInt8 };
    }

    private MemberMapping MapArray(IdlTypeSpec element, List<int> dims, string where, bool optional)
    {
        if (optional)
            throw Unsupported(where, "an @optional array");
        if (element.Kind == IdlTypeKind.Primitive)
        {
            var (cs, isChar, isUInt8) = MapElement(element, where, "an array element");
            return new MemberMapping { CsType = cs, IsChar = isChar, IsUInt8 = isUInt8, IsFixedBuffer = true, Dimensions = dims };
        }

        string what = element.Kind switch
        {
            IdlTypeKind.String => "an array of strings",
            IdlTypeKind.Sequence => "an array of sequences",
            _ => _kinds.TryGetValue(element.Name, out var kind) ? $"an array of {kind}s ({element.Name})" : $"an array of {element.Name}",
        };
        throw Unsupported(where, what, "only arrays of primitive types are supported (as fixed buffers); use a sequence instead");
    }

    /// <summary>A primitive, enum, struct or union in C#.</summary>
    private (string CsType, bool IsChar, bool IsUInt8) MapElement(IdlTypeSpec spec, string where, string role)
    {
        if (spec.Kind == IdlTypeKind.Primitive)
        {
            switch (spec.Name)
            {
                case "char": return ("byte", true, false);
                case "uint8": return ("byte", false, true);
                case "wchar": throw Unsupported(where, "wchar");
                case "long double": throw Unsupported(where, "long double");
                case "any": throw Unsupported(where, "any");
            }
            return (MapPrimitive(spec.Name), false, false);
        }
        if (spec.Kind == IdlTypeKind.WString)
            throw Unsupported(where, "wstring");
        if (spec.Kind != IdlTypeKind.Named)
            throw Unsupported(where, $"{spec} as {role}");

        if (_kinds.TryGetValue(spec.Name, out var kind) && kind == "bitmask")
            throw Unsupported(where, $"the bitmask {spec.Name}", "use an integer of the bitmask's size with the bit values as constants");
        return (GetCSharpNamespace(spec.Name), false, false);
    }

    private static MemberMapping With(MemberMapping m, string csType) => new MemberMapping
    {
        CsType = csType, IsManaged = m.IsManaged, IsFixedBuffer = m.IsFixedBuffer, Dimensions = m.Dimensions,
        SequenceBound = m.SequenceBound, StringBound = m.StringBound, IsChar = m.IsChar, IsUInt8 = m.IsUInt8,
    };

    private static UnsupportedIdlException Unsupported(string where, string what, string? hint = null) =>
        new UnsupportedIdlException(
            $"'{where}' is {what}, which CycloneDDS.NET cannot represent with the same wire format and type identity" +
            (hint != null ? $": {hint}." : "."));

    /// <summary>
    /// Converts an IDL module path to a C# namespace.
    /// </summary>
    /// <param name="idlModulePath">IDL module path (e.g., "Module::SubModule")</param>
    /// <returns>C# namespace (e.g., "Module.SubModule")</returns>
    public string GetCSharpNamespace(string idlModulePath)
    {
        if (string.IsNullOrEmpty(idlModulePath)) return string.Empty;
        return idlModulePath.Replace("::", ".");
    }
}

public enum IdlTypeKind { Primitive, String, WString, Sequence, Named }

/// <summary>
/// An IDL type spec as the idlc JSON plugin prints it: a primitive keyword, <c>string</c>/<c>string&lt;N&gt;</c>,
/// <c>wstring</c>, <c>sequence&lt;T&gt;</c>/<c>sequence&lt;T, N&gt;</c>, or a scoped name.
/// </summary>
public sealed class IdlTypeSpec
{
    private static readonly HashSet<string> Primitives = new()
    {
        "boolean", "char", "wchar", "octet", "int8", "uint8", "int16", "uint16", "int32", "uint32",
        "int64", "uint64", "float", "double", "long double", "any",
    };

    public IdlTypeKind Kind { get; private init; }
    /// <summary>The keyword or scoped name.</summary>
    public string Name { get; private init; } = "";
    /// <summary>String or sequence bound; 0 = unbounded.</summary>
    public int Bound { get; private init; }
    public IdlTypeSpec? Element { get; private init; }

    public static IdlTypeSpec Parse(string text)
    {
        int pos = 0;
        var spec = ParseAt(text, ref pos);
        if (pos != text.Length)
            throw new FormatException($"Unexpected '{text.Substring(pos)}' in IDL type '{text}'.");
        return spec;
    }

    private static IdlTypeSpec ParseAt(string s, ref int pos)
    {
        SkipSpaces(s, ref pos);
        int start = pos;
        while (pos < s.Length && (char.IsLetterOrDigit(s[pos]) || s[pos] == '_' || s[pos] == ':' ||
                                  (s[pos] == ' ' && s.Substring(start, pos - start) == "long" && s.Substring(pos).StartsWith(" double"))))
            pos++;
        string word = s.Substring(start, pos - start);

        if (word == "sequence")
        {
            Expect(s, ref pos, '<');
            var element = ParseAt(s, ref pos);
            int bound = 0;
            SkipSpaces(s, ref pos);
            if (pos < s.Length && s[pos] == ',')
            {
                pos++;
                bound = ParseInt(s, ref pos);
            }
            Expect(s, ref pos, '>');
            return new IdlTypeSpec { Kind = IdlTypeKind.Sequence, Name = word, Element = element, Bound = bound };
        }
        if (word is "string" or "wstring")
        {
            int bound = 0;
            if (pos < s.Length && s[pos] == '<')
            {
                pos++;
                bound = ParseInt(s, ref pos);
                Expect(s, ref pos, '>');
            }
            return new IdlTypeSpec { Kind = word == "string" ? IdlTypeKind.String : IdlTypeKind.WString, Name = word, Bound = bound };
        }
        if (word.Length == 0)
            throw new FormatException($"Expected a type at position {start} in IDL type '{s}'.");
        return new IdlTypeSpec { Kind = Primitives.Contains(word) ? IdlTypeKind.Primitive : IdlTypeKind.Named, Name = word };
    }

    private static int ParseInt(string s, ref int pos)
    {
        SkipSpaces(s, ref pos);
        int start = pos;
        while (pos < s.Length && char.IsDigit(s[pos])) pos++;
        if (pos == start) throw new FormatException($"Expected a number at position {start} in IDL type '{s}'.");
        int value = int.Parse(s.Substring(start, pos - start));
        SkipSpaces(s, ref pos);
        return value;
    }

    private static void Expect(string s, ref int pos, char c)
    {
        SkipSpaces(s, ref pos);
        if (pos >= s.Length || s[pos] != c)
            throw new FormatException($"Expected '{c}' at position {pos} in IDL type '{s}'.");
        pos++;
    }

    private static void SkipSpaces(string s, ref int pos)
    {
        while (pos < s.Length && s[pos] == ' ') pos++;
    }

    public override string ToString() => Kind switch
    {
        IdlTypeKind.Sequence => Bound > 0 ? $"sequence<{Element}, {Bound}>" : $"sequence<{Element}>",
        IdlTypeKind.String or IdlTypeKind.WString => Bound > 0 ? $"{Name}<{Bound}>" : Name,
        _ => Name,
    };
}
