#  Debt Tracker




| ID | Priority | Description | Source | Target Batch | Status |
|---|---|---|---|---|---|
| D01 | P3 | `IdlEmitter.EmitStruct()` duplicates `@topic` logic | ME1-BATCH-01 | ME1-BATCH-04 | Closed — superseded by D06 fix (plain `@topic` always emitted; branching removed entirely) |
| D02 | P3 | `SchemaDiscovery` parsing blocks for config attributes are getting long | ME1-BATCH-01 | ME1-BATCH-04 | Fixed — extracted `SetExtensibility`, `PopulateEnumOrFields`, `ResolveTopicName` helpers |
| D03 | P2 | `SerializerEmitter.GetNativeType` etc. repeat EnumBitBound checks | ME1-BATCH-01 | ME1-BATCH-04 | Fixed — extracted `GetEnumCastExpression(int bitBound)` static helper replacing 3 duplicate switch expressions |
| D04 | P3 | `AddParticipant` / `RemoveParticipant` do not hot-wire dynamic DDS readers. Caller must restart. | ME1-BATCH-02 | ME1-BATCH-04 | Fixed — `_auxReadersPerParticipant` list added; `AddParticipant` creates aux readers for all active subscriptions |
| D05 | P2 | `[InlineArray]` union arms bypass discriminator UI metadata rendering. | ME1-BATCH-03 | ME1-BATCH-04 | Fixed (ME1-C02) — union-arm detection moved before InlineArray early-exit in `AppendFields`; `AppendInlineArrayField` now accepts discriminator metadata parameters |
| D06 | P1 | `@topic(name="...")` throws warnings in idlc; must generate as plain `@topic`. | ME1-BATCH-03 | ME1-BATCH-04 | Fixed (ME1-C03) — `EmitStruct` now unconditionally emits plain `@topic` with no `name=` parameter; `idlc` warnings eliminated |
