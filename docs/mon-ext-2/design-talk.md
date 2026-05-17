
\[BUG\] DdsMonitor: too strict component type def in ddsmon-settings.json "ComponentTypeName" contains full assembly id including version like

```
"ComponentTypeName": "DdsMonitor.Components.TopicExplorerPanel, DdsMonitor, Version=0.2.0.0, Culture=neutral, PublicKeyToken=null",
```

It should be just the full namespace & class, exclusding the version and culture etc., to allow for future compatibility, like:

```
"ComponentTypeName": "DdsMonitor.Components.TopicExplorerPanel",
```

To fix this issue and ensure that workspaces saved in `ddsmon.workspace` are forward-compatible, you need to replace all usages of `.AssemblyQualifiedName` with `.FullName` when referring to UI panel components.

Because the Blazor panel components are built into the main application, `Type.GetType()` or the `WindowManager`'s registry can successfully resolve them using just their `FullName` (e.g., `DdsMonitor.Components.TopicExplorerPanel`), omitting the strict versioning that breaks compatibility.

Here are the specific changes required in the source code:

**1\. Update** **WindowManager.cs** The `ResolveComponentTypeName` method enforces how the type names are recorded when panels are spawned. Change it to prefer `FullName` instead of `AssemblyQualifiedName`:

```
private string ResolveComponentTypeName(string componentTypeName)
{
    if (_panelTypes.TryGetValue(componentTypeName, out var registered))
    {
        return registered.FullName ?? componentTypeName;
    }

    var resolved = Type.GetType(componentTypeName);
    return resolved?.FullName ?? componentTypeName;
}
```

**2\. Update UI Components Spawning Panels** Across the Blazor components, the code currently requests panels using `typeof(YourPanel).AssemblyQualifiedName!`. Update all of these to use `.FullName!`:

-   **MainLayout.razor**:-   **Desktop.razor**:-   **TopicExplorerPanel.razor**:-   **SamplesPanel.razor**:-   **InstancesPanel.razor**:-   **ReplayPanel.razor**:-   **DetailPanel.razor**:

_Note: While there are a few other places using_ _AssemblyQualifiedName_ _(such as storing the_ _TopicTypeName_ _inside_ _ComponentState__), it is highly recommended to leave those alone. External dynamically-loaded topic DLLs (plugins) require the assembly identity to be resolved securely by_ _Type.GetType()__, whereas internal UI panels do not._

\[IMPROVEMENT\] More useful schema compiler info message schema compiler now writes "Running CycloneDDS Code Generator (Incremental)..." many times when compiling a larger project. I need it to bring more information, like what part of the project is just being compiled

To improve the info message and show exactly which project is currently being compiled, you need to update the MSBuild target file for the code generator.

The message is defined in **tools/CycloneDDS.CodeGen/CycloneDDS.targets**. You can inject the built-in MSBuild property `$(MSBuildProjectName)` into the `Text` attribute of the `<Message>` task.

Here is the exact change you should make:

**In** **tools/CycloneDDS.CodeGen/CycloneDDS.targets****:**

Locate the `CycloneDDSCodeGen` target (around line 30) and change the `<Message>` line from this:

```
<Message Text="Running CycloneDDS Code Generator (Incremental)..." Importance="high" />
```

To this:

```
<Message Text="Running CycloneDDS Code Generator (Incremental) for $(MSBuildProjectName)..." Importance="high" />
```

This will automatically substitute the name of the project currently triggering the build step (e.g., _Running CycloneDDS Code Generator (Incremental) for FeatureDemo..._), making it much easier to track progress in larger solutions.

can it show the csharp file name it is thrying to compile? I thought the code generator is run per file, not per whole project..

Actually, the code generator runs **per project**, not per individual file.

Here is how the pipeline is structured:

-   **MSBuild Target Invocation**: In `CycloneDDS.targets`, the `CycloneDDSCodeGen` target is executed once for the entire project. It passes the entire project directory (`$(MSBuildProjectDirectory)`) to the generator as the source path.-   **Whole-Project Scanning**: Inside the code generator (`Program.cs`), it takes this source directory and passes it to the `SchemaDiscovery` class. The discovery logic finds all `.cs` files in the directory at once and parses them into a single Roslyn `Compilation`.-   **Why it works this way**: The generator requires the full project context to correctly resolve types across different files, figure out generic arguments, and build a unified `GlobalTypeRegistry` to detect IDL name collisions before generating the C# serialization code.

Because the generator ingests the whole directory in a single execution step, the MSBuild `<Message>` task only fires once per project. Therefore, injecting `$(MSBuildProjectName)` is the most granular and accurate level of reporting available for the build log.

\[IMPROVEMENT\] dds monitor shows unfriendly source time stamp in "Sample info" tab of sample detail Now it looks like

```
"SourceTimestamp: 1773826775412144300"
```

I need it in standard format in LOCAL time zone, similar to

```
"2026-03-18 09:39:35.4147021"
```

to match the display of incoming time stamp

The Timestamp shown in the All Samples window is probably showing UTC time and not local time zone. I need it to show LOCAL time zone.

To fix the timestamp displays to show local time and format the raw nanosecond `SourceTimestamp` into a human-readable standard format, you need to update the `DetailPanel`, `SamplesPanel`, and `InstancesPanel` components.

CycloneDDS `SourceTimestamp` is typically provided in **nanoseconds since the UNIX epoch** (1970-01-01), while `SampleData.Timestamp` is stored as UTC in .NET.

Here are the specific changes to make:

1\. DetailPanel.razor

Update `RenderSampleInfo` to format both the incoming `Timestamp` and the `SourceTimestamp` to the requested `"yyyy-MM-dd HH:mm:ss.fffffff"` format in the local time zone.

In `tools/DdsMonitor/DdsMonitor.Blazor/Components/DetailPanel.razor`, locate the `RenderSampleInfo()` method and update it to this:

```
private RenderFragment RenderSampleInfo() => builder =>
{
    if (_currentSample == null)
    {
        return;
    }

    var info = _currentSample.SampleInfo;

    // Convert UTC Incoming timestamp to Local standard format
    string incomingTimeLocal = _currentSample.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fffffff");
    
    // CycloneDDS source timestamps are nanoseconds since Unix epoch. (1 tick = 100 ns)
    string sourceTimeLocal = (info.SourceTimestamp > 0 && info.SourceTimestamp != long.MaxValue)
        ? DateTime.UnixEpoch.AddTicks(info.SourceTimestamp / 100).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fffffff")
        : "Unknown";

    builder.OpenElement(0, "div");
    builder.AddAttribute(1, "class", "detail-info");
    builder.AddContent(2, $"Global Ordinal: {_currentSample.Ordinal}");
    builder.AddMarkupContent(3, "<br />");
    builder.AddContent(4, $"Incoming Timestamp: {incomingTimeLocal}");
    builder.AddMarkupContent(5, "<br />");
    builder.AddContent(6, $"Domain ID: {_currentSample.DomainId}");
    builder.AddMarkupContent(7, "<br />");
    builder.AddContent(8, $"Partition: {(string.IsNullOrEmpty(_currentSample.PartitionName) ? "<default>" : _currentSample.PartitionName)}");
    builder.AddMarkupContent(9, "<br />");
    builder.AddContent(10, $"Instance: {info.InstanceState}");
    builder.AddMarkupContent(11, "<br />");
    builder.AddContent(12, $"Sample: {info.SampleState}");
    builder.AddMarkupContent(13, "<br />");
    builder.AddContent(14, $"View: {info.ViewState}");
    builder.AddMarkupContent(15, "<br />");
    builder.AddContent(16, $"SourceTimestamp: {sourceTimeLocal}");
    builder.CloseElement();
};
```

2\. SamplesPanel.razor

Update the `All Samples` list and Grid views to format the timestamp into local time.

In `tools/DdsMonitor/DdsMonitor.Blazor/Components/SamplesPanel.razor`, locate the `RenderCellValue` method and update the `ColumnKind.Timestamp` case to call `.ToLocalTime()`:

        ```
        case ColumnKind.Timestamp:
            builder.AddContent(6, row.Sample.Timestamp.ToLocalTime().ToString("HH:mm:ss.fff"));
            break;
```

A few lines up in the same file (inside the `<ItemContent Context="row">` template), update the expanded card view's timestamp:

```
        <span class="samples-panel__cell samples-panel__col-timestamp">@row.Sample.Timestamp.ToLocalTime().ToString("HH:mm:ss.fff")</span>
```

3\. InstancesPanel.razor

Finally, ensure the `Instances` window is also using local time.

In `tools/DdsMonitor/DdsMonitor.Blazor/Components/InstancesPanel.razor`, locate the `RenderCellValue` method and update the `ColumnKind.Time` case:

```
        case ColumnKind.Time:
            builder.AddContent(3, row.Row.Sample.Timestamp.ToLocalTime().ToString("HH:mm:ss.fff"));
            break;
```

\[BUG\] union list item shown in sample detail's "Table" tab can't be exanded to see the union content if the sample contains a list of uniton, the list shows just the union type names as its items (OK) but does not allow expanding them to see their inner content (discriminator and the content of corresponding union arm). Only the active union arm needs to be shown, similarly as the union is displayed in the "Tree" tab.

\[IMPROVEMENT\] The union field is now displayed as the union type name string which can be expanded to show the discriminator and the active union arm. Instead of the uniton type name (which is useless, and inconsistent as we do not show types for other fields) we cuold show the union discriminator field directly. And when expanded, only the content of the active union arsm should be shown.

To resolve both the issue with union list items in the "Table" tab and the display improvements for unions in the "Tree" tab, we need to update the rendering logic in `DetailPanel.razor`.

By extracting a reusable `GetUnionInfo` method, we can determine the active arm and discriminator for any union on the fly. This allows us to hide the inactive union arms in both tabs and use the discriminator value itself as the row value instead of the verbose union type name.

Here is the step-by-step fix for **tools/DdsMonitor/DdsMonitor.Blazor/Components/DetailPanel.razor**:

1\. Add Union helper methods

Add these helpers at the bottom of your `@code` block (just before `private static bool IsLeafType...`) to extract the discriminator and active arm:

