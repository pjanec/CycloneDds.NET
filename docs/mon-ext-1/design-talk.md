---
in this design talk, we are going to detail the changes in the following TODO list:

[TODO] typed enums
Add support for defining the bit size of the enum data type
In csharp DSL read the type specification

    public enum ESomething : byte
    
and convert it to @bit_bound annotation in IDL..

    @bit_bound(8)
    enum ESomething {
      ...
    };


When you use the @bit_bound(8) annotation with an enum in Cyclone DDS, the generated plain-C code typically maps the field to a uint8_t.

Mapping Behavior
 - In standard IDL-to-C mapping, an enum is often represented by a standard C enum type, which compilers usually default to 32 bits (int). However, Cyclone's IDL compiler (idlc) uses the @bit_bound annotation to optimize the storage and serialization size:
    - uint8_t: Used when @bit_bound is between 1 and 8 bits.
    - uint16_t: Used when @bit_bound is between 9 and 16 bits.
    - uint32_t: The default size if the annotation is omitted or set between 17 and 32 bits.


[TODO]
Add support for inline arrays specified using [InlineArray] attribute. Now we support unmanaged fixed size arrays.

[TODO] DdsMonitor should render just the currently selected union arm (using the discriminator field)
for union type fields and hide the other arm data parts that are generated as separate csharp fields but are not
currently selected via the discriminator field.
This should apply also to the dynamic building of editing fields for the dynamic panel used for sending samples.
Based on the currently selected value of the discriminator, only relevant csharp data struct field should be offered
editing and the other should be hidden from editing.

[TODO] Auto open of web-blowser, auto-close once browser disconnected.
the ddsmon can remove the https way and keep just the http one for simplicity. security is not an issue in ddsmon case.
by default (with no command line args) the ddsmon should open the default web browser 
 using the http localhost address and correct port.
And to terminate when the browser disconnects (user closes the browser tab)
or when the browser fails to connect within given timeout (browser failed to open).
Command line arguments to define the browser connection and disconnection timeouts.


[TODO] Default topic name based on full namespace
Topic name must by default (if [DdsTopic] attribute does NOT specify the name)
include full namespace name of the message struct in Csharp notation, using underscore instead of dots.
This allows using StartsWith filter to cover whole family of topcis.

[TODO] Multi-participant reception
The ddsmon should support listening to different partitions and domains at the same time, using multiple participants,
each assigned a concrete domain id and partition name.
The ddsmon should read the samples from all participants, and stamp them with a unique global sample ordinal
so that each sample is uniquely identifiable.

The sample information kept by the ddsmon should include the link to the participant (maybe just some index to global
participant table if not a full reference), allowing for retrieving the domain id and partition name for each sample.

These both need to be serialized to json for each sample (next to the sample ordinal and incoming time stamp.) 

The sample filter used for the sample replay must be able to address these fields (ordinal, partition, domain, incoming time stamp).

DdsMonitor sample detail panel should show this information in its 'Sample Info' tab.


[TODO] Start/Pause/Reset, Domains/partitions indicator, 
Dds monitor should show the "Start/Pause/Reset" buttons (colored icons, like a tape recorder) directly in ints main menu line,
after all main menu items.
There should also be an indicator what domains and partitions are currently actively listened to (based on the participants).
By clicking this indicator a dialog should open allowing to add/remove/edit the listening participants. 
This dialog must be accessible also from the 'Windows' main menu.
The participant settings should be valid just until the ddsmon terminates.
Ddsmon need new command line arguments to specify the participant parameters (domain, partition) for each participant.
By default the ddsmon should start listening on the default domain and empty partition.




[TODO] headless recorder/replay
ddsmon to run in headless mode
  1. Recording the traffic to a json file according to given filter expression.
  2. Replay the traffic from given json file according to given filter expression
   including the time range (or sample ordinal range)
   The filter must support string comparions "StartsWith" applicable to topic name

new CLI Options:
  1. replay rate (float; 1=real time, 2=2 times realtime etc)
  2. filter exression to be applied to the recorded or replayed data.
        - On live recording, Filter removes non-matching incoming samples (not allocating ordinal for them)
        - On replay, filter removes non-matching samples from the input file (not counting them into total frame count)



---



--- now the items one by one ---

i would like to discuss how to Add support for inline arrays specified using \[InlineArray\] attribute. Now we support unmanaged fixed size arrays. What would it take to support the inline arrays?

To support C# 12 `[InlineArray]` attributes in the CycloneDDS.NET stack alongside your current unmanaged `fixed` arrays, you will need to update several specific subsystems. Currently, the system relies on `unsafe fixed` buffers which generate a struct with a specific compiler-generated field named `FixedElementField`.

Here is what it would take to fully support `[InlineArray]` across the provided codebase:

**1\. Update JSON Serialization in DdsMonitor (****FixedBufferJsonConverter****)** Currently, `FixedBufferJsonConverterFactory` and `FixedBufferJsonConverter` are heavily hardcoded to support `public unsafe fixed T Name[N]` structs by explicitly looking for a single instance field named `FixedElementField`.

-   **What needs to change:** A struct decorated with `[InlineArray]` has exactly one field, but it is _user-defined_, not necessarily named `FixedElementField`. You will need to update the converter to look for the `[System.Runtime.CompilerServices.InlineArrayAttribute]` on the struct.-   **Data access:** Instead of looking up `FixedElementField`, the converter can cast the struct to a `Span<T>` (using `MemoryMarshal.CreateSpan` or similar ref semantics) to cleanly read and write the elements during JSON serialization.

**2\. Update Field Metadata Extraction (****FieldMetadata****)** The `FieldMetadata` class uses an `isFixedSizeArray` boolean to determine if a field represents a C# fixed-size buffer where "elements are copied on each access".

-   **What needs to change:** The reflection logic that populates `FieldMetadata` needs to be updated to recognize fields whose types are decorated with `[InlineArray]`.-   **Length detection:** Currently, the system likely uses your custom `[ArrayLengthAttribute]` or parses the unmanaged byte sizes to find the length. For inline arrays, you can extract the `fixedArrayLength` directly from the `[InlineArray(int length)]` attribute argument.

**3\. Update Code Generation (****CycloneDDS.CodeGen****)** Your code generators build the serializers and C# structures from schemas.

-   **What needs to change:** The `SerializerEmitter` and `ViewEmitter` need to learn how to interact with `[InlineArray]` types. Because `[InlineArray]` instances can implicitly cast to `Span<T>`, the `SerializerEmitter` can be simplified to use standard `Span` operations rather than `unsafe` pointers to read/write from the DDS CDR streams.

**4\. Update the IDL Importer (****CycloneDDS.IdlImporter****)** If you want to generate safe `[InlineArray]` structs from IDL arrays instead of legacy `fixed` arrays:

-   **What needs to change:** The `TypeMapper` and `CSharpEmitter` will need to generate the `[InlineArray(N)]` attribute syntax above a helper struct, rather than generating `[ArrayLength]` or `unsafe fixed` arrays in the generated C# files.

**Summary** The transition is mostly about swapping out `unsafe` pointer logic and hardcoded `FixedElementField` lookups for `Span<T>` access and `InlineArrayAttribute` reflection checks. Because `[InlineArray]` acts cleanly as a `Span<T>`, it will likely simplify your serialization emitters and JSON converters significantly once implemented.

