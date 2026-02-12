
[IMPROVEMENT] Solution clean (or rebuild) should delete the generated csharp and idl files (now of obj/Generated)

[IMPROVEMENT] There should be just one generated cs file per dds struct, combining view, serializers etc.

[BUG] The code generator expects PascalCase names to match the property references in the generated code.
Generator should NOT expect any case. It should use exactly the same case as in the source files!



