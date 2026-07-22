# Planning Subsystem — Architecture & Design

## Architecture overview

```
User input (subjects + counts)
        │
        ▼
   Planner._queue         ← source of truth for "what still needs placement"
        │
        │ Create(snapshot)
        ▼
   IPlanningSelector      ← algorithm: (queue, slots) → proposals
        │
        │ proposes (slot, subject) ──→ Planner.accept(slot, subject) → ProposeResult
        │ ◄── (slotOK, subjectOK) ───
        │
        │ yields accepted PlannedSlot[]
        ▼
   Planner                ← decrements _queue, mutates schedule model
```

The planner drives the protocol: the selector proposes `(slot, subject)` pairs, the planner
responds with two independent booleans — is the slot acceptable, is the subject acceptable.
The selector reacts mechanically to the four possible responses without knowing *why*
something was rejected. Domain rules (e.g. "no lessons on Sunday") live in the planner and
don't leak into strategies.

### Key types

| Type | Role | Location |
|------|------|----------|
| `Planner` | Orchestrator. Owns `_queue`. Evaluates proposals. Calls `ResolveConflicts`, `Plan`. | `MyTimetable/Planner.cs` |
| `ProposeResult` | `(bool SlotOK, bool SubjectOK)` — planner's two-bit response to a proposal. | `Planning/ProposeResult.cs` |
| `IPlanningSelector` | `Plan(Func<Slot, string, ProposeResult> accept)` → `IEnumerable<PlannedSlot>`. | `Planning/IPlanningSelector.cs` |
| `IPlanningSelectorFactory` | Creates selectors from `(queue, fillable)`. | `Planning/IPlanningSelectorFactory.cs` |
| `PlanningSelectorBase` | Abstract base. Copies queue & fillable. Runs `PeekSlot`/`PeekSubject`/`Commit` loop. | `Planning/PlanningSelectorBase.cs` |
| `CompositePlanningSelector` | Chains multiple factories sequentially. Each inner selector gets the remaining queue & slots. | `Planning/CompositePlanningSelector.cs` |
| `DayLayout` | Static helpers: group slots into days, find first/last occupied chunks. | `Planning/DayLayout.cs` |
| `PlanningSelectorFactory` | Adapter: `Func<queue, fillable, IPlanningSelector>` → `IPlanningSelectorFactory`. | `Planning/PlanningSelectorFactory.cs` |

---

## Planner–Selector contract

The selector proposes `(slot, subject)` pairs to the planner, which replies with a
`ProposeResult` — two independent bits: `SlotOK` and `SubjectOK`. The selector reacts
mechanically:

| Planner says | Selector does |
|---|---|
| `(T, T)` | Commit slot & subject. Advance both. |
| `(T, F)` | Slot stays. Try next subject in the same slot. |
| `(F, T)` | Subject stays. Try next slot with the same subject. |
| `(F, F)` | Discard both. Fresh slot, fresh subject. |

The planner is responsible for detecting infinite loops: if the same `(slot, subject)`
pair is proposed twice in the same `Plan()` cycle, the planner escalates it to `(F, F)`
to force the selector into a different path. This keeps duplicate-tracking in one place
rather than duplicated across every selector.

### Flat loop (no nesting)

The selectors use a single flat loop with two boolean flags (`newSlot`, `newSubject`)
instead of nested while-loops. When a component needs a fresh value, its flag is set;
the next iteration re-peeks that component. When both are fresh, a proposal is made.
When accepted, both flags are set to advance fully.

## Copy-based selectors

The base class copies both inputs in its constructor:

```csharp
protected PlanningSelectorBase(Dictionary<string, int> queue, List<Slot> fillable)
{
    Queue = new Dictionary<string, int>(queue);  // snapshot
    Fillable = new List<Slot>(fillable);          // snapshot
}
```

This means:

1. **The selector mutates freely during `Plan()`.** `Queue[title]--` operates on the copy.
2. **The planner independently tracks its own `_queue`.** It decrements it as placements
   are yielded — separate from the selector's copy.
3. **No double-counting.** The selector never touches the planner's live queue.
4. **Isolation for compositing.** `CompositePlanningSelector` gives each inner factory
   a snapshot of what's left after previous factories consumed their share.

The copy approach keeps selectors as pure algorithms and planner-level policy in the planner.

---