I need to also adapt the dds monitor field rendering (in various sample tables icluding the samples panel and sample details panels) as well as dynamic building of the editing fields in the dynamic panel used for sending

To adapt the DDS Monitor UI and dynamic form systems for `[InlineArray]`, you will need to update the core metadata extraction that drives the UI, adjust the data-binding for the dynamic forms, and ensure the grid and detail panels can format the new memory layout.

Here are the specific adaptations required across the monitoring and sending panels:

**1\. Update Metadata Extraction (****FieldMetadata** **and** **TopicMetadata****)** The UI panels are entirely driven by `FieldMetadata` which describes the structure of your DDS topics.

-   **Identification:** Currently, `FieldMetadata.IsFixedSizeArray` is set to `true` when the engine detects a C# `unsafe fixed` buffer. You must update `TopicMetadata`'s reflection scanning to also set `IsFixedSizeArray = true` when a field's type has the `[InlineArray]` attribute.-   **Type and Length Extraction:** Extract the `ElementType` from the single defined field inside the inline array struct, and extract the `FixedArrayLength` from the attribute constructor, passing these to the `FieldMetadata` constructor.

**2\. Data Binding in the Sender (****DynamicForm.razor** **&** **DrawerContext****)** The `DynamicForm` component automatically generates editing fields based on `FieldMetadata`. It already features specific CSS and logic for fixed-size arrays (`.dynamic-form__array-fixed`) which intentionally omits the "Add" and "Remove" row buttons used for standard lists.

-   **UI Rendering:** Because the UI relies on the `IsFixedSizeArray` boolean, tagging `[InlineArray]` types correctly in step 1 will make the form automatically render the correct fixed-length input rows.-   **Data Binding (The Challenge):** The `TypeDrawerRegistry` uses `DrawerContext` to bind values with a `ValueGetter` and an `OnChange` callback. Because `[InlineArray]` is a value type (struct), modifying a single element via reflection in a form field cannot be done directly on a boxed struct. You must update the `Setter` delegates generated in `TopicMetadata` so that when the UI updates index `i`, the engine unboxes the `[InlineArray]`, casts it to a `Span<T>` to update the specific element, and then writes the _entire struct_ back to the parent payload.

**3\. Sample Details Rendering (****DetailPanel.razor****)** The `DetailPanel` displays samples in a tree view and as raw JSON.

-   **JSON View:** The detail JSON view and the "Expand All" cards in the samples panel rely on `System.Text.Json` via `DdsJsonOptions.Export`. As discussed previously, updating `FixedBufferJsonConverter<T>` to handle `[InlineArray]` will automatically fix the JSON payload rendering everywhere in the UI.-   **Tree View:** For the object tree, ensure that your underlying property navigators in `DetailPanel` cast the `[InlineArray]` to `ReadOnlySpan<T>` or `IEnumerable<T>` so the elements can be mapped to individual `.detail-tree__node` elements.

**4\. Grid Column Rendering (****SamplesPanel.razor** **&** **InstancesPanel.razor****)** When a user selects an array field as a column in the `SamplesPanel` or `InstancesPanel` using the Column Picker, the grid calls the compiled `Getter` delegate from `FieldMetadata` to populate the cell.

-   **Cell Formatting:** `[InlineArray]` instances do not implement `IEnumerable` by default. If the grid blindly calls `.ToString()` on the struct, it will just print the type name. You need to update the `Getter` expression tree generated in `TopicMetadata` to convert the `[InlineArray]` into a formatted string (e.g., `"[val1, val2, ...]"`), or an `object[]`, so that `.samples-panel__cell` can display the preview text properly.

i need to extend the filtering possibility by the StartsWith and EndsWith operators. What do i need to do? Both in the engine as well in the dds monitor presentation/editing layers.

To support `StartsWith` and `EndsWith` filtering operators across the engine and UI, you need to map these concepts from your existing domain models to the dynamic LINQ compiler, and expose them to the user in the Blazor visual builder.

Interestingly, your `FilterComparisonOperator` enum in `FilterNodes.cs` **already includes** **StartsWith****,** **EndsWith****, and** **Contains**. To make them functional, here is what you need to do:

1\. Update the Engine (`FilterCompiler.cs`)

The `FilterCompiler` translates your `FilterNode` tree into a string expression that `System.Linq.Dynamic.Core` evaluates. Dynamic LINQ fully supports standard C# string methods, but you must instruct the compiler to format them as method calls rather than standard binary operators (`==`, `>`, etc.).

You need to update the compilation loop inside `FilterCompiler.cs` to format the expression string differently when it encounters these operators. For example:

```
// Inside your switch statement for FilterComparisonOperator in FilterCompiler.cs
switch (conditionNode.Operator)
{
    case FilterComparisonOperator.Equals:
        return $"{conditionNode.FieldPath} == @{argIndex}";
    case FilterComparisonOperator.StartsWith:
        // Generates: Payload.MyString.StartsWith("UserValue")
        return $"{conditionNode.FieldPath}.StartsWith(@{argIndex})";
    case FilterComparisonOperator.EndsWith:
        return $"{conditionNode.FieldPath}.EndsWith(@{argIndex})";
    // ... handling other operators
}
```

2\. Update the Presentation Layer (`FilterBuilderPanel.razor`)

The `FilterBuilderPanel` provides the visual interface for constructing the `FilterNode` tree. You need to expose the new operators in the condition's `<select>` dropdown element.

-   **Add the UI Options:** Inside the operator `<select>` element (which uses the `.filter-builder__select` CSS class), add the `<option>` tags for `StartsWith` and `EndsWith`.-   **Restrict by Type (Recommended):** Because `StartsWith` and `EndsWith` only make sense for string comparisons, you should ideally wrap these `<option>` tags in a Razor `@if` block that checks the `ValueType` of the currently selected `FieldMetadata`.

```
@if (selectedField.ValueType == typeof(string) || selectedField.ValueType.Name.StartsWith("FixedString")) 
{
    <option value="@FilterComparisonOperator.StartsWith">Starts With</option>
    <option value="@FilterComparisonOperator.EndsWith">Ends With</option>
}
```

3\. Fulfilling the Application TODOs

Adding these string comparisons will directly satisfy several pending architectural requirements you have outlined in your `TODO.md` documents:

-   **Traffic Recording and Replay:** It will allow the system to record traffic to a JSON file (and replay it) using `StartsWith` string comparisons.-   **Topic Family Filtering:** It fulfills the requirement to allow users to filter a "whole family of topics" based on namespace grouping (e.g., using `StartsWith` on a generic topic name filter). Note that if you intend to filter on the _Topic Name_ itself (rather than just the sample `Payload` fields), you will also need to ensure your `FilterCompiler` logic exposes the topic name as a top-level bindable property to Dynamic LINQ.

next requirement is \[TODO\] Default topic name based on full namespace now "DdsTopic" requires a topic name as parameter. I need the topic name to be optional. Topic name must by default (if \[DdsTopic\] attribute does NOT specify the name) include full namespace name of the message struct in Csharp notation, using underscore instead of dots.

How?

