# Scout Summary: Planning Strategies Needing Unit Tests

## Source Layout

```
MyTimetable/Planning/         → 18 files (9 strategy classes, 4 non-strategies, 5 support files)
TestMyTimetable/Planning/     → 2 test files (GapClosingSelector.cs, DensePackerSelector.cs)
```

## Already Tested (skip)

| Class | Test File |
|---|---|
| `GapClosingSelector` | `TestMyTimetable/Planning/GapClosingSelector.cs` — 61 theory cases for `GetGapsInDay` + 6 `GetDays` tests |
| `DensePackerSelector` | `TestMyTimetable/Planning/DensePackerSelector.cs` — 12 fact tests covering edge-to-edge |

## Excluded Non-Strategies (skip per task)

- `IPlanningSelector.cs` — interface + factory interface
- `PlanningSelectorBase.cs` — abstract base class
- `PlanningSelectorFactory.cs` — generic factory wrapping a Func
- `PlannedSlot.cs` — model
- `DayLayout.cs` — static layout helper
- `RoundRobinPicker.cs` — internal picker used by some selectors
- `CompositePlanningSelector.cs` — orchestrator that chains factories

## Strategy Classes Needing Unit Tests (9 total)

### 1. `EmptyDaySeedSelector.cs` (lines 1–32)
- **File**: `MyTimetable/Planning/EmptyDaySeedSelector.cs`
- **Purpose**: Seeds one lesson into each **completely empty** day (no occupied pairs), into the first free slot. Each empty day is touched exactly once. If the queue empties early, remaining empty days are skipped.
- **Key behaviour**: Extends `PlanningSelectorBase`. Uses `DayLayout.GetDays()` + `DayLayout.FirstChunk()` to detect empty days. Subject selection via `RoundRobinPicker`.
- **Test priority**: Medium — deterministic, well-scoped.
- **Edge cases**: No empty days → nothing placed; fewer lessons than empty days; more lessons than empty days; all days empty; all days non-empty.

### 2. `FairShareSelector.cs` (lines 1–59)
- **File**: `MyTimetable/Planning/FairShareSelector.cs`
- **Purpose**: On each step, selects the subject whose addition minimises the sum-of-squared deviations between the **running placed proportions** and the **original queue proportions** (method-of-largest-remainders style).
- **Key behaviour**: Captures desired proportions in constructor. Maintains `_placed` dictionary + `_placedTotal`. The `DistanceAfterPlacing` simulates what would happen if each candidate were placed.
- **Test priority**: High — non-trivial algorithm, easy to get wrong.
- **Edge cases**: Empty queue; single subject; two subjects with equal proportions; proportions that exactly align; large disproportions; subjects with zero remaining in Available.

### 3. `LargestQueueFirstSelector.cs` (lines 1–12)
- **File**: `MyTimetable/Planning/LargestQueueFirstSelector.cs`
- **Purpose**: Always picks the subject with the **largest remaining count**. Single expression: `Available.MaxBy(kv => kv.Value).Key`.
- **Key behaviour**: No custom `TakeSlot()` — falls through to base (sequential slots). Simplicity means it's easy to verify.
- **Test priority**: Low — trivial logic.
- **Edge cases**: Empty queue; all equal counts; ties (MaxBy returns first).

### 4. `LeadingChunkGrowthSelector.cs` (lines 1–63)
- **File**: `MyTimetable/Planning/LeadingChunkGrowthSelector.cs`
- **Purpose**: Grows the **shortest first occupied chunk** of each day **backwards** (towards start). Uses a `PriorityQueue<(Length, Date)>`. Each placement reduces the gap before the first chunk.
- **Key behaviour**: Custom `TakeSlot()` — dequeues from heap, places immediately before the chunk, re-enqueues with updated length if more space remains before that chunk. Uses `DayLayout.FirstChunk()` to find chunks.
- **Test priority**: High — two-stage (TakeSlot + PickSubject), heap logic is stateful.
- **Edge cases**: No days with first chunk having preceding space; single day; multiple days with varying chunk lengths; queue runs out before placements.

