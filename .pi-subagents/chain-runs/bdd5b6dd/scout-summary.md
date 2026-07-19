# Scout Summary — Planning Strategies Needing Unit Tests

## Source Layout

| Path | Role |
|---|---|
| `MyTimetable/Planning/` | Production strategy classes (18 files total) |
| `TestMyTimetable/Planning/` | Existing tests (2 strategies covered) |

## Already Tested (skip)

| Strategy | Test File |
|---|---|
| `GapClosingSelector.cs` | `TestMyTimetable/Planning/GapClosingSelector.cs` |
| `DensePackerSelector.cs` | `TestMyTimetable/Planning/DensePackerSelector.cs` |

## Non-Strategies (skip by task)

| File | Purpose |
|---|---|
| `IPlanningSelector.cs` | Interface only |
| `PlanningSelectorBase.cs` | Abstract base with `Plan()` loop |
| `PlanningSelectorFactory.cs` | Factory only |
| `PlannedSlot.cs` | Data model |
| `DayLayout.cs` | Layout helpers |
| `RoundRobinPicker.cs` | Helper used by some strategies |
| `CompositePlanningSelector.cs` | Composite/decorator |

## Strategies Needing Unit Tests (9 total)

### 1. `EmptyDaySeedSelector.cs` (lines 1–33)
- **Inheritance:** `PlanningSelectorBase`
- **Sub-strategy:** `RoundRobinPicker` for subject selection
- **Slot strategy:** Seeds one lesson into each **completely empty** day (no occupied pairs), into the first free slot of that day.
- **Stops when:** No more empty days remain OR queue empty.
- **Dependencies:** `DayLayout.GetDays()`, `DayLayout.FirstChunk()`, `RoundRobinPicker`
- **Test shapes:** Empty queue, single empty day, multiple empty days, mixed empty/non-empty days, all days occupied, queue larger than empty-day count.

### 2. `FairShareSelector.cs` (lines 1–60)
- **Inheritance:** `PlanningSelectorBase`
- **Subject strategy:** Captures initial queue proportions in constructor, then on each step picks the subject that minimizes the sum-of-squared deviations from those proportions after placement.
- **No custom slot logic** (uses base `TakeSlot` = first available).
- **Stops when:** `Available` empty.
- **Dependencies:** None beyond base.
- **Test shapes:** Empty queue, single subject, multiple subjects equal proportions, skewed proportions, single slot vs many slots, exact fit vs leftovers, zero-weight subjects.

### 3. `LargestQueueFirstSelector.cs` (lines 1–8)
- **Inheritance:** `PlanningSelectorBase`
- **Subject strategy:** `Available.MaxBy(kv => kv.Value).Key` — always picks the subject with the largest remaining count. Ties broken by enumeration order.
- **No custom slot logic.**
- **Stops when:** `Available` empty (MaxBy returns default → null key → stop).
- **Dependencies:** None beyond base.
- **Test shapes:** Empty queue, single subject, two subjects with different counts, ties, count reaches zero mid-plan.

### 4. `LeadingChunkGrowthSelector.cs` (lines 1–64)
- **Inheritance:** `PlanningSelectorBase`
- **Sub-strategy:** `RoundRobinPicker` for subject selection
- **Slot strategy:** Grows the **first (leading) chunk** of occupied slots toward the start of the day. Maintains a min-heap by `(chunkLength, date)`. Picks the day with the shortest leading chunk, places a lesson right before it, re-enqueues the day if more space remains before the chunk.
- **Only considers non-empty days** with a leading chunk that has a free slot before it.
- **Dependencies:** `DayLayout.GetDays()`, `DayLayout.FirstChunk()`, `RoundRobinPicker`, `PriorityQueue<,>`
- **Test shapes:** Empty queue, empty fillable list, single day with one slot before chunk, multiple days with different leading chunk lengths, day where chunk reaches slot 1 (stops growing), no qualifying days, round-robin subject cycling.

### 5. `RandomSelector.cs` (lines 1–17)
- **Inheritance:** `PlanningSelectorBase`
- **Subject strategy:** Uniform random selection from `Available` via `Random.Next()`.
- **Accepts optional `Random` seed** (for deterministic testing).
- **No custom slot logic.**
- **Stops when:** `Available` empty.
- **Dependencies:** `System.Random`
- **Test shapes:** Empty queue, deterministic seed reproduces sequence, only one subject available, multiple subjects verify uniform-ish distribution, slot exhaustion before queue.