To make the topic name optional and default to the namespace-based format, you will need to update the attribute definition and adjust both the runtime reflection logic and the compile-time code generator to handle the fallback name. This directly fulfills your pending requirement to allow users to filter whole families of topics using the `StartsWith` operator.

Here are the specific steps to implement this:

**1\. Update** **DdsTopicAttribute** First, modify the attribute so that the topic name is optional. You can do this by adding a parameterless constructor or providing a default `null` value to the existing constructor. Update `src/CycloneDDS.Schema/Attributes/TypeLevel/DdsTopicAttribute.cs`:

```
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
public sealed class DdsTopicAttribute : Attribute
{
    // Make TopicName nullable
    public string? TopicName { get; }

    // Allow empty constructor or default null parameter
    public DdsTopicAttribute(string? topicName = null)
    {
        TopicName = topicName;
    }
}
```

**2\. Update Runtime Topic Discovery** When the `DdsMonitor.Engine` scans your assemblies for topics at runtime, it needs to compute the default name if the attribute's `TopicName` is null. Update the reflection logic in `src/DdsMonitor.Engine/Discovery/TopicDiscoveryService.cs` or `src/DdsMonitor.Engine/Metadata/TopicMetadata.cs`:

```
// Inside your assembly scanning loop where you find types with [DdsTopic]
var attr = type.GetCustomAttribute<DdsTopicAttribute>();

// Determine the topic name
string topicName = attr.TopicName;
if (string.IsNullOrWhiteSpace(topicName))
{
    // type.FullName gives "FeatureDemo.Scenarios.ChatRoom.ChatMessage"
    // We replace dots with underscores to get "FeatureDemo_Scenarios_ChatRoom_ChatMessage"
    topicName = type.FullName?.Replace('.', '_') ?? type.Name;
}

// Pass this computed topicName to the TopicMetadata constructor
```

**3\. Update Compile-Time Code Generation** Your `CycloneDDS.CodeGen` tool builds the serializers and IDL files during the build process and must also be aware of this fallback naming convention. In `tools/CycloneDDS.CodeGen/SchemaDiscovery.cs`, when you analyze the Roslyn syntax tree and semantic model for `[DdsTopic]`:

```
// When inspecting the [DdsTopic] attribute on an INamedTypeSymbol
string? explicitName = GetAttributeArgument(ddsTopicAttribute, 0); // however you currently extract the string argument

string finalTopicName;
if (string.IsNullOrWhiteSpace(explicitName))
{
    // Construct the full name manually from the Roslyn symbol
    string fullNamespace = typeSymbol.ContainingNamespace.ToString();
    string className = typeSymbol.Name;
    
    // Replace dots with underscores
    string csharpFullName = string.IsNullOrEmpty(fullNamespace) 
        ? className 
        : $"{fullNamespace}.{className}";
        
    finalTopicName = csharpFullName.Replace('.', '_');
}
else
{
    finalTopicName = explicitName;
}
```

**4\. Update Dependent Source Generators/Emitters** Make sure any code that relies on `TopicName` in `tools/CycloneDDS.CodeGen/IdlEmitter.cs` and `tools/CycloneDDS.CodeGen/SerializerEmitter.cs` consumes the `finalTopicName` resolved in Step 3, rather than blindly attempting to read a potentially null value directly from the attribute metadata.

By replacing the dots with underscores, your `DdsMonitor` will now be able to leverage the `StartsWith` filter (e.g., `StartsWith("FeatureDemo_Scenarios_")`) to efficiently select and replay traffic from entire message families.

ok next request is \[TODO\] typed enums how to Add support for defining the bit size of the enum data type In csharp DSL we should read the type specification behing the comma

```
public enum ESomething : byte
```

and convert it to @bit\_bound annotation in IDL..

```
@bit_bound(8)
enum ESomething {
  ...
};
```

When you use the @bit\_bound(8) annotation with an enum in Cyclone DDS, the generated plain-C code typically maps the field to a uint8\_t.

Mapping Behavior

-   In standard IDL-to-C mapping, an enum is often represented by a standard C enum type, which compilers usually default to 32 bits (int). However, Cyclone's IDL compiler (idlc) uses the @bit\_bound annotation to optimize the storage and serialization size:
    -   uint8\_t: Used when @bit\_bound is between 1 and 8 bits.
    -   uint16\_t: Used when @bit\_bound is between 9 and 16 bits.
    -   uint32\_t: The default size if the annotation is omitted or set between 17 and 32 bits.

To fulfill the pending requirement for typed enums and the `@bit_bound` annotation, you need to update your compile-time code generator (`CycloneDDS.CodeGen`) to inspect the underlying types of your C# enums and emit the correct IDL annotations.

Here is the step-by-step implementation to achieve this:

**1\. Update the Data Model (****TypeInfo.cs****)** First, extend your `TypeInfo` model to store the bit bound so that the IDL emitter and the serializer can use it. Modify `tools/CycloneDDS.CodeGen/TypeInfo.cs` to include a new property:

```
public class TypeInfo 
{
    // Existing properties...
    public DdsExtensibilityKind Extensibility { get; set; } = DdsExtensibilityKind.Appendable;
    public string Name { get; set; } = string.Empty;
    
    // New property for Enum bit size
    public int EnumBitBound { get; set; } = 32; // Standard 32-bit default
}
```

**2\. Extract the Underlying Type (****SchemaDiscovery.cs****)** When Roslyn parses an `enum` in C#, it makes the underlying type available via `INamedTypeSymbol.EnumUnderlyingType`. You need to map these .NET types to the correct bit lengths. In `tools/CycloneDDS.CodeGen/SchemaDiscovery.cs`, locate where you build the `TypeInfo` for an enum, and map the underlying type:

```
if (typeSymbol.TypeKind == TypeKind.Enum)
{
    int bitBound = 32; // Default IDL enum size
    if (typeSymbol.EnumUnderlyingType != null)
    {
        bitBound = typeSymbol.EnumUnderlyingType.SpecialType switch
        {
            SpecialType.System_Byte or SpecialType.System_SByte => 8,
            SpecialType.System_Int16 or SpecialType.System_UInt16 => 16,
            SpecialType.System_Int32 or SpecialType.System_UInt32 => 32,
            _ => 32 // Fallback
        };
    }
    
    typeInfo.EnumBitBound = bitBound;
    // ... populate enum members
}
```

**3\. Emit the** **@bit\_bound** **Annotation (****IdlEmitter.cs****)** Next, update the IDL generator to conditionally write the annotation above the enum definition. In `tools/CycloneDDS.CodeGen/IdlEmitter.cs`, find the block that generates the `enum` syntax and prepend the attribute:

```
// Inside the loop emitting types in IdlEmitter
if (type.IsEnum) // Assuming you have an IsEnum flag or check
{
    // Cyclone DDS default is 32. We only need to annotate if it's 8 or 16.
    if (type.EnumBitBound < 32)
    {
        sb.AppendLine($"    @bit_bound({type.EnumBitBound})");
    }
    sb.AppendLine($"    enum {type.Name} {{");
    
    // ... loop through enum members and emit them ...
    
    sb.AppendLine("    };");
}
```