```
private (object? Discriminator, MemberInfo? ActiveArm, object? ArmValue) GetUnionInfo(object unionObj)
{
    var type = unionObj.GetType();
    MemberInfo? discMember = null;
    foreach (var m in GetMembers(type))
    {
        if (m.GetCustomAttribute<DdsDiscriminatorAttribute>() != null)
        {
            discMember = m;
            break;
        }
    }

    object? discValue = discMember != null ? GetMemberValue(discMember, unionObj) : null;
    bool anyExplicitMatch = false;
    MemberInfo? activeArm = null;
    
    foreach (var m in GetMembers(type))
    {
        if (m.GetCustomAttribute<DdsDiscriminatorAttribute>() != null) continue;
    
        var caseAttr = m.GetCustomAttribute<DdsCaseAttribute>();
        if (caseAttr != null && UnionValuesEqualTree(caseAttr.Value, discValue))
        {
            activeArm = m;
            anyExplicitMatch = true;
            break;
        }
    }
    
    if (!anyExplicitMatch)
    {
        foreach (var m in GetMembers(type))
        {
            if (m.GetCustomAttribute<DdsDefaultCaseAttribute>() != null)
            {
                activeArm = m;
                break;
            }
        }
    }
    
    object? armValue = activeArm != null ? GetMemberValue(activeArm, unionObj) : null;
    return (discValue, activeArm, armValue);
}

private bool IsUnionArmVisible(FieldMetadata field, object payload)
{
    if (field.IsDiscriminatorField) return true;
    if (field.DependentDiscriminatorPath == null) return true;

    var discField = _currentSample!.TopicMetadata.AllFields.FirstOrDefault(
        f => string.Equals(f.StructuredName, field.DependentDiscriminatorPath, StringComparison.Ordinal));
    if (discField == null) return true;
    
    var currentDisc = discField.Getter(payload);
    
    if (field.IsDefaultUnionCase)
    {
        return !_currentSample.TopicMetadata.AllFields.Any(f =>
            string.Equals(f.DependentDiscriminatorPath, field.DependentDiscriminatorPath, StringComparison.Ordinal) &&
            !f.IsDefaultUnionCase &&
            f.ActiveWhenDiscriminatorValue != null &&
            UnionValuesEqualTree(f.ActiveWhenDiscriminatorValue, currentDisc));
    }
    
    return field.ActiveWhenDiscriminatorValue != null &&
        UnionValuesEqualTree(field.ActiveWhenDiscriminatorValue, currentDisc);
}
```

2\. Update Table View `RenderTableView`

Update the `foreach` loop to filter out inactive arms from the top-level table, and add an expand toggle inside the nested list element rendering to let users expand union list items:

```
private RenderFragment RenderTableView() => builder =>
{
    if (_currentSample == null)
    {
        return;
    }

    builder.OpenElement(0, "table");
    builder.AddAttribute(1, "class", "detail-table");
    builder.OpenElement(2, "thead");
    builder.OpenElement(3, "tr");
    builder.OpenElement(4, "th");
    builder.AddContent(5, "Field");
    builder.CloseElement();
    builder.OpenElement(6, "th");
    builder.AddContent(7, "Value");
    builder.CloseElement();
    builder.CloseElement();
    builder.CloseElement();
    
    builder.OpenElement(8, "tbody");
    foreach (var field in _currentSample.TopicMetadata.AllFields.Where(meta => !meta.IsSynthetic && IsUnionArmVisible(meta, _currentSample.Payload)))
    {
        var value = field.Getter(_currentSample.Payload);
        var isExpandable = IsTableFieldExpandable(value);
        var fieldName = field.StructuredName;
        var isExpanded = isExpandable && _tableExpandedFields.Contains(fieldName);
    
        // ── Main field row ────────────────────────────────────────────────
        builder.OpenElement(9, "tr");
    
        builder.OpenElement(10, "td");
        if (isExpandable)
        {
            builder.OpenElement(11, "button");
            builder.AddAttribute(12, "type", "button");
            builder.AddAttribute(13, "class", "detail-table__toggle");
            builder.AddAttribute(14, "onclick",
                EventCallback.Factory.Create(this, () => ToggleTableField(fieldName)));
            builder.AddContent(15, isExpanded ? "\u25bc" : "\u25ba");
            builder.CloseElement();
        }
        builder.AddContent(16, field.StructuredName);
        builder.CloseElement();
    
        builder.OpenElement(17, "td");
        builder.AddContent(18, RenderValue(value));
        builder.CloseElement();
    
        builder.CloseElement(); // tr
    
        // ── Expanded element rows ─────────────────────────────────────────
        if (isExpanded && value != null)
        {
            var arr = ToElementArray(value);
            if (arr != null)
            {
                for (int i = 0; i < arr.Length; i++)
                {
                    var elemVal = arr.GetValue(i);
                    var elemName = $"{fieldName}[{i}]";
                    var isElemUnion = elemVal != null && elemVal.GetType().GetCustomAttribute<DdsUnionAttribute>() != null;
                    var isElemExpanded = isElemUnion && _tableExpandedFields.Contains(elemName);
    
                    builder.OpenElement(19, "tr");
                    builder.AddAttribute(20, "class", "detail-table__element-row");
                    builder.OpenElement(21, "td");
                    builder.AddAttribute(22, "class", "detail-table__element-name");
                    
                    if (isElemUnion)
                    {
                        builder.OpenElement(11, "button");
                        builder.AddAttribute(12, "type", "button");
                        builder.AddAttribute(13, "class", "detail-table__toggle");
                        builder.AddAttribute(14, "onclick",
                            EventCallback.Factory.Create(this, () => ToggleTableField(elemName)));
                        builder.AddContent(15, isElemExpanded ? "\u25bc" : "\u25ba");
                        builder.CloseElement();
                    }
                    builder.AddContent(23, $"[{i}]");
                    builder.CloseElement();
                    
                    builder.OpenElement(24, "td");
                    if (isElemUnion)
                    {
                        var uInfo = GetUnionInfo(elemVal!);
                        builder.AddContent(25, RenderValue(uInfo.Discriminator));
                    }
                    else
                    {
                        builder.AddContent(25, RenderValue(elemVal));
                    }
                    builder.CloseElement();
                    builder.CloseElement();
                    
                    if (isElemExpanded)
                    {
                        var uInfo = GetUnionInfo(elemVal!);
                        if (uInfo.ActiveArm != null)
                        {
                            builder.OpenElement(30, "tr");
                            builder.AddAttribute(31, "class", "detail-table__element-row");
                            builder.OpenElement(32, "td");
                            builder.AddAttribute(33, "class", "detail-table__element-name");
                            builder.AddAttribute(34, "style", "padding-left: 44px !important;");
                            builder.AddContent(35, uInfo.ActiveArm.Name);
                            builder.CloseElement();
                            builder.OpenElement(36, "td");
                            builder.AddContent(37, RenderValue(uInfo.ArmValue));
                            builder.CloseElement();
                            builder.CloseElement();
                        }
                    }
                }
            }
        }
    }
    builder.CloseElement();
    builder.CloseElement();
};
```

3\. Update Tree View `RenderNode`

Update `RenderNode` to show union fields using their discriminator values directly as the display string, and expand exactly the active arm natively without recreating the discriminator field below it:

```
private RenderFragment RenderNode(object? value, string label, string path, HashSet<object> traversal, int depth) => builder =>
{
    var sequence = 0;
    var type = value?.GetType();
    var isReferenceType = type != null && !type.IsValueType;
    var isCycle = isReferenceType && traversal.Contains(value!);
    var isMaxDepth = depth >= MaxTreeDepth;
    var isLeaf = type == null || IsLeafType(type) || isCycle || isMaxDepth;
    var isUnion = type != null && type.GetCustomAttribute<DdsUnionAttribute>() != null;

    var isRoot = string.IsNullOrEmpty(path) && _currentSample != null &&
        string.Equals(label, _currentSample.TopicMetadata.ShortName, StringComparison.Ordinal);
    var nextPath = isRoot
        ? string.Empty
        : (string.IsNullOrEmpty(path) ? label : $"{path}.{label}");
    var displayValue = value;
    var displayType = type;
    
    if (isCycle)
    {
        displayValue = "(cycle)";
        displayType = typeof(string);
    }
    else if (isMaxDepth)
    {
        displayValue = "(max depth)";
        displayType = typeof(string);
    }
    else if (isUnion && value != null)
    {
        var uInfo = GetUnionInfo(value);
        displayValue = uInfo.Discriminator;
        displayType = uInfo.Discriminator?.GetType() ?? typeof(string);
    }
    
    var isArrayLike = !isLeaf && type != null &&
        (type.IsArray ||
         IsFixedBufferValueType(type) ||
         IsInlineArrayValueType(type) ||
         (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)));
    
    var isToggleable = !isLeaf && !isRoot;
    var isExpanded = !isToggleable || IsTreeNodeExpanded(nextPath, isArrayLike);
    
    builder.OpenElement(sequence++, "div");
    builder.AddAttribute(sequence++, "class", "detail-tree__node");
    builder.OpenElement(sequence++, "div");
    builder.AddAttribute(sequence++, "class", "detail-tree__row");
    
    if (isToggleable)
    {
        var togglePath = nextPath;
        var toggleIsArray = isArrayLike;
        builder.OpenElement(sequence++, "button");
        builder.AddAttribute(sequence++, "type", "button");
        builder.AddAttribute(sequence++, "class", "detail-tree__toggle");
        builder.AddAttribute(sequence++, "onclick",
            EventCallback.Factory.Create(this, () => ToggleTreeNode(togglePath, toggleIsArray)));
        builder.AddContent(sequence++, isExpanded ? "\u25bc" : "\u25ba");
        builder.CloseElement();
    }
    
    builder.OpenElement(sequence++, "span");
    builder.AddAttribute(sequence++, "class", "detail-tree__label");
    builder.AddContent(sequence++, label);
    builder.CloseElement();
    
    builder.OpenElement(sequence++, "span");
    builder.AddAttribute(sequence++, "class", GetValueClass(displayType));
    builder.AddContent(sequence++, RenderValue(displayValue));
    builder.CloseElement();
    
    if (isLeaf)
    {
        RenderPin(builder, ref sequence, nextPath);
        RenderTextViewButton(builder, ref sequence, value);
    }
    
    builder.CloseElement();
    
    var addedToTraversal = false;
    if (!isLeaf && isReferenceType && value != null)
    {
        addedToTraversal = traversal.Add(value);
    }
    
    if (!isLeaf && value != null && isExpanded)
    {
        var nonNullType = type!;
        builder.OpenElement(sequence++, "div");
        builder.AddAttribute(sequence++, "class", "detail-tree__children");
    
        if (nonNullType.IsArray)
        {
            var arr = (Array)value;
            for (int i = 0; i < arr.Length; i++)
            {
                var idx = i;
                builder.AddContent(sequence++, RenderNode(arr.GetValue(idx), $"[{idx}]", nextPath, traversal, depth + 1));
            }
        }
        else if (IsFixedBufferValueType(nonNullType))
        {
            var elements = ReadFixedBufferElements(value, nonNullType);
            for (int i = 0; i < elements.Length; i++)
            {
                var idx = i;
                builder.AddContent(sequence++, RenderNode(elements.GetValue(idx), $"[{idx}]", nextPath, traversal, depth + 1));
            }
        }
        else if (IsInlineArrayValueType(nonNullType))
        {
            var elements = ReadInlineArrayElements(value, nonNullType);
            for (int i = 0; i < elements.Length; i++)
            {
                var idx = i;
                builder.AddContent(sequence++, RenderNode(elements.GetValue(idx), $"[{idx}]", nextPath, traversal, depth + 1));
            }
        }
        else if (nonNullType.IsGenericType &&
                 nonNullType.GetGenericTypeDefinition() == typeof(List<>))
        {
            var list = (System.Collections.IList)value;
            for (int i = 0; i < list.Count; i++)
            {
                var idx = i;
                builder.AddContent(sequence++, RenderNode(list[idx], $"[{idx}]", nextPath, traversal, depth + 1));
            }
        }
        else
        {
            if (isUnion)
            {
                var uInfo = GetUnionInfo(value);
                if (uInfo.ActiveArm != null)
                {
                    builder.AddContent(sequence++, RenderNode(uInfo.ArmValue, uInfo.ActiveArm.Name, nextPath, traversal, depth + 1));
                }
            }
            else
            {
                foreach (var member in GetMembers(nonNullType))
                {
                    var memberValue = GetMemberValue(member, value);
                    builder.AddContent(sequence++, RenderNode(memberValue, member.Name, nextPath, traversal, depth + 1));
                }
            }
        }
    
        builder.CloseElement();
    }
    
    if (addedToTraversal)
    {
        traversal.Remove(value!);
    }
    
    builder.CloseElement();
};
```