## Pluggable picker & slotter

Selectors are decomposed into two concerns, each with an interface and multiple
implementations. Every selector accepts optional `IPickerFactory?` / `ISlotterFactory?`
and defaults to its natural implementation. The selector owns creation of its components
from its own queue/Fillable snapshots — users compose via factories, never pre-constructed
instances.

### Interfaces

```csharp
// Planning/Pickers/IPicker.cs
public interface IPicker {
    string? Peek(Dictionary<string, int> queue);  // propose candidate, no state mutation
    void Commit(string title);                     // confirm placement, advance state
}

// Planning/Slotters/ISlotter.cs
public interface ISlotter {
    Slot? Peek();           // propose next slot, may advance internal iterator
    void Commit(Slot slot); // confirm slot usage
}
```

Pickers and slotters are split into peek/commit:
- **Peek** returns a candidate but must not mutate logical state (position, counts).
- **Commit** is called only when the planner accepts the proposal.
- For enumerator/iterator-based implementations, Peek advances the enumerator
  and Commit is a no-op — the next Peek will naturally return the next element.
- For heap-based implementations, Peek dequeues and optionally re-enqueues a
  follow-up state; Commit is a no-op.
- For list-based implementations (SequentialSlotter), Peek returns the first
  element without removing; Commit removes it.

### Pickers (`Planning/Pickers/`)

| Class | Algorithm | Commit behavior |
|-------|-----------|-----------------|
| `LargestQueueFirstPicker` | `MaxBy` remaining count | No-op (stateless) |
| `SmallestQueueFirstPicker` | `MinBy` remaining count | No-op (stateless) |
| `RoundRobinPicker` | Ordered cycle, skips exhausted | Advances `_position` cursor |
| `FairSharePicker` | Minimizes squared deviation from desired proportions | Increments `_placed` counts |
| `RandomPicker` | Uniform random | No-op |
| `WeightedRandomPicker` | Roulette-wheel proportional to remaining | No-op |

### Slotters (`Planning/Slotters/`)

| Class | Algorithm | Commit behavior |
|-------|-----------|-----------------|
| `SequentialSlotter` | Returns `Fillable[0]` | Removes from Fillable |
| `GapClosingSlotter` | Precomputed gap enumerator, ordered by gap size → date → number | No-op (enumerator advanced in Peek) |
| `LeadingChunkGrowthSlotter` | Priority queue of first chunks, fills leftward toward slot 1 | No-op (heap processed in Peek) |
| `TrailingChunkGrowthSlotter` | Priority queue of last chunks, fills rightward toward slot N | No-op (heap processed in Peek) |
| `EmptyDaySeedSlotter` | One slot per completely empty day (first open slot) | No-op (enumerator advanced in Peek) |

### Selector wiring

**Picker-only** (use default `SequentialSlotter`):
`LargestQueueFirstSelector`, `SmallestQueueFirstSelector`, `RoundRobinSelector`,
`FairShareSelector`, `RandomSelector`, `WeightedRandomSelector`.

**Dual** (custom slotter, default `RoundRobinPicker`):
`EmptyDaySeedSelector`, `GapClosingSelector`, `LeadingChunkGrowthSelector`,
`TrailingChunkGrowthSelector`.

### Factory adapters

Following the same pattern as `PlanningSelectorFactory`:

```csharp
public interface IPickerFactory {
    IPicker Create(Dictionary<string, int> queue);
}
public interface ISlotterFactory {
    ISlotter Create(List<Slot> fillable);
}
```

Adapters: `PickerFactory(Func<queue, IPicker>)` and `SlotterFactory(Func<fillable, ISlotter>)`.

Example of custom composition:

```csharp
new LeadingChunkGrowthSelector(queue, fillable, slotCount: 6,
    pickerFactory: new PickerFactory(q => new FairSharePicker(q)));
```

### Why classes, not delegates

Pickers and slotters are classes rather than delegates because they need to defer
state commits across rejection-retry cycles. Since the planner may reject proposals,
the peek/commit split requires mutable, call-scoped state:

- RoundRobin must not advance `_position` on a rejected pick — only on Commit.
- FairShare must not increment `_placed` on a rejected pick — only on Commit.

For slotters the situation is simpler: most consume state eagerly in Peek
(enumerator or heap dequeue), so Commit is a no-op. Only list-based slotters
defer removal to Commit.