**4\. Update Serialization (****SerializerEmitter.cs****)** Because standard enums default to 4 bytes (`int32_t`), your current `SerializerEmitter` likely uses `WriteInt32` and `ReadInt32` for all enums. To ensure your C# application writes the correct number of bytes to the network matching the `uint8_t` or `uint16_t` layout dictated by Cyclone DDS, update the serializer logic to respect the `EnumBitBound`:

```
// Inside tools/CycloneDDS.CodeGen/SerializerEmitter.cs
string writeMethod = type.EnumBitBound switch 
{
    8 => "WriteUInt8",
    16 => "WriteUInt16",
    _ => "WriteInt32"
};

string readMethod = type.EnumBitBound switch 
{
    8 => "ReadUInt8",
    16 => "ReadUInt16",
    _ => "ReadInt32"
};

// Generate the cast correctly in the emitter:
// e.g. writer.WriteUInt8((byte)payload.MyEnumField);
```

_(Note: You can rely on your existing mapping helpers in_ _TypeMapper.cs_ _if you pass the underlying primitive type string like_ _"byte"_ _or_ _"ushort"_ _rather than resolving it manually)._

This pipeline ensures that writing `public enum ESomething : byte` in C# naturally flows into a 1-byte `@bit_bound(8)` representation in the IDL and perfectly aligns memory across the C# and C/C++ boundaries.

next request is \[TODO\] DdsMonitor should render just the currently selected union arm (using the discriminator field) for union type fields and hide the other arm data parts that are generated as separate csharp fields but are not currently selected via the discriminator field.

This should apply also to the dynamic building of editing fields for the dynamic panel used for sending samples. Based on the currently selected value of the discriminator, only relevant csharp data struct field should be offered editing and the other should be hidden from editing.

pls suggest how.

To support dynamic rendering and editing of union types based on the active discriminator, you need to leverage the existing `[DdsUnion]`, `[DdsDiscriminator]`, and `[DdsCase]` attributes emitted by your IDL importer.

Here is the step-by-step approach to implement this across the engine and UI layers:

1\. Extend the Metadata Model (`FieldMetadata.cs` & `TopicMetadata.cs`)

Currently, `TopicMetadata` flattens all fields into a single list. You need to add properties to `FieldMetadata` so the UI knows if a field is a union arm and which discriminator controls it.

**Update** **FieldMetadata.cs****:** Add new properties to track union relationship:

```
public sealed class FieldMetadata 
{
    // Existing properties...
    
    // Union specific properties
    public string? DependentDiscriminatorPath { get; }
    public object? ActiveWhenDiscriminatorValue { get; }
    public bool IsDefaultUnionCase { get; }

    // Update constructor to accept these...
}
```

**Update** **TopicMetadata.cs** **Reflection Scanning:** When recursively flattening fields, check if the declaring type has `[DdsUnionAttribute]`. If it does, find the field with `[DdsDiscriminatorAttribute]`. Pass the discriminator's path and the `[DdsCase]` values to the arms.

```
bool isUnion = type.GetCustomAttribute<DdsUnionAttribute>() != null;
string? discriminatorPath = null;

if (isUnion)
{
    var discMember = members.FirstOrDefault(m => m.GetCustomAttribute<DdsDiscriminatorAttribute>() != null);
    discriminatorPath = discMember != null ? $"{parentPath}{discMember.Name}" : null;
}

foreach (var member in members)
{
    // ... existing extraction ...
    
    object? caseValue = null;
    bool isDefaultCase = false;
    
    if (isUnion && member.GetCustomAttribute<DdsDiscriminatorAttribute>() == null)
    {
        var caseAttr = member.GetCustomAttribute<DdsCaseAttribute>();
        if (caseAttr != null) caseValue = caseAttr.Value;
        
        if (member.GetCustomAttribute<DdsDefaultCaseAttribute>() != null)
            isDefaultCase = true;
    }

    // Pass discriminatorPath, caseValue, and isDefaultCase into FieldMetadata
}
```

2\. Update the Sender Form (`DynamicForm.razor`)

The `DynamicForm` iterates over `EditableFields` to generate inputs. You need to intercept this loop and skip fields (arms) that do not match the current discriminator value.

**Update the loop in** **DynamicForm.razor****:**

```
@foreach (var field in EditableFields)
{
    // Check if this field is a union arm
    if (field.DependentDiscriminatorPath != null)
    {
        // 1. Find the discriminator field metadata
        var discField = EditableFields.First(f => f.StructuredName == field.DependentDiscriminatorPath);
        
        // 2. Read the *current* live value of the discriminator from the Payload
        var currentDiscValue = discField.Getter(Payload);
        
        // 3. Evaluate if this arm is active
        bool isActiveArm = false;
        if (field.IsDefaultUnionCase) 
        {
            // Logic to check if currentDiscValue doesn't match ANY known DdsCase
            // isActiveArm = ...
        }
        else 
        {
            // Compare values (handle type conversions like bool/int/enum carefully)
            isActiveArm = object.Equals(Convert.ChangeType(currentDiscValue, field.ActiveWhenDiscriminatorValue.GetType()), field.ActiveWhenDiscriminatorValue);
        }

        if (!isActiveArm) continue; // Skip rendering this input row!
    }

    // ... proceed with rendering <div class="dynamic-form__row"> ...
}
```

_Note: Because_ _DynamicForm_ _leverages_ _DrawerContext_ _with_ _OnChange_ _callbacks__, whenever the user changes the discriminator dropdown/checkbox, Blazor will re-render the_ _DynamicForm__, automatically hiding the old arm and showing the new one._

3\. Update the Samples Inspector (`DetailPanel.razor`)

When viewing historical samples, the object tree should also hide inactive union arms.

In `DetailPanel.razor`, where you use reflection to expand a `.detail-tree__node` into its children:

-   Check if the object being expanded is decorated with `[DdsUnion]`.-   If yes, read the value of the property marked with `[DdsDiscriminator]`.-   Filter the children: Only yield the discriminator property itself, AND the specific property whose `[DdsCase(value)]` matches the discriminator's value (or the `[DdsDefaultCase]` if none match).-   Render these filtered children, skipping the inactive unmanaged/dummy fields generated by the IDL importer.

By applying this logic, both the structural tree view and the dynamic editing form will seamlessly act like true tagged unions, presenting only the active data path to the user.

next requets is \[TODO\] Auto open of web-blowser, auto-close once browser disconnected. the ddsmon can remove the https way and keep just the http one for simplicity. security is not an issue in ddsmon case. by default (with no command line args) the ddsmon should open the default web browser using the http localhost address and correct port. And to terminate when the browser disconnects (user closes the browser tab) or when the browser fails to connect within given timeout (browser failed to open). Command line arguments to define the browser connection and disconnection timeouts.

how?

To implement the auto-open and auto-close browser requirements for the DDS Monitor, we can take advantage of the fact that it is a Blazor Server application. Blazor Server uses a persistent SignalR connection (a "Circuit") to communicate with the browser. By tracking when circuits open and close, we can reliably detect when the user connects or closes their browser tab.

Here is the step-by-step implementation to adapt your application:

1\. Create the Browser Lifecycle Options

First, define a configuration class to hold your timeouts. ASP.NET Core will automatically map command-line arguments (like `--ConnectTimeout 30`) to this class.

