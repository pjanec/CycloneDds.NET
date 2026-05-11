# FDP Code Standards — Standing Rules

Quick reference. Cite this doc when reviewing or writing batch instructions.

---

## 0. Review Checklist — Test Quality (mandatory in every batch review)

**Passing tests are not sufficient.** During every batch review, explicitly assess the tests before the code.

For each new or modified test ask:

| Question | Common failure |
|---|---|
| Does it assert the specific field/value that matters, or just "no exception"? | `BTreeTick_ProducesChannelWrite` that doesn't assert `channel.ActiveAction` |
| Does it verify the full chain, not just isolated units? | Testing behavior ingress without checking arbitration clears the stale channel on the next frame |
| Does it distinguish "fired" from "fired correctly"? | `SpyExecutor` counting `Execute` calls but never verifying the executor can write back to the channel |
| Does adding a named constant to the production code mean the test assertion should reference it too? | `Assert.True(size <= 96)` after `MaxChannelSizeBytes = 96` exists |
| Is there a negative case for every positive case? | Testing "stale channel is cleared" but not "valid channel is NOT cleared" |
| Does the test catch a realistic regression, or only the implementation as written? | Checking `ActiveAction == 0` after clear but not `BehaviorInstanceId == 0` — a selective-clear regression would slip through |

Flag weak tests as **Issues** in the review (P1 if they miss the core contract, P2 if minor). Weak tests must be corrected in the next batch **before** new feature work begins (they are Corrective Task 0).

---


## 1. No Magic Numbers in Production Code

**Applies to all non-test `.cs` files. No exceptions.**

| Category | Bad | Good |
|---|---|---|
| Fixed buffer sizes | `fixed byte Params[32]` | `fixed byte Params[BehaviorConstants.ActionParamsByteSize]` |
| Size budgets in tests | `Assert.True(size <= 96)` | `Assert.True(size <= BehaviorConstants.MaxChannelSizeBytes)` |
| Lookup table capacity | `new IActionExecutor[64]` | `new IActionExecutor[BehaviorConstants.MaxActionTypes]` |
| Angle literals | `Quaternion.CreateFromYawPitchRoll(0, 0, MathF.PI/2f)` | `SimMath.FacingNorth` |
| Numeric states | `if (channel.ActiveAction != 0)` using raw `0` for "no action" | Use an enum or named `const` |
| Any other literal | `_grid = SpatialHashGrid.Create(150, 150, 5.0f, ...)` | `const int GridWidth = 150; const float CellSizeM = 5.0f;` |

**Rule of thumb:** changing a buffer size, capacity, or threshold should be a **one-line edit** to a named constant. If it requires a grep, there are magic numbers.

**Tests are exempt** for simple assertions of expected numeric outcomes (e.g. `Assert.Equal(28, Unsafe.SizeOf<SimTransform>())`). But when a production constant already exists, tests should reference it.

---

## 2. Coordinate System — use SimMath, never System.Numerics raw

The FDP world is **right-handed, X=east, Y=north, Z=up**.

| Term | Axis | Zero direction |
|---|---|---|
| Yaw | Z | East (+X) |
| Pitch | Y | Horizontal (0 = level) |
| Roll | X | Level (0 = level) |

`System.Numerics.Quaternion.CreateFromYawPitchRoll` uses a **different convention** (yaw around Y). It is **banned in production code**.

**Always use:**
```csharp
SimMath.FromYaw(yawRad)           // ground vehicles
SimMath.FromYawPitchRoll(y, p, r) // full 3D
SimMath.FacingNorth / FacingEast / FacingWest / FacingSouth   // in tests / setup
```

`Quaternion.Identity` == facing east == `SimMath.FacingEast`. Use whichever is clearer.

---

## 3. ECS Mutation — GetComponentRW vs GetComponentRO

| Context | Read | Write |
|---|---|---|
| **Main-thread synchronous system** | `World.GetComponent<T>` or `GetComponentRW<T>` | `ref var c = ref World.GetComponentRW<T>(e);` — **in-place, zero copy** |
| **Async / background module** (operates on read-only snapshot) | `view.GetComponentRO<T>(e)` | `cmd.SetComponent(e, modifiedCopy)` via `IEntityCommandBuffer` |

Background systems **never touch the main world directly** and **never write to the snapshot**. The copy cost through the command buffer (~32–96 bytes) is intentional and negligible. Do not add `GetComponentRW` to `ISimulationView` — it is intentionally absent.

---

## 4. Zero Allocation on Hot Path

- No `new` in `OnUpdate` loops.
- Pre-allocate all arrays, pools, and scratch buffers in `OnCreate`.
- Use `stackalloc` for small temporary spans (neighbour lists, etc.).
- No LINQ in simulation loops (`foreach` over a query is fine; `.Where().Select()` is not).

---

## 5. Component Design Rules

- All ECS components are **unmanaged value types** (`struct`, no reference fields).
- `[StructLayout(LayoutKind.Sequential)]` on every component struct.
- Use `fixed byte` buffers with **named size constants** (see §1).
- Do not register thousands of tiny components — use inline fixed buffers (`Params[32]`) to stay within the 256-component kernel limit.