\[BUG\] in sample details, null string is shown as empty string - unrecognizable from it null string should be should in a special way, different from usual string - maybe as "null" but in different color than usual strings are so we know it is not a 4-character string but actually a null value.

\[IMPROVEMENT\] dds mon enum field values should be shown in different color than numeric field values and different than string values and different for struct headers to the field data type is recignizable at first glance and not accidentally visually mistaken with string representation for example

To resolve both the issue with `null` values being invisible and the missing syntax highlighting for enums and other scalar types in the value views, you need to update the `RenderValue` method in **DetailPanel.razor**.

Currently, the `RenderValue` method defaults to emitting an empty string with the base `detail-tree__value` CSS class if a value doesn't match specific reference types, completely skipping the type-specific CSS classes that already exist in your stylesheets (`is-null`, `is-enum`, `is-number`, etc.).

Here is the exact replacement for the `RenderValue` method.

Update `DetailPanel.razor`

In **tools/DdsMonitor/DdsMonitor.Blazor/Components/DetailPanel.razor**, locate the `private RenderFragment RenderValue(object? value) => builder =>` method (around line 708 in your source) and replace the entire method with this updated version:

```
private RenderFragment RenderValue(object? value) => builder =>
{
    if (value == null)
    {
        builder.OpenElement(0, "span");
        builder.AddAttribute(1, "class", GetValueClass(null));
        builder.AddContent(2, "null");
        builder.CloseElement();
        return;
    }

    if (value is string text)
    {
        builder.OpenElement(0, "span");
        builder.AddAttribute(1, "class", GetValueClass(typeof(string)));
        builder.AddAttribute(2, "onmouseenter",
            EventCallback.Factory.Create<MouseEventArgs>(this, args => ShowJsonTooltip(text, args)));
        builder.AddAttribute(3, "onmouseleave",
            EventCallback.Factory.Create(this, HideTooltip));
        builder.AddAttribute(4, "onmousedown",
            EventCallback.Factory.Create<MouseEventArgs>(this, args => HandleValueMouseDown(text, true, args)));
        builder.AddAttribute(5, "oncontextmenu:preventDefault", true);
        builder.AddContent(6, text);
        builder.CloseElement();
    }
    else if (IsFixedStringType(value.GetType()))
    {
        var fsText = value.ToString() ?? string.Empty;
        builder.OpenElement(0, "span");
        builder.AddAttribute(1, "class", GetValueClass(value.GetType()));
        builder.AddAttribute(2, "onmousedown",
            EventCallback.Factory.Create<MouseEventArgs>(this, args => HandleValueMouseDown(fsText, false, args)));
        builder.AddAttribute(3, "oncontextmenu:preventDefault", true);
        builder.AddContent(4, fsText);
        builder.CloseElement();
    }
    else if (value is Array arr)
    {
        builder.OpenElement(6, "span");
        builder.AddAttribute(7, "class", "detail-tree__value detail-tree__value--array");
        builder.AddContent(8, $"[{arr.Length}]");
        builder.CloseElement();
    }
    else if (value.GetType().IsGenericType &&
             value.GetType().GetGenericTypeDefinition() == typeof(List<>))
    {
        var list = (System.Collections.IList)value;
        builder.OpenElement(6, "span");
        builder.AddAttribute(7, "class", "detail-tree__value detail-tree__value--array");
        builder.AddContent(8, $"[{list.Count}]");
        builder.CloseElement();
    }
    else if (IsFixedBufferValueType(value.GetType()))
    {
        int byteSize = Marshal.SizeOf(value.GetType());
        var elemField = value.GetType().GetField("FixedElementField",
            BindingFlags.Public | BindingFlags.Instance);
        int elemSize = elemField != null ? Marshal.SizeOf(elemField.FieldType) : 1;
        builder.OpenElement(6, "span");
        builder.AddAttribute(7, "class", "detail-tree__value detail-tree__value--array");
        builder.AddContent(8, $"[{byteSize / elemSize}]");
        builder.CloseElement();
    }
    else
    {
        // Format boolean types to lowercase "true"/"false" for standard display, 
        // fallback to standard ToString() for everything else.
        var content = value is bool b ? (b ? "true" : "false") : (value.ToString() ?? string.Empty);
        
        // Dynamically extract the correct CSS class for Enums, Primitives, etc.
        var cssClass = GetValueClass(value.GetType());
    
        builder.OpenElement(6, "span");
        builder.AddAttribute(7, "class", cssClass);
        builder.AddAttribute(8, "onmousedown",
            EventCallback.Factory.Create<MouseEventArgs>(this, args => HandleValueMouseDown(content, false, args)));
        builder.AddAttribute(9, "oncontextmenu:preventDefault", true);
        builder.AddContent(10, content);
        builder.CloseElement();
    }
};
```

Why this works:

-   **The** **null** **Check**: Added an explicit check at the top. If `value == null`, it uses the `GetValueClass(null)` helper (which returns `detail-tree__value is-null`), mapping perfectly to your existing `#f28ba8` CSS rule, and renders the literal text `"null"`.-   **The Type Highlighting**: Replaced the hardcoded `"detail-tree__value"` strings inside the rendering block with `GetValueClass(value.GetType())`. Because `GetValueClass` evaluates `.IsEnum` and `.IsPrimitive`, it now properly injects the `is-enum` and `is-number` CSS classes you've already defined into the Table Tab and the Tree leaf values.

\[IMPROVEMENT\] In the All Smaples window I need to be able to filter OUT some message types (usually the most frequent ones flooding the sample panel).

1.  this must be doable via the existing filter mechanism, by adding condition for topic type name
    -   in the filter panel i do not see a way how to filter by something else but the payload fields
    -   i am lacking some way of selecting the non-payload fields like topic name, sample state etc.
    -   maybe we can prepend the existing field list items with "Payoad." prefix and add also other list where the non-payload fields can be selected and used in the filter
2.  there should be a right click context menu on the sample allowing to EXTEND already existing filter by removing the clicked message type.

To implement this, we need to expose the non-payload metadata properties (`TopicName` and `InstanceState`) to the filter builder, visually distinguish them in the Field Picker, and add the "Filter Out" context menu option to the sample panels.

Because the underlying `FilterCompiler` uses the `Payload.XXX` syntax to hook into the dynamic expression evaluator, we can safely expose these metadata properties as wrapper fields. They will be written into the filter string as `Payload.Topic` or `Payload.InstanceState` and evaluated correctly against the `SampleData` instance.

Here are the specific changes to make:

1\. Add `Topic` and `InstanceState` to Available Fields

We must declare the non-payload fields as "synthetic wrapper fields" inside the topic metadata.

**File:** `tools/DdsMonitor/DdsMonitor.Engine/Metadata/TopicMetadata.cs` Locate the `AppendSyntheticFields` method (around line 1311) and update it to expose the new fields:

```
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
    
    // ME1: Add getters for Topic and InstanceState
    var topicGetter = new Func<object, object?>(input => ((SampleData)input).TopicMetadata.ShortName);
    var stateGetter = new Func<object, object?>(input => ((SampleData)input).SampleInfo.InstanceState);
    
    // Wrapper fields: top-level SampleData properties exposed as filterable fields.
    allFields.Add(new FieldMetadata("Timestamp", "Timestamp", typeof(DateTime), timestampGetter, SyntheticSetter, isSynthetic: true, isWrapperField: true));
    allFields.Add(new FieldMetadata("Ordinal", "Ordinal", typeof(long), ordinalGetter, SyntheticSetter, isSynthetic: true, isWrapperField: true));
    allFields.Add(new FieldMetadata("Topic", "Topic", typeof(string), topicGetter, SyntheticSetter, isSynthetic: true, isWrapperField: true));
    allFields.Add(new FieldMetadata("InstanceState", "Instance State", typeof(DdsInstanceState), stateGetter, SyntheticSetter, isSynthetic: true, isWrapperField: true));
    
    // Display-only synthetic fields (not filterable via the standard field picker).
    allFields.Add(new FieldMetadata(DelayFieldName, DelayFieldName, typeof(double), delayGetter, SyntheticSetter, isSynthetic: true));
    allFields.Add(new FieldMetadata(SizeFieldName, SizeFieldName, typeof(int), sizeGetter, SyntheticSetter, isSynthetic: true));
}
```

