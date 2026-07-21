# Design Decisions — Planning Subsystem

## Status

Draft. Two goals defined below. Implementation not started.

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

```csharp
interface IPicker {
    string? Pick(IReadOnlyDictionary<string, int> queue, HashSet<string> excluded);
}

interface ISlotter {
    Slot? NextSlot(List<Slot> fillable);
}
```

Existing logic gets extracted into named implementations:

| Picker | Slotter |
|--------|---------|
| RoundRobinPicker | SequentialSlotter |
| LargestQueueFirstPicker | GapClosingSlotter |
| SmallestQueueFirstPicker | LeadingChunkSlotter |
| FairSharePicker | TrailingChunkSlotter |
| RandomPicker | EmptyDaySeedSlotter |
| WeightedRandomPicker | |

### Non-decomposable strategies

Some strategies (e.g. entropy-based optimization) cannot separate picking from slotting —
slot choice depends on subject choice and vice versa. These continue to implement the
full selector interface directly. Two code paths is acceptable: decomposable strategies
use picker+slotter composition; non-decomposable ones implement the full contract.

### Intent

This is a learning exercise, not a commercial requirement. The goal is to work through
the design trade-offs and see where they lead. Composing strategies from parts should
require zero user-written code.