### Non-decomposable strategies

Some strategies cannot separate picking from slotting (e.g. entropy-based
optimization where slot choice depends on subject and vice versa). These
implement `IPlanningSelector` directly. Two code paths is acceptable.

---

## Implemented: Justified streaming

The planner can reject proposals mid-stream and the selector retries without
rebuilding internal state — the `IEnumerable` is now genuinely streaming.

### Protocol

```csharp
IEnumerable<PlannedSlot> Plan(Func<Slot, string, ProposeResult> accept);
```

The `accept` callback gives the planner full control over what gets placed.
The planner evaluates domain rules (e.g. "no lessons on Sunday", "no consecutive
same-subject slots", etc.) and returns two independent booleans. The selector
handles the four response patterns mechanically — it never needs to know
*why* something was rejected.

### Implementation summary

| Strategy | What changed |
|----------|-------------|
| LargestQueueFirst, SmallestQueueFirst, Random, WeightedRandom | Peek = old Pick; Commit = no-op |
| RoundRobin | `_position` advance moved from Peek to Commit |
| FairShare | `_placed`/`_placedTotal` update moved from Peek to Commit |
| GapClosing, EmptyDaySeed | Peek = old NextSlot (enumerator advances); Commit = no-op |
| LeadingChunk, TrailingChunk | Peek = old NextSlot (heap dequeue + re-enqueue); Commit = no-op |
| SequentialSlotter | Peek returns `Fillable[0]`; Commit removes it |

### Planner-side (pending)

The `AcceptProposal` method currently accepts everything `(true, true)`. To be
implemented:

- Duplicate-detection safety net: same `(slot, subject)` proposed twice → `(false, false)`.
- Domain rule evaluation: per-slot rules, per-subject rules, combination rules.

---

## Directory structure

```
MyTimetable/Planning/
├── Pickers/
│   ├── IPicker.cs
│   ├── LargestQueueFirstPicker.cs
│   ├── SmallestQueueFirstPicker.cs
│   ├── RoundRobinPicker.cs
│   ├── FairSharePicker.cs
│   ├── RandomPicker.cs
│   └── WeightedRandomPicker.cs
├── Slotters/
│   ├── ISlotter.cs
│   ├── SequentialSlotter.cs
│   ├── GapClosingSlotter.cs        (also contains GapRange, SlotRange, FirstAndLastOccupied)
│   ├── LeadingChunkGrowthSlotter.cs
│   ├── TrailingChunkGrowthSlotter.cs
│   └── EmptyDaySeedSlotter.cs
├── PlanningSelectorBase.cs
├── IPlanningSelector.cs
├── IPlanningSelectorFactory.cs
├── PlanningSelectorFactory.cs
├── PickerFactory.cs
├── SlotterFactory.cs
├── IPickerFactory.cs
├── ISlotterFactory.cs
├── CompositePlanningSelector.cs
├── ProposeResult.cs
├── DayLayout.cs                    (Day, Chunk, GetDays, FirstChunk, LastChunk)
├── PlannedSlot.cs
├── LargestQueueFirstSelector.cs
├── SmallestQueueFirstSelector.cs
├── RoundRobinSelector.cs
├── FairShareSelector.cs
├── RandomSelector.cs
├── WeightedRandomSelector.cs
├── EmptyDaySeedSelector.cs
├── GapClosingSelector.cs
├── LeadingChunkGrowthSelector.cs
└── TrailingChunkGrowthSelector.cs
```

```
TestMyTimetable/Planning/
├── CompositePlanningSelectorTests.cs
├── DayLayoutTests.cs
├── EmptyDaySeedSelectorTests.cs
├── FairShareSelectorTests.cs
├── GapClosingSelectorTests.cs
├── LargestQueueFirstSelectorTests.cs
├── LeadingChunkGrowthSelectorTests.cs
├── PlanningSelectorFactoryTests.cs
├── RandomSelectorTests.cs
├── RoundRobinPickerTests.cs
├── RoundRobinSelectorTests.cs
├── SmallestQueueFirstSelectorTests.cs
├── TrailingChunkGrowthSelectorTests.cs
└── WeightedRandomSelectorTests.cs
```

---

## Intent

This is a learning exercise, not a commercial requirement. The goal is to work through
design trade-offs and see where they lead.