2\. Prefix Field Items in the Field Picker UI

To satisfy your request of distinguishing payload vs. metadata fields, we can inject prefixes dynamically in the dropdown list.

**File:** `tools/DdsMonitor/DdsMonitor.Blazor/Components/FieldPicker.razor` Update the list rendering (around line 744) to prepend `Sample.` or `Payload.`:

```
    <ul class="field-picker__results">
        @for (var i = 0; i < _filteredFields.Count; i++)
        {
            var field = _filteredFields[i];
            var isActive = i == _highlightIndex;
            <li class="field-picker__row @(isActive ? "is-active" : string.Empty)"
                @onclick="() => SelectField(field)">
                <span class="field-picker__name">@(field.IsWrapperField ? "Sample." : "Payload.")@field.DisplayName</span>
                <span class="field-picker__type">@field.ValueType.Name</span>
            </li>
        }
    </ul>
```

**File:** `tools/DdsMonitor/DdsMonitor.Engine/Ui/FieldPickerFilter.cs` Update the `Matches` method so you can type "Payload." or "Sample." into the search box and have it filter correctly:

```
    public static bool Matches(FieldMetadata field, string query)
    {
        if (field == null)
        {
            throw new ArgumentNullException(nameof(field));
        }
    
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }
    
        var fullPath = (field.IsWrapperField ? "Sample." : "Payload.") + field.StructuredName;
    
        return fullPath.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               field.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase);
    }
```

3\. Add Right-Click "Filter Out" to All Samples Panel

Extend the sample context menu to append an exclusion constraint for the clicked topic.

**File:** `tools/DdsMonitor/DdsMonitor.Blazor/Components/SamplesPanel.razor` Add this helper method inside the `@code` block:

```
private void ExcludeTopicFromFilter(string topicName)
{
    var condition = $"Payload.Topic != \"{topicName}\"";
    if (string.IsNullOrWhiteSpace(_filterText))
    {
        _filterText = condition;
    }
    else
    {
        _filterText = $"({_filterText}) AND {condition}";
    }
    ApplyFilter();
    SavePanelState();
    StateHasChanged();
}
```

Then update `OpenRowContextMenu` (around line 898) to include the new menu option:

```
private void OpenRowContextMenu(IndexedSample row, MouseEventArgs args)
{
    var items = new List<ContextMenuItem>
    {
        new("Show Detail (New Window)", null, () =>
        {
            OpenDetail(row.Sample);
            return Task.CompletedTask;
        }),
        new("Clone to Send/Emulator", null, () =>
        {
            EventBroker.Publish(new CloneAndSendRequestEvent(row.Sample.TopicMetadata, row.Sample.Payload));
            return Task.CompletedTask;
        }),
        new("Filter Out Topic", null, () =>
        {
            ExcludeTopicFromFilter(row.Sample.TopicMetadata.ShortName);
            return Task.CompletedTask;
        })
    };

    ContextMenuService.Show(new ContextMenuState(items, args.ClientX, args.ClientY));
}
```

4\. Add Right-Click "Filter Out" to Instances Panel (Optional but Recommended)

To keep the UI consistent, add the same right-click logic to the Instances panel grid.

**File:** `tools/DdsMonitor/DdsMonitor.Blazor/Components/InstancesPanel.razor` Add the helper method in the `@code` block:

```
private void ExcludeTopicFromFilter(string topicName)
{
    var condition = $"Payload.Topic != \"{topicName}\"";
    if (string.IsNullOrWhiteSpace(_filterText))
    {
        _filterText = condition;
    }
    else
    {
        _filterText = $"({_filterText}) AND {condition}";
    }
    ApplyFilter();
    SavePanelState();
    StateHasChanged();
}
```

Then update `OpenRowContextMenu` (around line 817):

```
private void OpenRowContextMenu(IndexedInstance row, MouseEventArgs args)
{
    var items = new List<ContextMenuItem>
    {
        new("Show Instance Samples", null, () =>
        {
            OpenInstanceSamples(row.Row);
            return Task.CompletedTask;
        }),
        new("Show Detail (New Window)", null, () =>
        {
            OpenDetail(row.Row.Sample);
            return Task.CompletedTask;
        }),
        new("Filter Out Topic", null, () =>
        {
            ExcludeTopicFromFilter(row.Row.Sample.TopicMetadata.ShortName);
            return Task.CompletedTask;
        })
    };

    ContextMenuService.Show(new ContextMenuState(items, args.ClientX, args.ClientY));
}
```

\[BUG\] In all samples panel, when trying to sort the samples by clicking on the table column "Ordinal" header, the visual sort indicator changes (arrow up/arrow down) but the samples are always shown in the ascending ordinal order. Showing the last incoming samples first is a crucial feature when monitoring so this should be well supported - it must work in the first place, ans in must be performant (not causing to send all samples from server to client every time a new sample arrives...)

\[IMPROVEMENTS\] Samples autoscroll Also, the All Samples should support autoscroll capability if ascending ordinal order is active. If the selection is on the currently last sample and a new sample arrives, the selection should AUTOMATICALLY change to the most recently receiveid sample, this row should be made visible on the screen (by scrolling the sample list) and if there is a linked sample detail window, it should be updated to reflect the newly selected sample.

To fix both the sorting bug in the "All Samples" panel and properly implement the "track mode" autoscroll capability, you need to update **tools/DdsMonitor/DdsMonitor.Blazor/Components/SamplesPanel.razor**.

There are two primary issues in the current code:

-   **The Bug:** In `EnsureView()`, the `TopicMetadata == null` block (which drives the "All Samples" view) completely omits the `_viewCache.Sort(...)` logic before returning.-   **The Missing Feature:** There is no logic tying the UI's `_trackMode` (the target button on the UI) to the arrival of new samples or the `_virtualizeRef` element scrolling.

Performance Optimization

Calling `_viewCache.Sort(...)` for every incoming sample on 10,000+ items is an `O(N log N)` operation that will bottleneck your UI. Because incoming samples are appended sequentially, if the user is sorting by the synthetic **Ordinal** or **Timestamp** fields, the snapshot is _already_ sorted ascending. We can short-circuit the sort logic: `Reverse()` the list `O(N)` if descending, and do nothing if ascending.

Here are the specific method replacements to apply to **SamplesPanel.razor**:

1\. Update Core View Logic

Replace the `EnsureView()`, `ToggleSort()`, and `ApplyFilter()` methods, and append the two new helpers `ApplySortToViewCache()` and `UpdateSelectionAndTracking()`.

```
private void EnsureView()
{
    // ── Fixed-samples mode (Replay filter panel) ──────────────────────
    var fixedSamples = FixedSamples;
    if (fixedSamples != null)
    {
        if (!_viewDirty) return;

        _viewCache.Clear();
        foreach (var sample in fixedSamples)
        {
            if (_filterPredicate == null || _filterPredicate(sample))
            {
                _viewCache.Add(sample);
            }
        }
    
        ApplySortToViewCache();
    
        _totalCount = fixedSamples.Count;
        _currentCount = _viewCache.Count;
        _viewDirty = false;
        UpdateSelectionAndTracking();
        return;
    }
    
    // ── Live store mode ───────────────────────────────────────────────
    var snapshot = SampleStore.AllSamples;
    if (!_viewDirty && snapshot.Count == _lastAllSamplesCount)
    {
        return;
    }
    
    _lastAllSamplesCount = snapshot.Count;
    _viewCache.Clear();
    
    if (TopicMetadata == null)
    {
        // All-topics mode: include every sample without a topic-type filter.
        foreach (var sample in snapshot)
        {
            if (_filterPredicate == null || _filterPredicate(sample))
            {
                _viewCache.Add(sample);
            }
        }
    
        ApplySortToViewCache();
    
        _totalCount = snapshot.Count;
        _currentCount = _viewCache.Count;
        _viewDirty = false;
        UpdateSelectionAndTracking();
        return;
    }
    
    var topicType = TopicMetadata.TopicType;
    var total = 0;
    
    foreach (var sample in snapshot)
    {
        if (sample.TopicMetadata.TopicType != topicType) continue;
    
        total++;
    
        if (_filterPredicate == null || _filterPredicate(sample))
        {
            _viewCache.Add(sample);
        }
    }
    
    ApplySortToViewCache();
    
    _totalCount = total;
    _currentCount = _viewCache.Count;
    _viewDirty = false;
    UpdateSelectionAndTracking();
}

private void ApplySortToViewCache()
{
    if (_sortField != null)
    {
        // Fast path: Ordinal and Timestamp arrive natively in ascending order.
        if (ReferenceEquals(_sortField, OrdinalField) || ReferenceEquals(_sortField, TimestampField))
        {
            if (_sortDirection == SortDirection.Descending)
            {
                _viewCache.Reverse();
            }
        }
        else
        {
            _viewCache.Sort((left, right) => CompareSamples(left, right, _sortField, _sortDirection));
        }
    }
}

private void UpdateSelectionAndTracking()
{
    if (_viewCache.Count == 0)
    {
        _selectedIndex = -1;
        _selectedSample = null;
        return;
    }

    if (_trackMode)
    {
        int latestIndex = _sortDirection == SortDirection.Ascending ? _viewCache.Count - 1 : 0;
        var row = new IndexedSample(latestIndex, _viewCache[latestIndex]);
        _selectedSample = row.Sample;
        _selectedIndex = row.Index;
    
        _pendingTrackSample = row.Sample;
        _trackDebouncer?.Trigger();
    }
    else if (_selectedSample != null)
    {
        // Check if it's still at the same index
        int expected = _selectedIndex;
        if (expected >= 0 && expected < _viewCache.Count && ReferenceEquals(_viewCache[expected], _selectedSample))
        {
            // Unchanged
        }
        else
        {
            int newIndex = _viewCache.IndexOf(_selectedSample);
            if (newIndex >= 0)
            {
                _selectedIndex = newIndex;
            }
            else
            {
                _selectedIndex = -1;
                _selectedSample = null;
            }
        }
    }
}

private void ToggleSort(FieldMetadata? field)
{
    if (field == null) return;

    if (ReferenceEquals(field, _sortField))
    {
        _sortDirection = _sortDirection == SortDirection.Ascending
            ? SortDirection.Descending
            : SortDirection.Ascending;
    }
    else
    {
        _sortField = field;
        _sortDirection = SortDirection.Ascending;
    }
    
    _viewDirty = true;
    EnsureView();
    _ = _virtualizeRef?.RefreshDataAsync();
    StateHasChanged();
    
    if (_trackMode && _selectedIndex >= 0)
    {
        _ = InvokeAsync(() => EnsureSelectionVisibleAsync(_selectedIndex));
    }
}

private void ApplyFilter()
{
    if (TopicMetadata == null)
    {
        if (string.IsNullOrWhiteSpace(_filterText))
        {
            _filterPredicate = null;
            _viewDirty = true;
            _filterError = null;
            NotifyFilterChanged();
            EnsureView();
            _ = _virtualizeRef?.RefreshDataAsync();
            StateHasChanged();
            if (_trackMode && _selectedIndex >= 0) _ = InvokeAsync(() => EnsureSelectionVisibleAsync(_selectedIndex));
            return;
        }

        var allResult = FilterCompiler.Compile(_filterText, null);
        if (!allResult.IsValid || allResult.Predicate == null)
        {
            _filterError = allResult.ErrorMessage ?? "Invalid filter.";
            return;
        }
    
        _filterError = null;
        _filterPredicate = allResult.Predicate;
        _viewDirty = true;
        NotifyFilterChanged();
        EnsureView();
        _ = _virtualizeRef?.RefreshDataAsync();
        StateHasChanged();
        if (_trackMode && _selectedIndex >= 0) _ = InvokeAsync(() => EnsureSelectionVisibleAsync(_selectedIndex));
        return;
    }
    
    Func<SampleData, bool> topicPredicate = sample => sample.TopicMetadata.TopicType == TopicMetadata.TopicType;
    
    if (string.IsNullOrWhiteSpace(_filterText))
    {
        _filterPredicate = topicPredicate;
        _viewDirty = true;
        _filterError = null;
        NotifyFilterChanged();
        EnsureView();
        _ = _virtualizeRef?.RefreshDataAsync();
        StateHasChanged();
        if (_trackMode && _selectedIndex >= 0) _ = InvokeAsync(() => EnsureSelectionVisibleAsync(_selectedIndex));
        return;
    }
    
    var result = FilterCompiler.Compile(_filterText, TopicMetadata);
    if (!result.IsValid || result.Predicate == null)
    {
        _filterError = result.ErrorMessage ?? "Invalid filter.";
        return;
    }
    
    _filterError = null;
    _filterPredicate = sample => topicPredicate(sample) && result.Predicate(sample);
    _viewDirty = true;
    NotifyFilterChanged();
    EnsureView();
    _ = _virtualizeRef?.RefreshDataAsync();
    StateHasChanged();
    if (_trackMode && _selectedIndex >= 0) _ = InvokeAsync(() => EnsureSelectionVisibleAsync(_selectedIndex));
}
```