Create a new file `tools/DdsMonitor/DdsMonitor.Engine/Hosting/BrowserLifecycleOptions.cs`:

```
namespace DdsMonitor.Engine.Hosting;

public class BrowserLifecycleOptions
{
    /// <summary>Timeout in seconds to wait for the initial browser connection.</summary>
    public int ConnectTimeout { get; set; } = 15;
    
    /// <summary>Timeout in seconds to wait before shutting down after all tabs are closed. (Allows for F5 refreshes)</summary>
    public int DisconnectTimeout { get; set; } = 5;
}
```

2\. Implement a Blazor Circuit Tracker

We need to track the active connections. Create a custom `CircuitHandler` that increments a counter when a browser tab connects, and decrements it when it disconnects.

Create a new file `tools/DdsMonitor/DdsMonitor.Blazor/Services/BrowserTrackingCircuitHandler.cs`:

```
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Server.Circuits;

namespace DdsMonitor.Services;

public sealed class BrowserTrackingCircuitHandler : CircuitHandler
{
    public event Action<bool>? ConnectionChanged;
    private int _connectedCircuits = 0;

    public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        if (Interlocked.Increment(ref _connectedCircuits) == 1)
        {
            // The first browser tab connected
            ConnectionChanged?.Invoke(true);
        }
        return Task.CompletedTask;
    }

    public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        if (Interlocked.Decrement(ref _connectedCircuits) == 0)
        {
            // All browser tabs have been closed
            ConnectionChanged?.Invoke(false);
        }
        return Task.CompletedTask;
    }
}
```

3\. Implement the Auto-Close Background Service

This service will listen to the circuit tracker and gracefully stop the application if timeouts are reached.

Create a new file `tools/DdsMonitor/DdsMonitor.Blazor/Services/BrowserLifecycleService.cs`:

```
using System;
using System.Threading;
using System.Threading.Tasks;
using DdsMonitor.Engine.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DdsMonitor.Services;

public sealed class BrowserLifecycleService : BackgroundService
{
    private readonly BrowserTrackingCircuitHandler _circuitHandler;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly BrowserLifecycleOptions _options;
    private CancellationTokenSource? _timeoutCts;
    private readonly object _sync = new();

    public BrowserLifecycleService(
        BrowserTrackingCircuitHandler circuitHandler,
        IHostApplicationLifetime lifetime,
        IOptions<BrowserLifecycleOptions> options)
    {
        _circuitHandler = circuitHandler;
        _lifetime = lifetime;
        _options = options.Value;
        
        _circuitHandler.ConnectionChanged += OnConnectionChanged;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Start the initial connection timeout immediately upon boot
        ResetTimeout(_options.ConnectTimeout, "Browser failed to connect in time.");
        return Task.CompletedTask;
    }

    private void OnConnectionChanged(bool isConnected)
    {
        if (isConnected)
        {
            // A browser is connected; cancel any pending termination timeouts
            lock (_sync)
            {
                _timeoutCts?.Cancel();
                _timeoutCts = null;
            }
        }
        else
        {
            // All browsers disconnected; start the disconnect countdown (allowing time for page refreshes)
            ResetTimeout(_options.DisconnectTimeout, "Browser disconnected.");
        }
    }

    private void ResetTimeout(int seconds, string reason)
    {
        lock (_sync)
        {
            _timeoutCts?.Cancel();
            _timeoutCts = new CancellationTokenSource();
            
            Task.Delay(TimeSpan.FromSeconds(seconds), _timeoutCts.Token).ContinueWith(t =>
            {
                if (!t.IsCanceled)
                {
                    Console.WriteLine($"\n[Shutdown] {reason} Initiating auto-close after {seconds}s.");
                    _lifetime.StopApplication();
                }
            });
        }
    }
}
```

4\. Update the Application Entry Point

Finally, modify `tools/DdsMonitor/DdsMonitor.Blazor/Program.cs` to force HTTP, register the new services, and automatically launch the default browser once the server address is bound.

Update `Program.cs` as follows:

```
using System.Diagnostics;
using System.Linq;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Builder;
using DdsMonitor.Components;
using DdsMonitor.Engine.Hosting;
using DdsMonitor.Engine.Ui;
using DdsMonitor.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Force HTTP only, bind to loopback, and let the OS assign a free port (port 0)
builder.WebHost.UseUrls("http://127.0.0.1:0");

// 2. Bind command-line arguments to our options class
builder.Services.Configure<BrowserLifecycleOptions>(builder.Configuration);

// 3. Register standard services
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddDdsMonitorServices(builder.Configuration);
builder.Services.AddSingleton<ITypeDrawerRegistry, TypeDrawerRegistry>();
builder.Services.AddSingleton<TooltipService>();
builder.Services.AddSingleton<ContextMenuService>();
builder.Services.AddScoped<WorkspacePersistenceService>();

// 4. Register our custom lifecycle tracking services
builder.Services.AddSingleton<BrowserTrackingCircuitHandler>();
// Forward the standard CircuitHandler interface to our custom implementation
builder.Services.AddSingleton<CircuitHandler>(sp => sp.GetRequiredService<BrowserTrackingCircuitHandler>());
builder.Services.AddHostedService<BrowserLifecycleService>();

var app = builder.Build();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode();

// 5. Start the web host asynchronously instead of blocking with app.Run()
await app.StartAsync();

// 6. Extract the dynamically assigned local HTTP address
var server = app.Services.GetRequiredService<IServer>();
var addresses = server.Features.Get<IServerAddressesFeature>();
var localUrl = addresses?.Addresses.FirstOrDefault();

if (localUrl != null)
{
    Console.WriteLine($"DDS Monitor listening at {localUrl}");
    
    // Auto-open the default web browser using the shell
    try
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = localUrl,
            UseShellExecute = true
        });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to auto-open browser: {ex.Message}");
    }
}

// 7. Wait indefinitely until the background lifecycle service calls StopApplication()
await app.WaitForShutdownAsync();
```

How to use it:

-   **Normal Launch:** By default, running `ddsmon.exe` will bind to a random HTTP port, launch your browser, and auto-terminate 5 seconds after you close the tab.-   **Custom Timeouts:** Users can override the timeouts using standard .NET command-line arguments:

next request is \[TODO\] Multi-participant reception The ddsmon should support listening to different partitions and domains at the same time, using multiple participants, each assigned a concrete domain id and partition name. The ddsmon should read the samples from all participants, and stamp them with a unique global sample ordinal so that each sample is uniquely identifiable.

The sample information kept by the ddsmon should include the link to the participant (maybe just some index to global participant table if not a full reference), allowing for retrieving the domain id and partition name for each sample.

These both need to be serialized to json for each sample (next to the sample ordinal and incoming time stamp.)

The sample filter used for the sample replay must be able to address these fields (ordinal, partition, domain, incoming time stamp).

DdsMonitor sample detail panel should show this information in its 'Sample Info' tab.

pls suggest how.

To fulfill the requirement for multi-participant reception, global sample indexing, and extended filtering, you must update the core domain models, the DDS bridging layer, the serialization pipeline, and the user interface.

Here is the step-by-step implementation:

**1\. Update the Domain Models (****SampleData.cs** **and** **SampleExportRecord.cs****)** To allow retrieving the domain ID and partition name for each sample and serialize them to JSON, add these fields directly to your data structures.

-   Update `tools/DdsMonitor/DdsMonitor.Engine/Models/SampleData.cs`:

```
public record SampleData
{
    public long Ordinal { get; init; }
    public DateTime IncomingTimestamp { get; init; } // Ensure this exists
    public int ParticipantIndex { get; init; }
    public uint DomainId { get; init; }
    public string PartitionName { get; init; } = string.Empty;
    
    // ... existing fields (TopicTypeName, Payload, etc.)
}
```

-   Update `tools/DdsMonitor/DdsMonitor.Engine/Import/SampleExportRecord.cs` so the JSON serializer captures them during export/import:

```
internal sealed class SampleExportRecord
{
    public long Ordinal { get; set; }
    public DateTime IncomingTimestamp { get; set; }
    public uint DomainId { get; set; }
    public string PartitionName { get; set; } = string.Empty;
    // ... existing fields
}
```

**2\. Implement Multi-Participant Reception (****IDdsBridge.cs** **and** **DdsBridge.cs****)** Currently, `IDdsBridge` exposes a single `DdsParticipant`. You must change this to support multiple participants.

-   Modify `IDdsBridge` to expose a collection of participants:

```
public interface IDdsBridge : IDisposable 
{
    IReadOnlyList<DdsParticipant> Participants { get; }
    // ...
}
```

-   In `DdsBridge.cs`, update the initialization logic to iterate over a configured list of Domains/Partitions (from `DdsSettings`) and instantiate a `DdsParticipant` for each.-   When a user subscribes to a topic (e.g., in `CreateDynamicReader`), **DdsBridge** **must create a** **DdsReader<T>** **for every active participant** and aggregate their data streams.

**3\. Implement the Global Sample Ordinal (****DdsIngestionService.cs** **or** **DynamicReader.cs****)** To stamp samples with a unique global ordinal so that each sample is uniquely identifiable, use a thread-safe static or singleton counter.

-   Define a global counter in your ingestion layer:

```
private static long _globalOrdinalCounter = 0;
```

-   When a `DynamicReader` receives a raw DDS sample, construct the `SampleData` by incrementing this counter and injecting the participant's metadata:

```
long uniqueOrdinal = Interlocked.Increment(ref _globalOrdinalCounter);

var sampleData = new SampleData 
{
    Ordinal = uniqueOrdinal,
    IncomingTimestamp = DateTime.UtcNow,
    ParticipantIndex = readerParticipantIndex,
    DomainId = readerDomainId,
    PartitionName = readerPartitionName,
    // ... payload mapping
};
```

**4\. Update the Filtering Engine (****FilterCompiler.cs****)** The `FilterCompiler` uses `System.Linq.Dynamic.Core` to evaluate strings into a `Func<SampleData, bool>`.

-   Because you added `Ordinal`, `DomainId`, `PartitionName`, and `IncomingTimestamp` directly to the `SampleData` record in step 1, **the dynamic LINQ compiler will automatically support addressing these fields**.-   Users will simply write filters like `Ordinal > 100 AND DomainId == 0 AND Payload.MyField == 42`.-   Ensure the `PayloadFieldRegex` in `FilterCompiler.cs` does not accidentally corrupt these top-level metadata properties. It currently correctly targets strings beginning with `Payload.`, so top-level properties are safe.

**5\. Update the UI (****DetailPanel.razor****)** The `DetailPanel` needs to display this new metadata in its "Sample Info" tab.

-   In `tools/DdsMonitor/DdsMonitor.Blazor/Components/DetailPanel.razor`, locate the HTML rendering the Sample Info table.-   Add the new rows:

```
<table class="detail-table">
    <tbody>
        <tr>
            <th>Global Ordinal</th>
            <td>@_currentSample.Ordinal</td>
        </tr>
        <tr>
            <th>Incoming Timestamp</th>
            <td>@_currentSample.IncomingTimestamp.ToString("o")</td>
        </tr>
        <tr>
            <th>Domain ID</th>
            <td>@_currentSample.DomainId</td>
        </tr>
        <tr>
            <th>Partition</th>
            <td>@(string.IsNullOrEmpty(_currentSample.PartitionName) ? "<default>" : _currentSample.PartitionName)</td>
        </tr>
        <!-- Existing rows -->
    </tbody>
</table>
```

next request is \[TODO\] Start/Pause/Reset, Domains/partitions indicator Dds monitor should show the "Start/Pause/Reset" buttons (colored icons, like a tape recorder) directly in ints main menu line, after all main menu items. There should also be an indicator what domains and partitions are currently actively listened to (based on the participants). By clicking this indicator a dialog should open allowing to add/remove/edit the listening participants. This dialog must be accessible also from the 'Windows' main menu. The participant settings should be valid just until the ddsmon terminates. Ddsmon need new command line arguments to specify the participant parameters (domain, partition) for each participant. By default the ddsmon should start listening on the default domain and empty partition.

pls suggest how, suggest the layout of the participant editor panel (asciiart wireframe).

To implement the global transport controls (Start/Pause/Reset) and the dynamic multi-participant configuration, you will need to update the configuration model to parse command-line arguments, modify the `MainLayout` to include the new UI elements, and create a new modal dialog for the participant editor.

Here is the step-by-step implementation guide along with the requested ASCII wireframe.

1\. Update Configuration Model for Command-Line Arguments

ASP.NET Core automatically parses command-line arguments into the `IOptions` configuration hierarchy. First, update `DdsSettings.cs` to handle a list of participants instead of a single domain.

**Update** **tools/DdsMonitor/DdsMonitor.Engine/DdsSettings.cs**:

```
public class ParticipantConfig
{
    public uint DomainId { get; set; } = 0;
    public string PartitionName { get; set; } = string.Empty;
}

public sealed class DdsSettings 
{
    public const string SectionName = "DdsSettings";
    
    // Default to Domain 0 and empty partition
    public List<ParticipantConfig> Participants { get; set; } = new() 
    { 
        new ParticipantConfig { DomainId = 0, PartitionName = "" } 
    };
    
    // ... existing properties ...
}
```

_Command-Line Usage:_ Users can now specify multiple participants via standard .NET configuration args: `ddsmon.exe --DdsSettings:Participants:0:DomainId=0 --DdsSettings:Participants:1:DomainId=1 --DdsSettings:Participants:1:PartitionName="Sensors"`

2\. Update the Engine (`IDdsBridge` & Global State)

The `DdsBridge` must support pausing ingestion and completely resetting the global tracking state.

**Update** **tools/DdsMonitor/DdsMonitor.Engine/IDdsBridge.cs**:

```
public interface IDdsBridge : IDisposable 
{
    IReadOnlyList<DdsParticipant> Participants { get; }
    
    bool IsPaused { get; set; }
    
    void AddParticipant(uint domainId, string partitionName);
    void RemoveParticipant(int index);
    void ResetAll(); // Clears SampleStore, InstanceStore, and resets the global ordinal
}
```

3\. Update the Main Menu / Toolbar (`MainLayout.razor`)

You need to inject the transport controls and the domain indicator directly into the `.app-toolbar` flex container after the main menu items.

