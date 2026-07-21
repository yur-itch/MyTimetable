# Planning Subsystem — Architecture & Design Decisions

## Status

- **Goal 2 (picker/slotter extraction): done.** 11 implementation classes extracted,
  10 selectors wired, all 449 tests pass.
- **Goal 1 (justified streaming): not started.**

---

## Architecture overview

```
User input (subjects + counts)
        │
        ▼
   Planner._queue         ← source of truth for "what still needs placement"
        │
        │ Create(snapshot of queue, snapshot of fillable slots)
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
| `IPlanningSelector` | Interface: `Plan()` → `IEnumerable<PlannedSlot>`. | `Planning/IPlanningSelector.cs` |
| `IPlanningSelectorFactory` | Creates selectors from `(queue, fillable)`. | `Planning/IPlanningSelectorFactory.cs` |
| `PlanningSelectorBase` | Abstract base. Copies queue & fillable. Runs `TakeSlot`/`PickSubject` loop. | `Planning/PlanningSelectorBase.cs` |
| `CompositePlanningSelector` | Chains multiple factories sequentially. Each inner selector gets remaining queue & slots. | `Planning/CompositePlanningSelector.cs` |
| `DayLayout` | Static helpers: group slots into days, find first/last occupied chunks. | `Planning/DayLayout.cs` |
| `PlanningSelectorFactory` | Adapter: `Func<queue, fillable, IPlanningSelector>` → `IPlanningSelectorFactory`. | `Planning/PlanningSelectorFactory.cs` |

---

## Design decision 1: Copy-based selectors (paradigm 2)

### The two paradigms considered

| | Paradigm 1 (reference) | Paradigm 2 (copy — chosen) |
|---|---|---|
| Selector sees | Live queue via shared reference | Frozen snapshot copied in constructor |
| Planner rejects a placement | Selector sees unchanged queue, retries | Selector has already advanced past it |
| Testability | Requires planner mock or queue lifecycle | Pure in/out, no external state |
| Composability | Must pass reference down tree | Each level gets its own snapshot |

### Why copies

The base class constructor copies both inputs:

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

### The streaming question

Selectors stream via `IEnumerable<PlannedSlot>`, but since the planner never injects
feedback mid-stream (paradigm 2), `List<PlannedSlot>` would behave identically.
The streaming is kept because Goal 1 plans to make it functional — the planner will
be able to reject placements and the selector will retry with a different subject
for the same slot.

---

## Design decision 2: Pluggable picker & slotter

### Motivation

- `RoundRobinPicker` was already a standalone class reused by 4 strategies.
- All other picker/slotter logic was inlined per selector.
- Users could not mix pickers and slotters without writing new selector classes.
- The developer was dissatisfied with RoundRobin and FairShare as the only
  default pickers for slot-oriented strategies.

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

No `excluded` parameter yet — that arrives with Goal 1 (justified streaming).

### Extracted pickers (`Planning/Pickers/`)

| Class | Algorithm | State |
|-------|-----------|-------|
| `LargestQueueFirstPicker` | `MaxBy` remaining count | Stateless |
| `SmallestQueueFirstPicker` | `MinBy` remaining count | Stateless |
| `RoundRobinPicker` | Ordered cycle, skips exhausted | `_position` cursor |
| `FairSharePicker` | Minimizes squared deviation from desired proportions | `_placed` counts |
| `RandomPicker` | Uniform random | `Random` instance |
| `WeightedRandomPicker` | Roulette-wheel proportional to remaining | `Random` instance |

### Extracted slotters (`Planning/Slotters/`)

| Class | Algorithm | State |
|-------|-----------|-------|
| `SequentialSlotter` | `Fillable[0]`, remove | Mutable list reference |
| `GapClosingSlotter` | Precomputed gap enumerator, ordered by gap size → date → number | `IEnumerator<Slot>` |
| `LeadingChunkGrowthSlotter` | Priority queue of first chunks, fills leftward toward slot 1 | `PriorityQueue` |
| `TrailingChunkGrowthSlotter` | Priority queue of last chunks, fills rightward toward slot N | `PriorityQueue` |
| `EmptyDaySeedSlotter` | One slot per completely empty day (first open slot) | `IEnumerator<Slot>` |

### Selector wiring

Every selector accepts optional `IPicker?` and/or `ISlotter?` in its constructor.
If not provided, it falls back to the natural implementation.

**Picker-only selectors** (use default `SequentialSlotter`):
`LargestQueueFirstSelector`, `SmallestQueueFirstSelector`, `RoundRobinSelector`,
`FairShareSelector`, `RandomSelector`, `WeightedRandomSelector`.

**Dual selectors** (custom slotter, default `RoundRobinPicker`):
`EmptyDaySeedSelector`, `GapClosingSelector`, `LeadingChunkGrowthSelector`,
`TrailingChunkGrowthSelector`.

Example of custom composition (zero user-written classes):

```csharp
new LeadingChunkGrowthSelector(queue, fillable,
    picker: new FairSharePicker(queue));
```

### Why classes, not delegates

In paradigm 2 (no feedback), pickers and slotters could be pure functions —
delegates would work:

```csharp
delegate string? Picker(IReadOnlyDictionary<string, int> queue);
delegate Slot? Slotter(List<Slot> fillable);
```

With justified streaming (Goal 1), the picker may be asked "try again" for the
same slot after a rejection. This requires mutable internal state scoped to the
`Plan()` call:

- RoundRobin must defer `_position` advance until acceptance.
- FairShare must defer `_placed` update until acceptance.
- Chunk-growth heap must defer dequeue until acceptance.
- The exclusion set for the current slot survives across retries.

Delegates cannot carry this state cleanly — closures with mutable captures are
classes by another name and harder to test. Classes are the correct choice.

### Non-decomposable strategies

Some strategies cannot separate picking from slotting (e.g. entropy-based
optimization where slot choice depends on subject and vice versa). These
continue to implement `IPlanningSelector` directly. Two code paths is acceptable.

---

## Goal 1: Justified streaming (future)

### Current state

The planner never injects feedback mid-stream. Selectors operate on a frozen snapshot.
The `IEnumerable` streaming is structurally decorative.

### Target

The planner can reject a placement (e.g. "no Math on Saturday"), and the selector
adapts on the fly — retrying the same slot with a different subject — without
rebuilding internal state from scratch.

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

## Test project

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
design trade-offs and see where they lead. Composing strategies from parts should require
zero user-written code.