2\. Update Tracking & Scroll Interaction

Replace `RefreshCounts`, `ToggleTrackMode`, and `SelectSample` to apply the automatic scrolling behavior and seamlessly turn track mode on/off when users click items manually.

```
private Task RefreshCounts()
{
    var previousTotal = _totalCount;
    var previousCurrent = _currentCount;
    EnsureView();
    var total = _totalCount;
    var current = _currentCount;

    if (total != previousTotal || current != previousCurrent)
    {
        _ = _virtualizeRef?.RefreshDataAsync();
        StateHasChanged();
    
        if (_trackMode && _selectedIndex >= 0)
        {
            // Schedule the JS scroll after the virtualize render pass is complete
            _ = InvokeAsync(() => EnsureSelectionVisibleAsync(_selectedIndex));
        }
    }
    
    return Task.CompletedTask;
}

private void ToggleTrackMode()
{
    _trackMode = !_trackMode;
    if (_trackMode)
    {
        UpdateSelectionAndTracking();
        StateHasChanged();

        if (_selectedIndex >= 0)
        {
            _ = InvokeAsync(() => EnsureSelectionVisibleAsync(_selectedIndex));
        }
    }
}

private void SelectSample(IndexedSample row, bool publishNow)
{
    _selectedSample = row.Sample;
    _selectedIndex = row.Index;

    _ = FocusGrid();
    
    if (publishNow)
    {
        // If the user manually clicks the very first/last sample depending on order, enable tracking naturally.
        bool isLatest = _sortDirection == SortDirection.Ascending 
            ? (row.Index == _viewCache.Count - 1) 
            : (row.Index == 0);
    
        if (_trackMode != isLatest)
        {
            _trackMode = isLatest;
        }
    
        EventBroker.Publish(new SampleSelectedEvent(GetPanelId(), row.Sample));
    }
    else if (_trackMode)
    {
        _pendingTrackSample = row.Sample;
        _trackDebouncer?.Trigger();
    }
}
```

\[BUG\] the red Reset button in main menu deletes all received samples but it also unsubscribes all so i have to click 'Subscribe all' checkbox in the topic. Reset shoudl not forget the active subscriptions!

To fix this issue, we need to modify the `ResetAll` method in the `DdsBridge` class. Currently, this method explicitly disposes of all active readers (which unsubscribes them from the DDS network) before clearing the sample history.

By removing the reader disposal logic, the bridge will keep all your active topic subscriptions alive while simply emptying the visual sample stores and resetting the ordinal counter to zero.

Here is the change you need to make in **tools/DdsMonitor/DdsMonitor.Engine/DdsBridge.cs**:

Locate the `ResetAll()` method (around line 348 based on your provided source structure) and replace the entire method with this updated version:

```
/// <inheritdoc />
public void ResetAll()
{
    lock (_sync)
    {
        ThrowIfDisposed();

        // Note: We deliberately do NOT dispose or clear _activeReaders 
        // or _auxReadersPerParticipant here so that the user's active 
        // topic subscriptions are preserved across resets.
    
        // Reset shared state.
        _ordinalCounter.Reset();
        _sampleStore?.Clear();
        _instanceStore?.Clear();
    }
}
```

What changed:

-   Removed the `foreach` loops that iterate over `_activeReaders.Values` and call `.Dispose()`.-   Removed the `.Clear()` calls for the active and auxiliary reader dictionaries.-   Removed the `ReadersChanged?.Invoke();` event trigger, since the active readers (subscriptions) haven't actually changed. The sample/instance grids will still immediately clear on the screen because `_sampleStore?.Clear()` and `_instanceStore?.Clear()` natively trigger their own view rebuild events.

\[BUG\] In Samples panel the Timestamp field is now shown by default and can not be removed. I would like to make it selectable from the column list as any other rows. If i select now the Timestamp from the column list (it is there already), a new column is added to the table but is always empty! Similarly to the filter, the column available for selection could include also non-payload fields. The only fixed columns always shown could be be just the Ordinal which is necessary as a UI for reversing the sort (latest on the top vs latest at the bottom)

To resolve this issue, we need to completely decouple the synthetic metadata fields (like Timestamp, Topic, Size, and Delay) from the hardcoded UI grid layout and treat them as standard selectable fields in the Column Picker.

The reason the added "Timestamp" column was always empty is because of a type-safety check in `GetFieldValue()`: in the "All Samples" view, it was validating that the field belonged strictly to the payload's `TopicType`, which incorrectly filtered out universal synthetic fields.

Here are the exact changes to apply to **tools/DdsMonitor/DdsMonitor.Blazor/Components/SamplesPanel.razor**:

1\. Simplify the `ColumnKind` Enum

Since all dynamic fields (Payload AND Metadata) will now be handled generically, remove the hardcoded enum values at the bottom of the file (around line 927):

```
private enum ColumnKind
{
    Ordinal,
    Status,
    Field,
    Actions
}
```

2\. Remove Hardcoded Field References

Remove the `TopicField`, `TimestampField`, `_delayField`, and `_sizeField` declarations (around line 877). You only need to keep the `OrdinalField` as it remains the sole fixed anchor:

```
private static readonly FieldMetadata OrdinalField = CreateSyntheticField(
    "Ordinal",
    typeof(long),
    input => ((SampleData)input).Ordinal);

// JSON options for sample tooltips and inline previews...
private static readonly JsonSerializerOptions TooltipJsonOptions = DdsJsonOptions.Display;

private readonly List<SampleData> _viewCache = new();
private Virtualize<IndexedSample>? _virtualizeRef;
// Removed _delayField and _sizeField here
```

3\. Update `InitializeColumns` and `PopulateAllTopicsAvailableColumns`

Update these methods to automatically inject all synthetic fields (except `Ordinal`) into the Available Columns pool, and set up a sensible default selection if the user hasn't saved a custom layout.

```
private void InitializeColumns()
{
    _availableColumns.Clear();
    _selectedColumns.Clear();
    _fieldTopicTypes.Clear();

    if (TopicMetadata == null)
    {
        PopulateAllTopicsAvailableColumns();
    }
    else
    {
        foreach (var field in TopicMetadata.AllFields)
        {
            if (field.StructuredName != "Ordinal")
            {
                _availableColumns.Add(field);
            }
        }
    }
    
    if (_savedSelectedColumnKeys != null && _savedSelectedColumnKeys.Count > 0)
    {
        foreach (var key in _savedSelectedColumnKeys)
        {
            var match = _availableColumns.FirstOrDefault(field =>
                string.Equals(field.StructuredName, key, StringComparison.Ordinal));
            if (match != null)
            {
                _selectedColumns.Add(match);
            }
        }
    }
    
    // Assign standard defaults if no custom layout is saved
    if (_selectedColumns.Count == 0)
    {
        var defaultKeys = new[] { "Topic", "Timestamp", "Size [B]", "Delay [ms]" };
        foreach (var key in defaultKeys)
        {
            var f = _availableColumns.FirstOrDefault(x => string.Equals(x.StructuredName, key, StringComparison.Ordinal));
            if (f != null) _selectedColumns.Add(f);
        }
        
        if (_selectedColumns.Count == 0)
        {
            _selectedColumns.AddRange(_availableColumns.Take(3));
        }
    }
    
    RebuildLayoutColumns();
    SavePanelState();
}

private void PopulateAllTopicsAvailableColumns()
{
    _availableColumns.Clear();
    _fieldTopicTypes.Clear();
    var seen = new HashSet<string>(StringComparer.Ordinal);

    foreach (var topic in TopicRegistry.AllTopics)
    {
        foreach (var field in topic.AllFields)
        {
            // Inject everything except Ordinal (which is fixed)
            if (field.StructuredName != "Ordinal" && seen.Add(field.StructuredName))
            {
                _availableColumns.Add(field);
                if (!field.IsSynthetic)
                {
                    _fieldTopicTypes[field] = topic.TopicType;
                }
            }
        }
    }
}
```