**Update** **tools/DdsMonitor/DdsMonitor.Blazor/Components/Layout/MainLayout.razor**:

```
<div class="app-toolbar">
    <!-- Existing App Menu -->
    <div class="app-menu">
        <span class="app-menu__brand">DDS Monitor</span>
        <!-- ... existing File/View menus ... -->
        
        <div class="app-menu__item @(_openMenu == "windows" ? "is-open" : "")">
            <button class="app-menu__trigger" @onclick="@(() => ToggleMenu("windows"))">Windows</button>
            <div class="app-menu__dropdown">
                <!-- Add to Windows menu -->
                <button class="app-menu__dropdown-item" @onclick="OpenParticipantEditor">Participant Editor...</button>
            </div>
        </div>
    </div>

    <!-- NEW: Global Transport Controls -->
    <div class="app-toolbar__transport" style="display: flex; gap: 6px; margin-left: 16px;">
        <button class="replay-panel__btn replay-panel__btn--play" title="Start Ingestion" @onclick="() => DdsBridge.IsPaused = false">
            <svg>...</svg> <!-- Play Icon -->
        </button>
        <button class="replay-panel__btn replay-panel__btn--pause" title="Pause Ingestion" @onclick="() => DdsBridge.IsPaused = true">
            <svg>...</svg> <!-- Pause Icon -->
        </button>
        <button class="replay-panel__btn replay-panel__btn--stop" title="Reset Data" @onclick="() => DdsBridge.ResetAll()">
            <svg>...</svg> <!-- Stop/Reset Icon -->
        </button>
    </div>

    <!-- NEW: Participant Indicator -->
    <button class="app-toolbar__button" style="margin-left: auto; font-family: monospace;" @onclick="OpenParticipantEditor">
        Listening: @GetParticipantSummary()
    </button>
</div>

<ParticipantEditorDialog @bind-IsOpen="_showParticipantEditor" />

@code {
    private bool _showParticipantEditor = false;
    
    private void OpenParticipantEditor() {
        _showParticipantEditor = true;
        _openMenu = null;
    }
    
    private string GetParticipantSummary() {
        // e.g., "D:0,1 | P:*,Sensors"
        var domains = string.Join(",", DdsBridge.Participants.Select(p => p.DomainId).Distinct());
        var partitions = string.Join(",", DdsBridge.Participants.Select(p => string.IsNullOrEmpty(p.PartitionName) ? "*" : p.PartitionName).Distinct());
        return $"D:{domains} | P:{partitions}";
    }
}
```

4\. Participant Editor Dialog Layout (ASCII Wireframe)

The participant settings are volatile (valid until termination), meaning changes apply instantly to `IDdsBridge` but are not saved to the `appsettings.json`.

```
+--------------------------------------------------------------------+
| Participant Editor                                             [X] |
+--------------------------------------------------------------------+
| Configure domains and partitions to actively listen to. Changes    |
| apply immediately and are valid until the application closes.      |
|                                                                    |
| +----------------------------------------------------------------+ |
| | Domain ID     | Partition Name                 |               | |
| +----------------------------------------------------------------+ |
| | [ 0         ] | [ <default>                  ] | [ Remove ]    | |
| | [ 1         ] | [ SensorNetwork              ] | [ Remove ]    | |
| | [ 42        ] | [ Telemetry                  ] | [ Remove ]    | |
| +----------------------------------------------------------------+ |
|                                                                    |
| [ + Add Participant ]                                              |
|                                                                    |
+--------------------------------------------------------------------+
|                                                [ Cancel ] [ OK ]   |
+--------------------------------------------------------------------+
```

5\. Implementation Notes for the Dialog Component

Create a new `ParticipantEditorDialog.razor`. It should utilize the same `.file-dialog__backdrop` and `.file-dialog__window` CSS classes you established for your `FileDialog` to ensure styling consistency.

When the user clicks **\[ OK \]**, the component should calculate the difference between the starting state and the new state:

-   Call `DdsBridge.RemoveParticipant(index)` for rows that were deleted.-   Call `DdsBridge.AddParticipant(domainId, partitionName)` for new rows.-   Call `EventBroker.Publish(new ParticipantsChangedEvent())` so the rest of the application (like the `TopicExplorerPanel` and the `MainLayout` indicator) triggers a UI refresh.

next request: \[TODO\] headless recorder/replay ddsmon to run in headless mode

1.  Recording the traffic to a json file according to given filter expression.
2.  Replay the traffic from given json file according to given filter expression including the time range (or sample ordinal range) The filter must support string comparions "StartsWith" applicable to topic name

new CLI Options:

1.  replay rate (float; 1=real time, 2=2 times realtime etc)
2.  filter exression to be applied to the recorded or replayed data.
    -   On live recording, Filter removes non-matching incoming samples (not allocating ordinal for them)
    -   On replay, filter removes non-matching samples from the input file (not counting them into total frame count)

pls suggest how

To implement headless recording, headless replay, and the new filtering and timing features, you will need to introduce a dedicated `BackgroundService` to orchestrate the headless flow, update your configuration model, adjust the ordinal allocation logic, and modify the pacing in your replay engine.

Here is the step-by-step approach to fulfill all requirements:

1\. Add New CLI Options (`DdsSettings.cs`)

First, update your `DdsSettings` to support the new command-line parameters. ASP.NET Core will automatically bind these from the CLI (e.g., `--DdsSettings:HeadlessMode=Record --DdsSettings:FilterExpression="TopicTypeName.StartsWith('FeatureDemo_')" --DdsSettings:ReplayRate=2.0`).

```
public enum HeadlessMode { None, Record, Replay }

public sealed class DdsSettings 
{
    public const string SectionName = "DdsSettings";
    // Existing properties...

    // New Headless & Replay Options
    public HeadlessMode HeadlessMode { get; set; } = HeadlessMode.None;
    public string HeadlessFilePath { get; set; } = string.Empty;
    public string FilterExpression { get; set; } = string.Empty;
    public float ReplayRate { get; set; } = 1.0f;
}
```

2\. Update Topic Filtering (`FilterCompiler.cs` & `SampleData`)

Because you added `StartsWith` to the `FilterCompiler` in the previous step, and `SampleData` already exposes `TopicTypeName`, the dynamic LINQ compiler will automatically support queries like `TopicTypeName.StartsWith("MyFamily_")`.

To support the time range requirement, users can also use the `IncomingTimestamp` property natively (e.g., `IncomingTimestamp > DateTime(...)`).

3\. Implement Filter-First Ordinal Allocation (Live Recording)

To ensure non-matching samples are completely ignored and do not consume an ordinal, you must evaluate the filter _before_ incrementing the global counter.

Update your `DynamicReader` or `DdsIngestionService`:

```
// 1. Compile the filter at startup
var filterResult = filterCompiler.Compile(settings.FilterExpression, null);
Func<SampleData, bool> predicate = filterResult.IsValid ? filterResult.Predicate! : (s => true);

// 2. When receiving a raw DDS sample
var tempSample = new SampleData 
{
    Ordinal = 0, // Not yet allocated
    IncomingTimestamp = DateTime.UtcNow,
    TopicTypeName = topicName,
    // ... metadata
    Payload = payload
};

// 3. Evaluate filter
if (predicate(tempSample))
{
    // 4. Allocate ordinal ONLY if it passes
    long uniqueOrdinal = Interlocked.Increment(ref _globalOrdinalCounter);
    
    // Use C# record 'with' expression to clone and stamp the final ordinal
    var finalSample = tempSample with { Ordinal = uniqueOrdinal };
    
    _channelWriter.TryWrite(finalSample);
}
```

