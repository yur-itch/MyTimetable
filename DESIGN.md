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
   IPlanningSelector      ← pure-ish algorithm: (queue, slots) → placements[]
        │
        │ yields PlannedSlot[]
        ▼
   Planner                ← decrements _queue, mutates schedule model
```

### Key types

| Type | Role | Location |
|------|------|----------|
| `Planner` | Orchestrator. Owns `_queue`. Calls `ResolveConflicts`, `Plan`. | `MyTimetable/Planner.cs` |
| `IPlanningSelector` | `Plan()` → `IEnumerable<PlannedSlot>`. | `Planning/IPlanningSelector.cs` |
| `IPlanningSelectorFactory` | Creates selectors from `(queue, fillable)`. | `Planning/IPlanningSelectorFactory.cs` |
| `PlanningSelectorBase` | Abstract base. Copies queue & fillable. Runs `TakeSlot`/`PickSubject` loop. | `Planning/PlanningSelectorBase.cs` |
| `CompositePlanningSelector` | Chains multiple factories sequentially. Each inner selector gets the remaining queue & slots. | `Planning/CompositePlanningSelector.cs` |
| `DayLayout` | Static helpers: group slots into days, find first/last occupied chunks. | `Planning/DayLayout.cs` |
| `PlanningSelectorFactory` | Adapter: `Func<queue, fillable, IPlanningSelector>` → `IPlanningSelectorFactory`. | `Planning/PlanningSelectorFactory.cs` |

---

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
   `Fillable` is consumed by `TakeSlot()`.
2. **The planner independently tracks its own `_queue`.** It decrements it as placements
   are yielded — separate from the selector's copy.
3. **No double-counting.** The selector never touches the planner's live queue.
4. **Isolation for compositing.** `CompositePlanningSelector` gives each inner factory
   a snapshot of what's left after previous factories consumed their share.

The alternative — giving selectors a live reference to the planner's queue — would
allow mid-stream retry on rejection, but at the cost of requiring every selector's
internal state to be rewindable (transaction log or re-run). The copy approach keeps
selectors as pure algorithms and planner-level policy in the planner.

---

## Pluggable picker & slotter

Selectors are decomposed into two concerns, each with an interface and multiple
implementations. Every selector accepts optional `IPicker?` / `ISlotter?` and defaults
to its natural implementation. Users can compose custom combinations without writing
new classes.

### Interfaces

```csharp
// Planning/Pickers/IPicker.cs
public interface IPicker {
    string? Pick(Dictionary<string, int> queue);
}

// Planning/Slotters/ISlotter.cs
public interface ISlotter {
    Slot? NextSlot();  // owns fillable reference or precomputed enumerator
}
```

### Pickers (`Planning/Pickers/`)

| Class | Algorithm | State |
|-------|-----------|-------|
| `LargestQueueFirstPicker` | `MaxBy` remaining count | Stateless |
| `SmallestQueueFirstPicker` | `MinBy` remaining count | Stateless |
| `RoundRobinPicker` | Ordered cycle, skips exhausted | `_position` cursor |
| `FairSharePicker` | Minimizes squared deviation from desired proportions | `_placed` counts |
| `RandomPicker` | Uniform random | `Random` instance |
| `WeightedRandomPicker` | Roulette-wheel proportional to remaining | `Random` instance |

### Slotters (`Planning/Slotters/`)

| Class | Algorithm | State |
|-------|-----------|-------|
| `SequentialSlotter` | `Fillable[0]`, remove | Mutable list reference |
| `GapClosingSlotter` | Precomputed gap enumerator, ordered by gap size → date → number | `IEnumerator<Slot>` |
| `LeadingChunkGrowthSlotter` | Priority queue of first chunks, fills leftward toward slot 1 | `PriorityQueue` |
| `TrailingChunkGrowthSlotter` | Priority queue of last chunks, fills rightward toward slot N | `PriorityQueue` |
| `EmptyDaySeedSlotter` | One slot per completely empty day (first open slot) | `IEnumerator<Slot>` |

### Selector wiring

**Picker-only** (use default `SequentialSlotter`):
`LargestQueueFirstSelector`, `SmallestQueueFirstSelector`, `RoundRobinSelector`,
`FairShareSelector`, `RandomSelector`, `WeightedRandomSelector`.

**Dual** (custom slotter, default `RoundRobinPicker`):
`EmptyDaySeedSelector`, `GapClosingSelector`, `LeadingChunkGrowthSelector`,
`TrailingChunkGrowthSelector`.

Example of custom composition:

```csharp
new LeadingChunkGrowthSelector(queue, fillable,
    picker: new FairSharePicker(queue));
```

### Why classes, not delegates

Pickers and slotters are classes rather than delegates because justified streaming
(see below) will require them to defer state commits across rejection-retry cycles:

- RoundRobin must not advance `_position` on a rejected pick.
- FairShare must not increment `_placed` on a rejected pick.
- Chunk-growth heap must not dequeue until the slot is accepted.

Delegates cannot carry this mutable, call-scoped state cleanly.

### Non-decomposable strategies

Some strategies cannot separate picking from slotting (e.g. entropy-based
optimization where slot choice depends on subject and vice versa). These
implement `IPlanningSelector` directly. Two code paths is acceptable.

---

## Future: Justified streaming

Currently selectors stream via `IEnumerable<PlannedSlot>`, but the planner never
injects feedback mid-stream — the `IEnumerable` is structurally unnecessary;
`List<PlannedSlot>` would behave identically.

The plan is to make streaming functional: the planner can reject a placement
(e.g. "no Math on Saturday"), and the selector retries the same slot with a
different subject without rebuilding internal state.

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
| LargestQueueFirst, SmallestQueueFirst, Random, WeightedRandom | Trivial | Add `excluded` set to `IPicker.Pick()` |
| RoundRobin, EmptyDaySeed, GapClosing | Easy | Defer `_position` advance to `CommitState()` |
| FairShare | Medium | Defer `_placed`/`_placedTotal` update to `CommitState()` |
| LeadingChunk, TrailingChunk | Real work | Split `NextSlot()` into peek + commit (heap dequeue deferred) |

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
├── CompositePlanningSelector.cs
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