4\. Update `RebuildLayoutColumns`

Remove the hardcoded calls for the metadata fields, dynamically mapping only your `_selectedColumns`:

```
private void RebuildLayoutColumns()
{
    _layoutColumns.Clear();

    if (_savedColumnWeights != null && _savedColumnWeights.Count > 0)
    {
        _columnWeights.Clear();
        foreach (var entry in _savedColumnWeights)
        {
            _columnWeights[entry.Key] = entry.Value;
        }
        _savedColumnWeights = null;
    }
    
    // Hardcoded fixed columns
    AddLayoutColumn("Ordinal", "Ordinal", ColumnKind.Ordinal, OrdinalField, "samples-panel__cell--ordinal", 0.7);
    AddLayoutColumn("Status", "Status", ColumnKind.Status, null, "samples-panel__cell--status", 0.5);
    
    // Dynamic user-selected columns (Payload AND Metadata)
    foreach (var field in _selectedColumns)
    {
        AddLayoutColumn(field.StructuredName, field.DisplayName, ColumnKind.Field, field, string.Empty, 1.0);
    }
    
    AddLayoutColumn("Actions", "Act", ColumnKind.Actions, null, "instances-panel__cell--actions", 0.9);
    
    var keys = new HashSet<string>(_layoutColumns.Select(column => column.Key), StringComparer.Ordinal);
    foreach (var key in _columnWeights.Keys.ToList())
    {
        if (!keys.Contains(key))
        {
            _columnWeights.Remove(key);
        }
    }
}
```

5\. Fix `GetFieldValue` for Synthetic Columns

Bypass the Type validation check for `IsSynthetic` fields so that standard UI data like `Timestamp` can be fetched regardless of the incoming topic type.

```
private object? GetFieldValue(FieldMetadata field, SampleData sample)
{
    var target = field.IsSynthetic ? (object)sample : sample.Payload;
    if (target == null)
    {
        return null;
    }

    // Only skip fields belonging to different topics if they are strictly payload fields
    if (!field.IsSynthetic && _fieldTopicTypes.TryGetValue(field, out var expectedTopicType) &&
        expectedTopicType != sample.TopicMetadata.TopicType)
    {
        return null;
    }
    
    try
    {
        return field.Getter(target);
    }
    catch
    {
        return null;
    }
}
```

6\. Update Renderers

Since `Timestamp` is now rendered generically through `FormatValue`, intercept `DateTime` objects to apply local formatting. Update `FormatValue` and strip down `RenderCellValue`:

```
private RenderFragment RenderCellValue(ColumnLayout column, IndexedSample row) => builder =>
{
    switch (column.Kind)
    {
        case ColumnKind.Ordinal:
            builder.AddContent(0, row.Sample.Ordinal);
            break;
        case ColumnKind.Status:
            builder.OpenElement(1, "span");
            builder.AddAttribute(2, "class", $"sample-status {GetStatusClass(row.Sample)}");
            builder.AddContent(3, GetStatusSymbol(row.Sample));
            builder.CloseElement();
            break;
        case ColumnKind.Field:
            if (column.Field != null)
            {
                var value = GetFieldValue(column.Field, row.Sample);
                builder.AddContent(8, RenderValue(value));
            }
            break;
        case ColumnKind.Actions:
            builder.OpenElement(9, "div");
            builder.AddAttribute(10, "class", "instances-panel__actions");
            // ... (keep the existing Actions button rendering)
            builder.OpenElement(11, "button");
            builder.AddAttribute(12, "type", "button");
            builder.AddAttribute(13, "class", "instances-panel__action");
            builder.AddAttribute(14, "title", "Show detail");
            builder.AddAttribute(15, "onclick", EventCallback.Factory.Create(this, () => OpenDetail(row.Sample)));
            builder.AddAttribute(16, "onmouseenter", EventCallback.Factory.Create<MouseEventArgs>(this, args => ShowDetailTooltip(row.Sample, args)));
            builder.AddAttribute(17, "onmouseleave", EventCallback.Factory.Create(this, HideTooltip));
            builder.AddContent(18, "D");
            builder.CloseElement();
            builder.CloseElement();
            break;
    }
};

private static string FormatValue(object? value)
{
    if (value == null)
    {
        return string.Empty;
    }

    if (value is DateTime dt)
    {
        return dt.ToLocalTime().ToString("HH:mm:ss.fff");
    }
    
    if (value is double number)
    {
        return number.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    }
    
    if (value is float floatValue)
    {
        return floatValue.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    }
    
    return value.ToString() ?? string.Empty;
}
```

\[IMPROVEMENT\] In the All Smaples window I need to be able to filter OUT some message types (usually the most frequent ones flooding the sample panel).

1.  this must be doable via the existing filter mechanism, by adding condition for topic type name
    -   in the filter panel i do not see a way how to filter by something else but the payload fields
    -   i am lacking some way of selecting the non-payload fields like topic name, sample state etc.
    -   maybe we can prepend the existing field list items with "Payoad." prefix and add also other list where the non-payload fields can be selected and used in the filter
2.  there should be a right click context menu on the sample allowing to EXTEND already existing filter by removing the clicked message type.

To implement these improvements, we need to formally distinguish between standard `Payload.` fields and non-payload `Sample.` metadata fields in the filter compiler and UI, expose `Topic` and `InstanceState` to the filter picker, and add the context menu actions.

Here is the step-by-step guide to updating the relevant files:

1\. Update the Filter Compiler Regex

To allow the filter expression evaluator to accept `Sample.Topic` alongside `Payload.Data`, update the regex that extracts field paths.

**File:** `tools/DdsMonitor/DdsMonitor.Engine/FilterCompiler.cs` Update the `PayloadFieldRegex` (around line 1119):

```
private static readonly Regex PayloadFieldRegex = new(
    "\\b(?:Payload|Sample)\\.([A-Za-z_][A-Za-z0-9_]*(?:\\.[A-Za-z_][A-Za-z0-9_]*)*)",
    RegexOptions.Compiled);
```

2\. Expose `Topic` and `InstanceState` fields

We need to register these properties as synthetic filterable fields.

**File:** `tools/DdsMonitor/DdsMonitor.Engine/Metadata/TopicMetadata.cs` Update the `AppendSyntheticFields` method (around line 1315):

```
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
    var stateGetter = new Func<object, object?>(input => ((SampleData)input).SampleInfo.InstanceState);
    
    // Wrapper fields: top-level SampleData properties exposed as filterable fields.
    allFields.Add(new FieldMetadata("Timestamp", "Timestamp", typeof(DateTime), timestampGetter, SyntheticSetter, isSynthetic: true, isWrapperField: true));
    allFields.Add(new FieldMetadata("Ordinal", "Ordinal", typeof(long), ordinalGetter, SyntheticSetter, isSynthetic: true, isWrapperField: true));
    allFields.Add(new FieldMetadata("Topic", "Topic", typeof(string), topicGetter, SyntheticSetter, isSynthetic: true, isWrapperField: true));
    allFields.Add(new FieldMetadata("InstanceState", "Instance State", typeof(DdsInstanceState), stateGetter, SyntheticSetter, isSynthetic: true, isWrapperField: true));
    
    // Display-only synthetic fields (not filterable via the standard field picker).
    allFields.Add(new FieldMetadata(DelayFieldName, DelayFieldName, typeof(double), delayGetter, SyntheticSetter, isSynthetic: true));
    allFields.Add(new FieldMetadata(SizeFieldName, SizeFieldName, typeof(int), sizeGetter, SyntheticSetter, isSynthetic: true));
}
```

3\. Support prefixes in the Field Picker and Filter Builder UIs

Update the UI components so they display and generate the correct `Sample.` or `Payload.` prefixes.

**File:** `tools/DdsMonitor/DdsMonitor.Blazor/Components/FieldPicker.razor` Update the `li` rendering inside `<ul class="field-picker__results">` (around line 746):

```
        @for (var i = 0; i < _filteredFields.Count; i++)
        {
            var field = _filteredFields[i];
            var isActive = i == _highlightIndex;
            <li class="field-picker__row @(isActive ? "is-active" : string.Empty)"
                @onclick="() => SelectField(field)">
                <span class="field-picker__name">@(field.IsWrapperField ? "Sample." : "Payload.")@field.DisplayName</span>
                <span class="field-picker__type">@field.ValueType.Name</span>
            </li>
        }
```

**File:** `tools/DdsMonitor/DdsMonitor.Engine/Ui/FieldPickerFilter.cs` Update `Matches()` (around line 1364):

```
    public static bool Matches(FieldMetadata field, string query)
    {
        if (field == null) throw new ArgumentNullException(nameof(field));
        if (string.IsNullOrWhiteSpace(query)) return true;
    
        var fullPath = (field.IsWrapperField ? "Sample." : "Payload.") + field.StructuredName;
    
        return fullPath.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               field.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase);
    }
```

**File:** `tools/DdsMonitor/DdsMonitor.Blazor/Components/FilterBuilderPanel.razor` Update `ApplyField` and `GetFieldForCondition` (around lines 773-774):

```
private void ApplyField(FilterConditionNode condition, FieldMetadata field)
{
    var prefix = field.IsWrapperField ? "Sample" : "Payload";
    condition.FieldPath = $"{prefix}.{field.StructuredName}";
    condition.ValueTypeName = field.ValueType.AssemblyQualifiedName;

    if (field.ValueType == typeof(bool)) condition.ValueText = "true";
    else if (field.ValueType.IsEnum) condition.ValueText = Enum.GetNames(field.ValueType).FirstOrDefault() ?? string.Empty;
    else condition.ValueText ??= string.Empty;
}

private FieldMetadata? GetFieldForCondition(FilterConditionNode condition)
{
    if (_topicMetadata == null || string.IsNullOrWhiteSpace(condition.FieldPath)) return null;

    string path;
    if (condition.FieldPath.StartsWith("Payload.", StringComparison.Ordinal))
        path = condition.FieldPath.Substring("Payload.".Length);
    else if (condition.FieldPath.StartsWith("Sample.", StringComparison.Ordinal))
        path = condition.FieldPath.Substring("Sample.".Length);
    else
        return null;
    
    return _topicMetadata.AllFields.FirstOrDefault(field =>
        string.Equals(field.StructuredName, path, StringComparison.Ordinal));
}
```

