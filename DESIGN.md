# Design Decisions — Planning Subsystem

## Status

- Goal 1 (justified streaming): **not started**.
- Goal 2 (picker/slotter extraction): **done** — 11 implementation classes extracted, 10 selectors
  wired, all 449 tests pass.

---

## Goal 1: Justified Streaming

### Current state

All selectors stream results via `IEnumerable<PlannedSlot> Plan()`, but the planner
never injects feedback mid-stream — selectors operate on a frozen snapshot of the queue.
The streaming is structurally unnecessary; `List<PlannedSlot>` would behave identically.

### Target

Make the streamed output **useful**: the planner can reject a placement (e.g. user-defined
rules like "no Math on Saturday"), and the selector adapts on the fly — retrying the
same slot with a different subject — without rebuilding internal state from scratch.

### Protocol sketch

```
Plan(accept: PlannedSlot -> bool) -> IEnumerable<PlannedSlot>
```

Base loop:

```
for each slot:
    excluded = {}
    for each candidate subject (excluding `excluded`):
        placement = build(slot, subject)
        if accept(placement):
            commit state → yield placement → move to next slot
        else:
            excluded.add(subject) → retry
    if no candidate accepted → skip slot
```

### Impact per strategy

| Strategy | Effort | Notes |
|----------|--------|-------|
| LargestQueueFirst, SmallestQueueFirst, Random, WeightedRandom | Trivial | Add exclusion set to PickSubject |
| RoundRobin, EmptyDaySeed, GapClosing | Easy | Defer _position advance to CommitState |
| FairShare | Medium | Defer _placed/_placedTotal update to CommitState |
| LeadingChunkGrowth, TrailingChunkGrowth | Real work | Split TakeSlot into peek + commit (heap dequeue deferred) |

---

## Goal 2: Pluggable Picker & Slotter

### Current state

- `RoundRobinPicker` exists as a standalone class, reused by 4 strategies.
- All other picker logic is inlined in selectors (LargestQueueFirst, FairShare, etc.).
- Slot selection is similarly inlined per strategy (sequential, gap-closing, chunk-growth, etc.).
- Users cannot mix and match pickers and slotters without writing new classes.

### Target

Pickers and slotters become first-class, composable building blocks.

### Implemented interfaces

```csharp
public interface IPicker {
    string? Pick(Dictionary<string, int> queue);
}

public interface ISlotter {
    Slot? NextSlot();  // owns its fillable reference / precomputed enumerator
}
```

Note: no `excluded` parameter yet — that arrives with Goal 1.

### Extracted classes (all in `MyTimetable/Planning/`)

| Picker | Slotter |
|--------|---------|
| `LargestQueueFirstPicker` | `SequentialSlotter` |
| `SmallestQueueFirstPicker` | `GapClosingSlotter` |
| `RoundRobinPicker` (reworked) | `LeadingChunkGrowthSlotter` |
| `FairSharePicker` | `TrailingChunkGrowthSlotter` |
| `RandomPicker` | `EmptyDaySeedSlotter` |
| `WeightedRandomPicker` | |

### Selector wiring

Every selector accepts optional `IPicker?` and/or `ISlotter?` in its constructor.
If not provided, it falls back to the "natural" implementation (e.g.
`LargestQueueFirstSelector` defaults to `LargestQueueFirstPicker`).

- **Picker-only selectors** (default sequential slotter): `LargestQueueFirstSelector`,
  `SmallestQueueFirstSelector`, `RoundRobinSelector`, `FairShareSelector`,
  `RandomSelector`, `WeightedRandomSelector`.
- **Dual selectors** (custom slotter + default round-robin picker): `EmptyDaySeedSelector`,
  `GapClosingSelector`, `LeadingChunkGrowthSelector`, `TrailingChunkGrowthSelector`.

`RoundRobinPicker.Pick()` now takes `Dictionary<string, int>` as a parameter instead
of storing the queue reference internally. This makes the dependency explicit and
allows all pickers to share the same interface shape. |

### Non-decomposable strategies

Some strategies (e.g. entropy-based optimization) cannot separate picking from slotting —
slot choice depends on subject choice and vice versa. These continue to implement the
full selector interface directly. Two code paths is acceptable: decomposable strategies
use picker+slotter composition; non-decomposable ones implement the full contract.

### Why classes, not delegates

In a frozen-snapshot world (no planner feedback), pickers and slotters would be pure
functions — delegates would suffice:

```csharp
delegate string? Picker(IReadOnlyDictionary<string, int> queue);
delegate Slot? Slotter(List<Slot> fillable);
```

With justified streaming (Goal 1), the picker may be asked "try again" for the same
slot after a rejection. This requires mutable internal state scoped to the Plan() call:

- RoundRobin must defer `_position` advance until acceptance.
- FairShare must defer `_placed` update until acceptance.
- Chunk-growth heap must defer dequeue until acceptance.
- The exclusion set for the current slot survives across retries.

Delegates cannot carry this state cleanly — closures capturing mutable state are just
classes by another name and harder to test. Classes are the correct choice.

### Files changed

| Action | File |
|--------|------|
| New | `IPicker.cs`, `ISlotter.cs` |
| New | `LargestQueueFirstPicker.cs`, `SmallestQueueFirstPicker.cs`, `FairSharePicker.cs`, `RandomPicker.cs`, `WeightedRandomPicker.cs` |
| New | `SequentialSlotter.cs`, `GapClosingSlotter.cs`, `LeadingChunkGrowthSlotter.cs`, `TrailingChunkGrowthSlotter.cs`, `EmptyDaySeedSlotter.cs` |
| Rewritten | `RoundRobinPicker.cs` (implements `IPicker`, `Pick()` takes queue param) |
| Rewritten | `LargestQueueFirstSelector.cs`, `SmallestQueueFirstSelector.cs`, `RoundRobinSelector.cs`, `FairShareSelector.cs`, `RandomSelector.cs`, `WeightedRandomSelector.cs`, `EmptyDaySeedSelector.cs`, `GapClosingSelector.cs`, `LeadingChunkGrowthSelector.cs`, `TrailingChunkGrowthSelector.cs` |
| Updated | `RoundRobinPickerTests.cs`, `GapClosingSelectorTests.cs` (was `GapClosingSelector.cs`, renamed) |

### Intent

This is a learning exercise, not a commercial requirement. The goal is to work through
the design trade-offs and see where they lead. Composing strategies from parts should
require zero user-written code.