### 5. `RandomSelector.cs` (lines 1–18)
- **File**: `MyTimetable/Planning/RandomSelector.cs`
- **Purpose**: Uniformly random pick among all Available subjects. Accepts optional `Random` for determinism in tests.
- **Key behaviour**: Uses `Random.Shared` by default, or injected instance. Seeded Random makes tests deterministic.
- **Test priority**: Medium — non-deterministic but easily seeded. Core concern: does it ever return null for a non-empty Available?
- **Edge cases**: Empty Available; single subject; multiple subjects — distribution testing optional.

### 6. `RoundRobinSelector.cs` (lines 1–31)
- **File**: `MyTimetable/Planning/RoundRobinSelector.cs`
- **Purpose**: Walks through subjects in fixed construction-order cycle, skipping exhausted ones. Position persists across calls.
- **Key behaviour**: `_order` list captured from `Queue.Keys` in constructor. Circular modulo iteration with wrap-around.
- **Test priority**: Medium — classic round-robin, but stateful.
- **Edge cases**: Empty queue; single subject; all subjects exhausted mid-cycle; subjects with uneven remaining counts (some become exhausted before others).

### 7. `SmallestQueueFirstSelector.cs` (lines 1–12)
- **File**: `MyTimetable/Planning/SmallestQueueFirstSelector.cs`
- **Purpose**: Always picks the subject with the **smallest remaining count**. Single expression: `Available.MinBy(kv => kv.Value).Key`.
- **Key behaviour**: Mirror of `LargestQueueFirstSelector`. No custom `TakeSlot()`.
- **Test priority**: Low — trivial logic.
- **Edge cases**: Empty queue; all equal counts; ties (MinBy returns first).

### 8. `TrailingChunkGrowthSelector.cs` (lines 1–65)
- **File**: `MyTimetable/Planning/TrailingChunkGrowthSelector.cs`
- **Purpose**: Mirror of `LeadingChunkGrowthSelector` — grows the **shortest last occupied chunk** **forwards** (towards end of day). Uses `DayLayout.LastChunk()`.
- **Key behaviour**: Heap-based custom `TakeSlot()`. Places after the last chunk, re-enqueues if more trailing space remains.
- **Test priority**: High — same complexity as LeadingChunkGrowthSelector.
- **Edge cases**: No days with trailing space; `_slotCount` boundary (slot 6); single day; multiple days; queue exhaustion.

### 9. `WeightedRandomSelector.cs` (lines 1–29)
- **File**: `MyTimetable/Planning/WeightedRandomSelector.cs`
- **Purpose**: Roulette-wheel selection — probability proportional to remaining count. Subject with larger remaining is more likely.
- **Key behaviour**: Sums remaining across Available; rolls a random in [0, total); walks Available accumulating until roll < accumulated; fallback to last entry.
- **Test priority**: Medium — seeded random makes it testable.
- **Edge cases**: Empty Available; single subject; subjects with equal weights; one subject far heavier than others.

## Shared Test Infrastructure

- All extend `PlanningSelectorBase` → same constructor pattern `(Dictionary<string, int> queue, List<Slot> fillable)` and public `Plan()` method.
- Plan() returns `IEnumerable<PlannedSlot>` where:
  - `PlannedSlot.Slot` has `Date` and `Number`
  - `PlannedSlot.Lesson` has `Title`, and always `LessonType = "PRACTICE"`
- Existing tests use `FluentAssertions` and `xUnit`.
- Test project uses `InternalsVisibleTo` — internal members of `DayLayout` are accessible.
- Reflective invocation pattern used for private methods (see `GapClosingSelector` test).

## Testing Patterns Seen in Existing Tests

1. **Setup**: Construct queue + fillable lists inline.
2. **Act**: Call `.Plan().ToList()` or `.Plan().Should().BeEmpty()`.
3. **Assert**: Check count, dates, numbers, titles.
4. **Determinism**: Inject `Random` for Random/WeightedRandom tests.

## Order of Difficulty (recommended implementation order)

1. `LargestQueueFirstSelector` — 1 expression
2. `SmallestQueueFirstSelector` — 1 expression
3. `RoundRobinSelector` — classic, moderate
4. `RandomSelector` — seeded Random
5. `WeightedRandomSelector` — seeded Random + roulette
6. `EmptyDaySeedSelector` — integrate with DayLayout
7. `FairShareSelector` — algorithmic
8. `LeadingChunkGrowthSelector` — heap stateful
9. `TrailingChunkGrowthSelector` — heap stateful (mirror of #8)