4\. Add the Right-Click Context Menu to Panels

Finally, append the fast "Filter Out Topic" action.

**File:** `tools/DdsMonitor/DdsMonitor.Blazor/Components/SamplesPanel.razor` Add this helper to the `@code` block:

```
private void ExcludeTopicFromFilter(string topicName)
{
    var condition = $"Sample.Topic != \"{topicName}\"";
    if (string.IsNullOrWhiteSpace(_filterText))
    {
        _filterText = condition;
    }
    else
    {
        _filterText = $"({_filterText}) AND {condition}";
    }
    ApplyFilter();
    SavePanelState();
    StateHasChanged();
}
```

And update `OpenRowContextMenu` (around line 900):

```
private void OpenRowContextMenu(IndexedSample row, MouseEventArgs args)
{
    var items = new List<ContextMenuItem>
    {
        new("Show Detail (New Window)", null, () =>
        {
            OpenDetail(row.Sample);
            return Task.CompletedTask;
        }),
        new("Clone to Send/Emulator", null, () =>
        {
            EventBroker.Publish(new CloneAndSendRequestEvent(row.Sample.TopicMetadata, row.Sample.Payload));
            return Task.CompletedTask;
        }),
        new("Filter Out Topic", null, () =>
        {
            ExcludeTopicFromFilter(row.Sample.TopicMetadata.ShortName);
            return Task.CompletedTask;
        })
    };

    ContextMenuService.Show(new ContextMenuState(items, args.ClientX, args.ClientY));
}
```

**File:** `tools/DdsMonitor/DdsMonitor.Blazor/Components/InstancesPanel.razor` Add the same `ExcludeTopicFromFilter(string topicName)` helper method as above to the `@code` block, and update its `OpenRowContextMenu` (around line 819):

```
private void OpenRowContextMenu(IndexedInstance row, MouseEventArgs args)
{
    var items = new List<ContextMenuItem>
    {
        new("Show Instance Samples", null, () =>
        {
            OpenInstanceSamples(row.Row);
            return Task.CompletedTask;
        }),
        new("Show Detail (New Window)", null, () =>
        {
            OpenDetail(row.Row.Sample);
            return Task.CompletedTask;
        }),
        new("Filter Out Topic", null, () =>
        {
            ExcludeTopicFromFilter(row.Row.Sample.TopicMetadata.ShortName);
            return Task.CompletedTask;
        })
    };

    ContextMenuService.Show(new ContextMenuState(items, args.ClientX, args.ClientY));
}
```

\[IMPROVEMENT\] On topics panel, when right clicking on a topic type in the topic table, I want a new "topic properties" window to be opened, showing

-   the actual topic name used by the DDS backend
    
-   topic fields in a flat list table (alpha sorted by field name) with columns
    
    1.  field name (MyStruct.SubStrict.Field1)
    2.  data type (full namespace)
    3.  isKeyField flag
-   byte size of the plain-c data structure
    
-   ?? maybe some other topic properties if available
    

The window is non-modal. It should use standard window index for saving and resoting the state (so if i open the window next time, it shows on the same position) - state persistence should be window index based, not topci type based.

It shoud use modern styling (no densely placed ugly controls).

To implement the new "Topic Properties" window with modern styling, sorted flat fields, and standard window index-based persistence, you will need to create a new Blazor panel component (`TopicPropertiesPanel.razor`), update your `app.css` to accommodate the new summary grid, and wire up the right-click context menu in the `TopicExplorerPanel`.

Here are the exact changes:

1\. Create `TopicPropertiesPanel.razor`

Create a new file **tools/DdsMonitor/DdsMonitor.Blazor/Components/TopicPropertiesPanel.razor** with the following content:

```
@using System.Reflection
@using System.Linq
@using System.Collections.Generic
@using DdsMonitor.Engine
@using CycloneDDS.Schema

@inject ITopicRegistry TopicRegistry

<div class="topic-properties">
    @if (TopicMetadata == null)
    {
        <div class="panel-placeholder-content">
            <h2>Topic Properties</h2>
            <p>No topic selected.</p>
        </div>
    }
    else
    {
        <div class="topic-properties__header">
            <div class="topic-properties__info-grid">
                <span class="topic-properties__label">DDS Topic Name:</span>
                <span class="topic-properties__value is-emphasis">@TopicMetadata.TopicName</span>

                <span class="topic-properties__label">CLR Type:</span>
                <span class="topic-properties__value">@TopicMetadata.TopicType.FullName</span>
    
                <span class="topic-properties__label">Extensibility:</span>
                <span class="topic-properties__value">@GetExtensibility(TopicMetadata.TopicType)</span>
    
                <span class="topic-properties__label">Plain-C Size:</span>
                <span class="topic-properties__value">@(GetPlainCSize(TopicMetadata.TopicType)?.ToString() ?? "Unknown") bytes</span>
            </div>
        </div>
        <div class="topic-properties__content">
            <table class="detail-table">
                <thead>
                    <tr>
                        <th>Field Name</th>
                        <th>Data Type</th>
                        <th style="width: 60px; text-align: center;">Key</th>
                    </tr>
                </thead>
                <tbody>
                    @foreach (var field in GetSortedFields())
                    {
                        <tr>
                            <td>@field.StructuredName</td>
                            <td><span class="topic-properties__type">@GetFriendlyTypeName(field.ValueType)</span></td>
                            <td style="text-align: center;">@(IsKey(field) ? "🔑" : "")</td>
                        </tr>
                    }
                </tbody>
            </table>
        </div>
    }
</div>

@code {
    [CascadingParameter]
    public PanelState? PanelState { get; set; }

    [Parameter]
    public TopicMetadata? TopicMetadata { get; set; }
    
    protected override void OnParametersSet()
    {
        if (PanelState != null && TopicMetadata != null)
        {
            // Persist the type name so the workspace can reload it later
            PanelState.ComponentState[nameof(TopicMetadata)] = TopicMetadata.TopicType.AssemblyQualifiedName ?? string.Empty;
        }
        else if (TopicMetadata == null && PanelState != null)
        {
            // Restore from workspace
            if (PanelState.ComponentState.TryGetValue(nameof(TopicMetadata), out var typeObj))
            {
                var typeName = typeObj is System.Text.Json.JsonElement el ? el.GetString() : typeObj?.ToString();
                if (!string.IsNullOrWhiteSpace(typeName))
                {
                    var type = Type.GetType(typeName);
                    if (type != null)
                    {
                        TopicMetadata = TopicRegistry.GetByType(type);
                    }
                }
            }
        }
    }
    
    private IEnumerable<FieldMetadata> GetSortedFields()
    {
        if (TopicMetadata == null) return Array.Empty<FieldMetadata>();
    
        return TopicMetadata.AllFields
            .Where(f => !f.IsSynthetic)
            .OrderBy(f => f.StructuredName, StringComparer.OrdinalIgnoreCase);
    }
    
    private bool IsKey(FieldMetadata field) => TopicMetadata?.KeyFields.Contains(field) == true;
    
    private uint? GetPlainCSize(Type type)
    {
        var method = type.GetMethod("GetDescriptorSize", BindingFlags.Public | BindingFlags.Static);
        return method != null ? (uint)method.Invoke(null, null)! : null;
    }
    
    private string GetExtensibility(Type type)
    {
        var attr = type.GetCustomAttribute<DdsExtensibilityAttribute>();
        return attr?.Kind.ToString() ?? "Appendable (Default)";
    }
    
    private string GetFriendlyTypeName(Type type)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            return $"List<{type.GetGenericArguments().FullName}>";
        }
        if (type.IsArray)
        {
            return $"{type.GetElementType()?.FullName}[]";
        }
        return type.FullName ?? type.Name;
    }
}
```

2\. Update `TopicExplorerPanel.razor`

Inject the `ContextMenuService`, update the table row to listen for right-clicks, and add the spawn logic.

In **tools/DdsMonitor/DdsMonitor.Blazor/Components/TopicExplorerPanel.razor**:

-   **At the top with the other injects**, add:-   **In the HTML table** **<tbody>**, update the `<tr ...>` to this:-   **In the** **@code** **block**, add these two methods:

3\. Update `app.css`

Append the new CSS rules for the properties panel layout.

In **tools/DdsMonitor/DdsMonitor.Blazor/wwwroot/app.css**, add this to the bottom of the file:

```
/* ─── Topic Properties Panel ─────────────────────────────────────────────────── */

.topic-properties {
    display: flex;
    flex-direction: column;
    height: 100%;
    gap: 12px;
}

.topic-properties__header {
    padding: 12px 16px;
    background: var(--bg-2);
    border: 1px solid var(--panel-border);
    border-radius: 10px;
    flex-shrink: 0;
}

.topic-properties__info-grid {
    display: grid;
    grid-template-columns: auto 1fr;
    gap: 6px 14px;
    font-size: 0.85rem;
    align-items: baseline;
}

.topic-properties__label {
    color: var(--panel-muted);
    font-weight: 600;
}

.topic-properties__value {
    color: var(--panel-text);
}

.topic-properties__value.is-emphasis {
    color: var(--accent-2);
    font-weight: 700;
    font-family: var(--mono-font, monospace);
}

.topic-properties__content {
    flex: 1;
    overflow: auto;
    border: 1px solid var(--panel-border);
    border-radius: 10px;
    background: var(--panel-bg);
}

.topic-properties__type {
    color: var(--panel-muted);
    font-size: 0.82rem;
    font-family: var(--mono-font, monospace);
}
```

\[IMPROVEMENT\] DdsMonitor: sorted topic in topic sources In the Topic Sources panel the table "Topics in selected assembly" - should be alphabetically sorted by topic name - CLR type should show the namespace as the recond row (grayed a bit)

