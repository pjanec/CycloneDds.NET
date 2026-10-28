using System;
using System.Collections.Generic;
using System.Linq;

namespace CycloneDDS.CodeGen
{
    /// <summary>
    /// Writes a member name into generated C#: a C# keyword (an IDL member named <c>base</c>, <c>lock</c>,
    /// <c>params</c>...) needs the verbatim prefix, <c>@base</c>. Use it only where the name stands alone
    /// as a token; a name glued into a longer identifier (<c>Get{name}</c>) needs no escaping.
    /// </summary>
    public static class CSharpIdentifier
    {
        private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const",
            "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern",
            "false", "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface",
            "internal", "is", "lock", "long", "namespace", "new", "null", "object", "operator", "out", "override",
            "params", "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
            "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true", "try", "typeof",
            "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void", "volatile", "while"
        };

        public static string Escape(string name) =>
            string.IsNullOrEmpty(name) || name[0] == '@' || !Keywords.Contains(name) ? name : "@" + name;
    }

    /// <summary>
    /// Writes a name into generated IDL: an identifier that collides with an IDL keyword - compared
    /// case-insensitively, so a member <c>Fixed</c> or a module <c>Module</c> collides too - takes IDL's
    /// escape, a leading underscore. idlc strips it again, so the name in the type is unchanged.
    /// </summary>
    public static class IdlIdentifier
    {
        private static readonly HashSet<string> Keywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "abstract", "any", "alias", "attribute", "bitfield", "bitmask", "bitset", "boolean", "case", "char",
            "component", "connector", "const", "consumes", "context", "custom", "default", "double", "exception",
            "emits", "enum", "eventtype", "factory", "false", "finder", "fixed", "float", "getraises", "getter",
            "home", "import", "in", "inout", "interface", "local", "long", "manages", "map", "mirrorport", "module",
            "multiple", "native", "object", "octet", "oneway", "out", "primarykey", "private", "port", "porttype",
            "provides", "public", "publishes", "raises", "readonly", "setraises", "setter", "sequence", "short",
            "string", "struct", "supports", "switch", "true", "truncatable", "typedef", "typeid", "typename",
            "typeprefix", "unsigned", "union", "uses", "valuebase", "valuetype", "void", "wchar", "wstring",
            "int8", "uint8", "int16", "int32", "int64", "uint16", "uint32", "uint64"
        };

        public static string Escape(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            if (name[0] == '@') name = name.Substring(1);
            return Keywords.Contains(name) ? "_" + name : name;
        }

        /// <summary>Escapes each segment of a scoped name (<c>A::Module::T</c> -> <c>A::_Module::T</c>).</summary>
        public static string EscapeScoped(string scoped) =>
            string.Join("::", scoped.Split(new[] { "::" }, StringSplitOptions.None).Select(Escape));
    }
}