### 6. `RoundRobinSelector.cs` (lines 1–27)
- **Inheritance:** `PlanningSelectorBase`
- **Subject strategy:** Cycles through subjects in the order of `Queue.Keys` fixed at construction. Skips exhausted subjects.
- **No custom slot logic.**
- **Stops when:** All subjects exhausted.
- **Dependencies:** None beyond base.
- **Test shapes:** Empty queue, single subject cycles correctly, multiple subjects with equal counts, unequal counts (some exhaust early), order matches constructor key order, runs to completion.

### 7. `SmallestQueueFirstSelector.cs` (lines 1–8)
- **Inheritance:** `PlanningSelectorBase`
- **Subject strategy:** `Available.MinBy(kv => kv.Value).Key` — always picks the subject with the smallest remaining count. Ties broken by enumeration order.
- **No custom slot logic.**
- **Stops when:** `Available` empty (MinBy returns default → null key → stop).
- **Dependencies:** None beyond base.
- **Test shapes:** Empty queue, single subject, two subjects with different counts, ties, count reaches zero mid-plan.

### 8. `TrailingChunkGrowthSelector.cs` (lines 1–69)
- **Inheritance:** `PlanningSelectorBase`
- **Sub-strategy:** `RoundRobinPicker` for subject selection
- **Slot strategy:** Mirror of `LeadingChunkGrowthSelector` at the **end** of the day. Grows the **last (trailing) chunk** toward the end of the day. Min-heap by `(chunkLength, date)`. Picks the day with the shortest trailing chunk, places a lesson right after it, re-enqueues if more space remains after the chunk.
- **Only considers non-empty days** with a trailing chunk that has a free slot after it.
- **Dependencies:** `DayLayout.GetDays()`, `DayLayout.LastChunk()`, `RoundRobinPicker`, `PriorityQueue<,>`, `_slotCount`
- **Test shapes:** Empty queue, empty fillable, single day with one slot after chunk, multiple days with different trailing chunk lengths, day where chunk reaches slot 6 (stops growing), no qualifying days, round-robin cycling.

### 9. `WeightedRandomSelector.cs` (lines 1–25)
- **Inheritance:** `PlanningSelectorBase`
- **Subject strategy:** Roulette-wheel selection — probability proportional to remaining count in queue. Builds cumulative distribution from `Available`, rolls `Random.Next(total)`, picks the bucket.
- **Accepts optional `Random` seed** (for deterministic testing).
- **No custom slot logic.**
- **Stops when:** `Available` empty.
- **Dependencies:** `System.Random`
- **Test shapes:** Empty queue, deterministic seed reproduces sequence, single subject always picked, two subjects where one dominates, equal weights, slot exhaustion before queue, fallback branch (last available).

## Common Test Pattern (from existing tests)

All existing tests live in `TestMyTimetable.Planning` namespace, use `xUnit` + `FluentAssertions`. Pattern:

```csharp
[Fact]
public void Plan_SomeCase_ExpectedBehavior()
{
    var queue = new Dictionary<string, int> { ["Subject"] = N };
    var fillable = new List<Slot> { /* Slot instances */ };
    var selector = new SomeSelector(queue, fillable);
    var result = selector.Plan().ToList();
    result.Should().HaveCount(expected);
    // assert slot positions, subject titles, order, etc.
}
```

Key naming: `Test<StrategyName>` class, `Plan_<scenario>_<expectation>` methods.

## Project Visibility

`TestMyTimetable` sees `internal` members of `MyTimetable` via `InternalsVisibleTo` (confirmed by GapClosingSelector tests accessing `DayLayout.GetDays()` directly). No reflection needed for public surface testing.

## Ordering Recommendation

1. **Simple first:** `LargestQueueFirstSelector`, `SmallestQueueFirstSelector`, `RoundRobinSelector` — ~8 LOC each, pure subject logic, no extra deps.
2. **Random with seeding:** `RandomSelector`, `WeightedRandomSelector` — deterministic seed makes tests reproducible.
3. **FairShareSelector** — moderate complexity, custom proportional algorithm.
4. **Slot-strategy classes:** `EmptyDaySeedSelector`, `LeadingChunkGrowthSelector`, `TrailingChunkGrowthSelector` — depend on `DayLayout` helpers and require multi-day fillable setup.