maybe it could have the same context menu opening the "topic properties" as i specified recently

To implement these improvements, we need to update the `TopicSourcesPanel.razor` file. We will inject the necessary services for the context menu and window management, modify the table rendering to include the namespace and right-click handler, and update the topic list to be sorted alphabetically.

Here are the exact changes to apply to **tools/DdsMonitor/DdsMonitor.Blazor/Components/TopicSourcesPanel.razor**:

1\. Add Service Injections

At the top of the file (around line 4), right after `@inject IAssemblySourceService AssemblySourceService`, add the window manager and context menu services:

```
@inject IWindowManager WindowManager
@inject ContextMenuService ContextMenuService
```

2\. Update the Table Rendering

Locate the `<tbody>` block for the topic detail table (around line 140) and replace it with this to include the right-click event handlers and the two-row layout for the CLR Type (reusing the existing `topic-explorer__namespace` CSS class for the grayed-out effect):

```
            <tbody>
                @foreach (var topic in _selectedTopics)
                {
                    <tr @oncontextmenu:preventDefault="true" 
                        @onmousedown="(args) => HandleRowMouseDown(topic, args)">
                        <td>@topic.TopicName</td>
                        <td>
                            <div class="topic-explorer__name">@topic.TopicType.Name</div>
                            <div class="topic-explorer__namespace">@topic.TopicType.Namespace</div>
                        </td>
                        <td>@(topic.IsKeyed ? "✓" : string.Empty)</td>
                    </tr>
                }
            </tbody>
```

3\. Update the C# `@code` block

At the bottom of the file in the `@code` block, apply two changes:

**A. Update** **RefreshSelectedTopics** **to sort alphabetically:** Find the `RefreshSelectedTopics()` method (around line 180) and replace it with:

```
    private void RefreshSelectedTopics()
    {
        _selectedTopics = _selectedIndex >= 0
            ? AssemblySourceService.GetTopicsForEntry(_selectedIndex)
                .OrderBy(t => t.TopicName, StringComparer.OrdinalIgnoreCase)
                .ToList()
            : Array.Empty<TopicMetadata>();
    }
```

**B. Add the Context Menu and Properties Panel logic:** Add these helper methods to the end of the `@code` block (just before `public void Dispose()`):

```
    private void HandleRowMouseDown(TopicMetadata topic, Microsoft.AspNetCore.Components.Web.MouseEventArgs args)
    {
        if (args.Button != 2) return; // Right mouse button
    
        var items = new List<ContextMenuItem>
        {
            new("Topic Properties", null, () =>
            {
                OpenTopicProperties(topic);
                return Task.CompletedTask;
            })
        };
    
        ContextMenuService.Show(new ContextMenuState(items, args.ClientX, args.ClientY));
    }
    
    private void OpenTopicProperties(TopicMetadata metadata)
    {
        var typeName = typeof(TopicPropertiesPanel).FullName!;
        var panels = WindowManager.ActivePanels
            .Where(panel => string.Equals(panel.ComponentTypeName, typeName, StringComparison.Ordinal))
            .ToList();
    
        // Standard window index logic: look for a hidden window first to recycle it
        var panel = panels.OrderBy(p => {
            var dotIndex = p.PanelId.LastIndexOf('.');
            return dotIndex >= 0 && int.TryParse(p.PanelId.Substring(dotIndex + 1), out var idx) ? idx : int.MaxValue;
        }).FirstOrDefault(p => p.IsHidden);
    
        if (panel == null)
        {
            panel = WindowManager.SpawnPanel(typeName);
            panel.Width = 640;
            panel.Height = 480;
        }
    
        panel.IsHidden = false;
        panel.IsMinimized = false;
        panel.ComponentState["TopicMetadata"] = metadata.TopicType.AssemblyQualifiedName ?? string.Empty;
        panel.Title = $"Properties [{metadata.ShortName}]";
        WindowManager.BringToFront(panel.PanelId);
    }
```

\[IMPROVEMENT\] DdsMonitor: full folder scan in topic sources In the Topic Sources panel

-   the path could be a folder path (no concrete dll name)
    
    -   system will scan all potential assemblies (\*.dll and \*.exe etc) automatically
    -   the CLR type in "Topics in selected assembly" will show 3-lines (1. base type name, 2. namespace, 3. assembly file path)
-   the scan will happen every time the dds monitor is started, auto-adapting to possible dll changes since the last run
    

To implement the full folder scanning capability, we need to adapt the `AssemblySourceService` to handle directory paths natively, expose the assembly path on the `TopicMetadata` model, update the UI table to show the 3-line CLR type, and slightly adjust the `FileDialog` component so you can easily select a folder.

Here are the step-by-step changes:

1\. Track Assembly Path in `TopicMetadata`

We need to capture the physical path of the DLL when the topic is discovered so the UI can display it.

**File:** `tools/DdsMonitor/DdsMonitor.Engine/Metadata/TopicMetadata.cs` Add the `AssemblyPath` property and initialize it in the constructor (around line 1289):

```
    /// <summary>
    /// Gets the namespace of the topic type.
    /// </summary>
    public string Namespace { get; }
    
    /// <summary>
    /// Gets the physical path of the assembly containing the topic type.
    /// </summary>
    public string AssemblyPath { get; }
    
    /// <summary>
    /// Gets a value indicating whether the topic is keyed.
    /// </summary>
    public bool IsKeyed { get; }
```

And inside the `public TopicMetadata(Type topicType)` constructor, assign it:

```
        ShortName = topicType.Name;
        Namespace = topicType.Namespace ?? string.Empty;
        AssemblyPath = topicType.Assembly.Location;
    
        var allFields = new List<FieldMetadata>();
```

2\. Update `AssemblySourceService` to Scan Folders

Modify the scanning logic to automatically enumerate `.dll` and `.exe` files if the configured path is a directory. Because this method is called on startup (`LoadAndScanAll`), it inherently satisfies your requirement to auto-adapt to DLL changes on every run.

**File:** `tools/DdsMonitor/DdsMonitor.Engine/AssemblyScanner/AssemblySourceService.cs` Replace the existing `ScanEntry` method (around line 1204) with this updated version:

```
    private List<TopicMetadata> ScanEntry(AssemblySourceEntry entry)
    {
        try
        {
            var found = new List<TopicMetadata>();
    
            if (Directory.Exists(entry.Path))
            {
                var files = Directory.EnumerateFiles(entry.Path, "*.*", SearchOption.TopDirectoryOnly);
                foreach (var file in files)
                {
                    if (file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || 
                        file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            found.AddRange(_discoveryService.DiscoverFromFileDetailed(file));
                        }
                        catch
                        {
                            // Ignore individual file failures when scanning an entire directory
                        }
                    }
                }
            }
            else if (File.Exists(entry.Path))
            {
                found.AddRange(_discoveryService.DiscoverFromFileDetailed(entry.Path));
            }
            else
            {
                throw new FileNotFoundException($"Path not found: {entry.Path}");
            }
    
            entry.TopicCount = found.Count;
            entry.LoadError = null;
            return found;
        }
        catch (Exception ex)
        {
            entry.TopicCount = 0;
            entry.LoadError = ex.Message;
            return new List<TopicMetadata>();
        }
    }
```

3\. Update the UI to Show the 3-Line CLR Type

Update the detail table to include the assembly path.

**File:** `tools/DdsMonitor/DdsMonitor.Blazor/Components/TopicSourcesPanel.razor` Locate the `<tbody>` loop (around line 982 or your recently modified block) and update the CLR Type `<td>` to include the third line. Also, update the "Add" button title.

```
            <!-- Update the Add button title near the top of the file -->
            <button class="topic-sources__btn" title="Add folder or DLL assembly" @onclick="OpenAddDialog">
    
            <!-- ... -->
    
            <tbody>
                @foreach (var topic in _selectedTopics)
                {
                    <tr @oncontextmenu:preventDefault="true" 
                        @onmousedown="(args) => HandleRowMouseDown(topic, args)">
                        <td>@topic.TopicName</td>
                        <td>
                            <div class="topic-explorer__name">@topic.TopicType.Name</div>
                            <div class="topic-explorer__namespace">@topic.TopicType.Namespace</div>
                            <div class="topic-explorer__namespace" title="@topic.AssemblyPath">@topic.AssemblyPath</div>
                        </td>
                        <td>@(topic.IsKeyed ? "✓" : string.Empty)</td>
                    </tr>
                }
            </tbody>
```

4\. Enable Folder Selection in `FileDialog`

Currently, the File Dialog rejects everything unless a specific file is selected. We can relax the `CanConfirm` logic so that if the user clicks "Open" while the input box is empty, it returns the _current directory_ instead.

**File:** `tools/DdsMonitor/DdsMonitor.Blazor/Components/FileDialog.razor`

First, update the `placeholder` on the file input (around line 755) to instruct the user:

```
            <input class="file-dialog__filename-input"
                   type="text"
                   value="@_fileNameInput"
                   @oninput="OnFileNameInput"
                   placeholder="@(Mode == FileDialogMode.Save ? "output.json" : "Select a file or leave empty for current folder…")"
                   spellcheck="false" />
```

Then, in the `@code` block, update `CanConfirm` and `ConfirmAsync` (around line 760):

```
    private bool CanConfirm => Mode == FileDialogMode.Open || !string.IsNullOrWhiteSpace(_fileNameInput);
    
    private void Confirm()
    {
        _ = ConfirmAsync(_fileNameInput);
    }
    
    private async Task ConfirmAsync(string rawPath)
    {
        var path = rawPath.Trim();
        if (string.IsNullOrEmpty(path))
        {
            if (Mode == FileDialogMode.Open)
            {
                path = _currentDir;
            }
            else
            {
                return;
            }
        }
    
        // For relative filename entry in Save mode, combine with current dir.
        if (!Path.IsPathRooted(path))
        {
            path = Path.Combine(_currentDir, path);
        }
    
        // In Open mode, insist the file or directory actually exists.
        if (Mode == FileDialogMode.Open && !File.Exists(path) && !Directory.Exists(path))
        {
            _navError = $"Path not found: {path}";
            return;
        }
    
        // Ensure target directory exists in Save mode.
        if (Mode == FileDialogMode.Save)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                _navError = $"Directory does not exist: {dir}";
                return;
            }
        }
    
        await OnResult.InvokeAsync(path);
    }
```

