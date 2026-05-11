# MON-BATCH-22 Report

**Date:** 2026-03-08  
**Status:** ✅ COMPLETE  
**Tests:** 116 passed, 0 failed (14 new tests added)

---

## Task 1: Checkbox UI Desync & Startup Auto-Subscription ✅

### Problem
Topics were receiving messages but their checkboxes in `TopicExplorerPanel` were unchecked on startup. The "Subscribe All" toggle behaved inconsistently because no topics were subscribed at initialization time, so every `IsSubscribed` evaluation returned `false`.

### Fix: Anchoring Auto-Subscription to Application Startup

The subscription is now anchored in `TopicExplorerPanel.OnInitialized()`. The component is always rendered on startup (it is a persistent fixture of the Desktop layout). The fix:

```csharp
protected override void OnInitialized()
{
    DdsBridge.ReadersChanged += HandleReadersChanged;
    // Auto-subscribe all known topics at startup
    AutoSubscribeAll();
    RefreshTopics();   // Rebuilds _topics with IsSubscribed=true
    StartRefreshTimer();
    StartSparklineTimer();
}

private void AutoSubscribeAll()
{
    foreach (var topic in TopicRegistry.AllTopics)
    {
        // Silently ignore topics without descriptor ops.
        DdsBridge.TrySubscribe(topic, out _, out _);
    }
}
```

Key points:
- `DdsBridge.ReadersChanged` listener is wired **before** subscribing, so any subscription events fired during `AutoSubscribeAll` are captured and processed.
- `RefreshTopics()` is called immediately **after** subscribing, so `_topics` is rebuilt with the updated `IsSubscribed=true` state before the first render. The DOM checkboxes begin in the correct checked state from frame 0.
- The existing `@onchange` + `checked="@topic.IsSubscribed"` Blazor binding is correct — it reflects `IsSubscribed` truthfully once the underlying state is correct. No `@bind-value` override was needed; the binding desync was purely a state desync (topics were not subscribed, hence `IsSubscribed` was legitimately `false`).

---

## Task 2: String `Contains/StartsWith/EndsWith` Expression Compiler Bug ✅

### Problem
Filtering on a string field like `"Message contains abc"` generated the LINQ expression:

```
Payload.Message.Contains("abc")
```

The greedy regex `\bPayload\.([A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*)` captured **`Message.Contains`** as the field path (including the method name), then `PrepareExpression` tried to find `Message.Contains` in `topicMeta.AllFields` and threw:

> `Unknown payload field 'Message.Contains'`

### Fix: Strip String Method Suffixes in Replace Callback

Rather than changing the regex (which would need complex lookaheads to distinguish method calls from nested struct paths), the fix intercepts inside the `Replace` callback. A new static array enumerates known string method suffixes:

```csharp
private static readonly string[] StringMethodSuffixes =
{
    ".Contains",
    ".StartsWith",
    ".EndsWith"
};
```

In `PrepareExpression`, after extracting `fieldPath` from the match, the callback checks for and strips any method suffix, then **re-appends** it to the substituted parameter name in the return value:

```csharp
// Strip .Contains / .StartsWith / .EndsWith that were greedily captured
string? strippedMethodSuffix = null;
foreach (var methodSuffix in StringMethodSuffixes)
{
    if (fieldPath.EndsWith(methodSuffix, StringComparison.Ordinal))
    {
        strippedMethodSuffix = methodSuffix;
        fieldPath = fieldPath.Substring(0, fieldPath.Length - methodSuffix.Length);
        break;
    }
}

// ... resolve field normally using stripped fieldPath ...

return GetPayloadParameterName(fields.IndexOf(field)) + (strippedMethodSuffix ?? string.Empty);
```

**Before:** `Payload.Message.Contains("abc")` → regex capture: `Message.Contains` → error  
**After:**  `Payload.Message.Contains("abc")` → regex capture: `Message.Contains` → stripped to `Message` → resolved to `field0` → returned as `field0.Contains` → Dynamic LINQ sees `field0.Contains("abc")` where `field0` is typed `string` → ✓

---

## Task 3: Timestamp / Synthetic Wrapper Field Crash ✅

### Problem
`Timestamp` and `Ordinal` are properties of `SampleData` (the wrapper object), not of the topic payload. When a filter expression `Payload.Timestamp > ...` was compiled, `PrepareExpression` searched `topicMeta.AllFields` for a field named `Timestamp`. Since no such field existed (only `Delay [ms]` and `Size [B]` were in `AllFields`), it threw:

> `Unknown payload field 'Timestamp'`

### Fix: Register Timestamp & Ordinal as Synthetic Wrapper Fields

Added two new entries to `TopicMetadata.AppendSyntheticFields`, each with `isSynthetic: true, isWrapperField: true`:

```csharp
var timestampGetter = new Func<object, object?>(input => ((SampleData)input).Timestamp);
var ordinalGetter   = new Func<object, object?>(input => ((SampleData)input).Ordinal);

allFields.Add(new FieldMetadata("Timestamp", "Timestamp", typeof(DateTime),
    timestampGetter, SyntheticSetter, isSynthetic: true, isWrapperField: true));
allFields.Add(new FieldMetadata("Ordinal",   "Ordinal",   typeof(long),
    ordinalGetter,   SyntheticSetter, isSynthetic: true, isWrapperField: true));
```

The existing `FilterCompiler.GetFieldValue` already dispatches correctly:

```csharp
var target = field.IsSynthetic ? sample : sample.Payload;
var value  = field.Getter(target!);
```

Since `isSynthetic=true`, `Getter` is invoked on the `SampleData` object → `sample.Timestamp` / `sample.Ordinal`. No additional FilterCompiler changes were needed.

**`FieldMetadata` extended** with a new `IsWrapperField` optional property (`default=false`) to distinguish SampleData wrapper fields (`Timestamp`, `Ordinal`) from display-only synthetic fields (`Delay [ms]`, `Size [B]`). The `FilterBuilderPanel.GetAvailableFields()` was updated to expose wrapper fields in the field picker UI:

```csharp
return _topicMetadata.AllFields
    .Where(field => !field.IsSynthetic || field.IsWrapperField)
    .ToList();
```

---

## Task 4: Subscribe All Graceful Error Suppression ✅

### Problem
Clicking "Subscribe All" called `BuildSubscriptionErrorMessage` for topics that failed to subscribe (commonly self-test or dynamically-discovered topics that lack descriptor ops). The resulting red error banner read:

> `Skipped 2 topic(s) without descriptor ops: SelfTestSimple, SelfTestPose`

These are expected, benign failures (topics without generated serialization descriptors cannot be subscribed; this is normal for test/reflection topics), yet they were surfaced as alarming UI errors.

### Fix: Null-out the Subscription Error in ToggleAllSubscriptions

The `_subscriptionError` assignment in `ToggleAllSubscriptions` was replaced with an explicit `null` reset, effectively silencing the descriptor-ops noise:

```csharp
// Descriptor-missing topics are silently skipped — they are expected to fail
// and the noise should not surface as a red error in the UI.
_subscriptionError = null;
```

The `BuildSubscriptionErrorMessage` helper is retained for the individual `ToggleSubscription` path (single-topic subscriptions still report errors if they fail), keeping meaningful per-topic error feedback intact while removing the noisy batch error.

---

## Files Changed

| File | Change |
|------|--------|
| `tools/DdsMonitor/DdsMonitor.Engine/Metadata/FieldMetadata.cs` | Added `IsWrapperField` constructor parameter and property |
| `tools/DdsMonitor/DdsMonitor.Engine/Metadata/TopicMetadata.cs` | Added `Timestamp` and `Ordinal` synthetic wrapper fields in `AppendSyntheticFields` |
| `tools/DdsMonitor/DdsMonitor.Engine/FilterCompiler.cs` | Added `StringMethodSuffixes` array; updated `PrepareExpression` Replace callback to strip and re-append string method suffixes |
| `tools/DdsMonitor/Components/TopicExplorerPanel.razor` | Added `AutoSubscribeAll()` called from `OnInitialized`; suppressed descriptor errors in `ToggleAllSubscriptions` |
| `tools/DdsMonitor/Components/FilterBuilderPanel.razor` | Updated `GetAvailableFields()` to include wrapper fields |
| `tests/DdsMonitor.Engine.Tests/DdsTestTypes.cs` | Added `[DdsManaged] public partial struct StringTopic` |
| `tests/DdsMonitor.Engine.Tests/Batch22Tests.cs` | 14 new tests covering all 4 tasks |

## Test Results

```
Passed!  - Failed: 0, Passed: 116, Skipped: 0, Total: 116
```

All 14 new Batch22 tests pass. No regressions in the existing 102 tests.
