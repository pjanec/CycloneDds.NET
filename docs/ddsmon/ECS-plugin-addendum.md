### Addendum to Phase 6: Domain Entity Plugins (ECS)

#### 1. Dynamic Configuration via Plugin UI
The original design notes that the `EntityStore` filters events by a configurable namespace prefix (e.g., `company.ECS.*`). To support runtime configuration of this prefix, as well as the new regex patterns, the ECS plugin will expose a custom settings panel.
*   **Settings Panel Registration:** The plugin will implement a new Blazor component (e.g., `EcsSettingsPanel.razor`) and register it during the `Initialize` phase using `context.GetFeature<PluginPanelRegistry>()?.RegisterPanelType(...)` (see [PLA1-DESIGN.md §4](../plugin-api/PLA1-DESIGN.md#4-phase-1--capability-querying-context-future-proof-foundation)). 
*   **Menu Integration:** The plugin will also add a menu item using `context.GetFeature<IMenuRegistry>()?.AddMenuItem(...)` (e.g., path `"Plugins/ECS"`, label `"Settings"`) to spawn this panel.
*   **State Persistence:** The settings (namespace prefix, EntityId regex, PartId regex) should be persisted using the `WorkspacePersistenceService` so they are restored between browser sessions.

#### 2. Key Field Regex Resolution
The original domain specification strictly defined the `EntityId` as **always** being the first `[DdsKey]` field (`Key1`) and the `PartId` as the optional second `[DdsKey]` field (`Key2`). The `EntityStore` aggregation algorithm must be updated to support regex-based extraction:
*   **Regex Matching:** During the "Extract Identity" step of the aggregation algorithm, the plugin will iterate through the `TopicMetadata.KeyFields` collection. It will evaluate the configured regexes against the `FieldMetadata.StructuredName` property (the flattened, dot-separated path of the field) to locate the `EntityId` and `PartId`.

#### 3. Numeric Integer Type Validation
Once the regex successfully matches a key field, the plugin must validate the underlying data type before attempting to extract the `EntityId` or `PartId`.
*   **Type Checking:** The plugin will inspect the `FieldMetadata.ValueType` property. 
*   **Supported Primitives:** The validation must confirm that the `ValueType` corresponds to a standard 32-bit or 64-bit integer, regardless of sign. Specifically, it should allow types such as `sbyte`, `byte`, `short`, `ushort`, `int`, `uint`, `long`, and `ulong`. 
*   **Error Handling:** If the regex matches a field but the `ValueType` is a non-integer type (like a floating-point number, string, or boolean), the `EntityStore` should gracefully reject the topic as an invalid ECS descriptor and exclude it from the aggregated `Entity`.