4\. Implement Replay Rate and Import Filtering (`ReplayEngine.cs`)

The `ReplayEngine` already uses real inter-sample timestamps for pacing. You must divide the delay by the `ReplayRate` and filter the stream immediately after import so skipped frames don't break the timing or frame counts.

Update `tools/DdsMonitor/DdsMonitor.Engine/Replay/ReplayEngine.cs`:

```
public async Task PlayAsync(string filePath, string filterExpression, float replayRate)
{
    var filterResult = _filterCompiler.Compile(filterExpression, null);
    var predicate = filterResult.IsValid ? filterResult.Predicate! : (s => true);

    DateTime? lastSampleTime = null;

    // The ImportService yields one record at a time, preventing OOM exceptions [7]
    await foreach (var sample in _importService.ImportAsync(filePath))
    {
        // Filter removes non-matching samples before they are counted
        if (!predicate(sample)) continue;

        if (lastSampleTime.HasValue)
        {
            TimeSpan realDelay = sample.IncomingTimestamp - lastSampleTime.Value;
            if (realDelay.Ticks > 0)
            {
                // Apply the Replay Rate multiplier
                long scaledTicks = (long)(realDelay.Ticks / replayRate);
                await Task.Delay(TimeSpan.FromTicks(scaledTicks));
            }
        }

        lastSampleTime = sample.IncomingTimestamp;

        // Route to the network via DynamicWriter [8]
        var writer = _bridge.GetWriter(sample.TopicTypeName);
        writer.Write(sample.Payload);
    }
}
```

5\. Create the Headless Orchestrator Service

Create a new `HeadlessRunnerService` that takes over the application lifecycle if headless mode is engaged. This hooks into your safe memory-streaming `Utf8JsonWriter` logic for recording.

```
using Microsoft.Extensions.Hosting;

public class HeadlessRunnerService : BackgroundService
{
    private readonly DdsSettings _settings;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly IReplayEngine _replayEngine;
    private readonly ISampleStore _sampleStore; // Or a direct channel reader
    private readonly IExportService _exportService;

    // Inject dependencies...

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_settings.HeadlessMode == HeadlessMode.None) 
            return; // Let the Blazor UI run normally

        Console.WriteLine($"[Headless] Mode: {_settings.HeadlessMode}");

        try
        {
            if (_settings.HeadlessMode == HeadlessMode.Replay)
            {
                Console.WriteLine($"[Headless] Replaying from {_settings.HeadlessFilePath} at {_settings.ReplayRate}x rate...");
                await _replayEngine.PlayAsync(_settings.HeadlessFilePath, _settings.FilterExpression, _settings.ReplayRate);
            }
            else if (_settings.HeadlessMode == HeadlessMode.Record)
            {
                Console.WriteLine($"[Headless] Recording to {_settings.HeadlessFilePath} with filter: {_settings.FilterExpression}");
                
                // Note: The ingestion service is already filtering using the logic from Step 3.
                // We just need to pipe the SampleStore to the ExportService continually, 
                // or open a direct FileStream and serialize incoming samples.
                await RecordLiveTrafficAsync(_settings.HeadlessFilePath, stoppingToken);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Headless] Error: {ex.Message}");
        }
        finally
        {
            Console.WriteLine("[Headless] Task complete. Shutting down.");
            _lifetime.StopApplication();
        }
    }
    
    private async Task RecordLiveTrafficAsync(string path, CancellationToken token)
    {
        // Implementation mimicking ExportService [9]:
        // Open FileStream, write JSON array bracket, wait for events from ISampleStore or ChannelReader, 
        // serialize each SampleData directly to stream, flush periodically.
        // Wait until cancellation token triggers (Ctrl+C).
    }
}
```

6\. Update `Program.cs`

Ensure the headless service runs, and optionally bypass starting the Blazor web server to save resources.

```
// Inside Program.cs [10]
builder.Services.AddHostedService<HeadlessRunnerService>();

var app = builder.Build();
var settings = app.Services.GetRequiredService<IOptions<DdsSettings>>().Value;

if (settings.HeadlessMode == HeadlessMode.None)
{
    // Normal UI boot
    app.UseStaticFiles();
    app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
    app.Run();
}
else
{
    // Headless boot - starts hosted services but doesn't bind HTTP ports
    await app.RunAsync(); 
}
```

how to allow using 'greater than' and 'lower than' operators in the filter expression on the command line? usual characters '>' and '<' will probably break the command line paring immediately. maybe we should extend the filter expression syntax to allow for alternative operators like 'gt', 'lt', 'ge', 'le', 'eq', 'ne' like "Payload.field1 ge 20"

Because your `FilterCompiler` relies on `System.Linq.Dynamic.Core` to evaluate strings into predicates, **you actually do not need to extend the syntax—it is supported natively out of the box.**

`System.Linq.Dynamic.Core` was designed with URL and command-line friendly OData-style operators specifically to avoid breaking parsers with `<` and `>` characters.

It natively understands the following alphabetical operators:

-   `eq` (==)-   `ne` (!=)-   `gt` (>)-   `ge` (>=)-   `lt` (<)-   `le` (<=)

You can immediately run your command line like this, and the engine will parse it correctly: `ddsmon.exe --DdsSettings:FilterExpression="Payload.field1 ge 20 and Payload.field2 lt 100"`

Optional: Normalizing the Expression for the UI

If you eventually plan to parse the command-line string back into your visual `FilterNode` tree (which uses the `FilterComparisonOperator` enum), you might want to normalize these alphabetical operators back into their standard C# symbol equivalents before processing.

You can do this by adding a simple string replacement pipeline at the beginning of your `Compile` method in `tools/DdsMonitor/DdsMonitor.Engine/FilterCompiler.cs`:

```
public FilterResult Compile(string expression, TopicMetadata? topicMeta)
{
    if (string.IsNullOrWhiteSpace(expression))
        return new FilterResult(true, s => true, null);

    // Normalize alphabetical operators to standard C# symbols for consistency 
    // with your internal FilterComparisonOperator mapping
    string normalizedExpression = expression
        .Replace(" ge ", " >= ", StringComparison.OrdinalIgnoreCase)
        .Replace(" le ", " <= ", StringComparison.OrdinalIgnoreCase)
        .Replace(" gt ", " > ", StringComparison.OrdinalIgnoreCase)
        .Replace(" lt ", " < ", StringComparison.OrdinalIgnoreCase)
        .Replace(" eq ", " == ", StringComparison.OrdinalIgnoreCase)
        .Replace(" ne ", " != ", StringComparison.OrdinalIgnoreCase);

    try
    {
        // Proceed with Dynamic LINQ compilation using the normalized string
        // ...
```

By ensuring there are spaces around the operators (e.g., `" ge "`), you prevent accidentally replacing letters inside actual field names like `Payload.message`.

