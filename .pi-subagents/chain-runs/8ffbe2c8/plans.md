# Implementation Plan: Unit Tests for 9 Untested Planning Strategies

## Goal
Add xUnit + FluentAssertions unit tests for all 9 strategy classes in `MyTimetable/Planning/` that currently have no test coverage, following the patterns established in the existing `GapClosingSelector` and `DensePackerSelector` tests.

## Tasks

### Phase 1 — Trivial PickSubject-only strategies (LargestQueueFirst, SmallestQueueFirst)

1. **Test `LargestQueueFirstSelector`**
   - File: `TestMyTimetable/Planning/LargestQueueFirstSelector.cs`
   - Class: `TestLargestQueueFirstSelector`
   - No custom `TakeSlot()` — base sequential slot behavior applies.
   - Edge cases: empty queue, single subject, multiple subjects with different counts, ties (same count).
   - 5 theory/fact tests.

2. **Test `SmallestQueueFirstSelector`**
   - File: `TestMyTimetable/Planning/SmallestQueueFirstSelector.cs`
   - Class: `TestSmallestQueueFirstSelector`
   - Mirror of Largest — uses `MinBy`.
   - Edge cases: empty queue, single subject, multiple subjects with different counts, ties.
   - 5 theory/fact tests.

### Phase 2 — Stateful pick strategies (RoundRobin, Random, WeightedRandom)

3. **Test `RoundRobinSelector`**
   - File: `TestMyTimetable/Planning/RoundRobinSelector.cs`
   - Class: `TestRoundRobinSelector`
   - Stateful: `_order` captured from `Queue.Keys` in constructor; `_position` persists across calls.
   - Edge cases: empty queue, single subject, two subjects even/uneven counts, multiple subjects with one exhausting mid-cycle.
   - 6 tests.

4. **Test `RandomSelector`**
   - File: `TestMyTimetable/Planning/RandomSelector.cs`
   - Class: `TestRandomSelector`
   - Deterministic via injected `Random` with fixed seed.
   - Edge cases: empty queue, single subject, multiple subjects verifying deterministic output with seeded Random.
   - 5 tests.

5. **Test `WeightedRandomSelector`**
   - File: `TestMyTimetable/Planning/WeightedRandomSelector.cs`
   - Class: `TestWeightedRandomSelector`
   - Roulette-wheel selection; seeded Random for determinism.
   - Edge cases: empty queue, single subject, two subjects with equal/different weights, verifying probability through seeded roll.
   - 6 tests.

### Phase 3 — Slot-strategy with DayLayout integration (EmptyDaySeed)

6. **Test `EmptyDaySeedSelector`**
   - File: `TestMyTimetable/Planning/EmptyDaySeedSelector.cs`
   - Class: `TestEmptyDaySeedSelector`
   - Uses `DayLayout.GetDays()` + `DayLayout.FirstChunk()` to detect empty days. Custom `TakeSlot()` via `_seeds` enumerator + `RoundRobinPicker` for subject selection.
   - Edge cases: no empty days → nothing placed; fewer lessons than empty days; more lessons than empty days; all days empty; all days non-empty.
   - 6 tests.

### Phase 4 — Algorithmic subject strategy (FairShare)

7. **Test `FairShareSelector`**
   - File: `TestMyTimetable/Planning/FairShareSelector.cs`
   - Class: `TestFairShareSelector`
   - Non-trivial algorithm: `DistanceAfterPlacing` computes sum-of-squared deviations. No custom `TakeSlot()` — base sequential slots.
   - Edge cases: empty queue, single subject, two subjects equal proportions, two subjects unequal proportions, zero-remaining subjects in Available, proportions that exactly align.
   - 7 tests.

### Phase 5 — Heap-based slot strategies (LeadingChunkGrowth, TrailingChunkGrowth)

8. **Test `LeadingChunkGrowthSelector`**
   - File: `TestMyTimetable/Planning/LeadingChunkGrowthSelector.cs`
   - Class: `TestLeadingChunkGrowthSelector`
   - Custom `TakeSlot()`: `PriorityQueue<(Length, Date)>` — dequeues shortest first-chunk, places before it, re-enqueues if more space remains.
   - Edge cases: no days with space before first chunk; single day with space; multiple days varying chunk lengths; queue runs out mid-placement; first chunk at start (no space).
   - 7 tests.

9. **Test `TrailingChunkGrowthSelector`**
   - File: `TestMyTimetable/Planning/TrailingChunkGrowthSelector.cs`
   - Class: `TestTrailingChunkGrowthSelector`
   - Mirror of Leading: grows shortest last-chunk forwards. `DayLayout.LastChunk()` + heap.
   - Edge cases: no days with trailing space; single day with trailing space; multiple days; queue exhaustion; last chunk at day end (no space).
   - 7 tests.

## Files to Modify

None. All new test files.

## New Files

- `TestMyTimetable/Planning/LargestQueueFirstSelector.cs` — 5 tests
- `TestMyTimetable/Planning/SmallestQueueFirstSelector.cs` — 5 tests
- `TestMyTimetable/Planning/RoundRobinSelector.cs` — 6 tests
- `TestMyTimetable/Planning/RandomSelector.cs` — 5 tests
- `TestMyTimetable/Planning/WeightedRandomSelector.cs` — 6 tests
- `TestMyTimetable/Planning/EmptyDaySeedSelector.cs` — 6 tests
- `TestMyTimetable/Planning/FairShareSelector.cs` — 7 tests
- `TestMyTimetable/Planning/LeadingChunkGrowthSelector.cs` — 7 tests
- `TestMyTimetable/Planning/TrailingChunkGrowthSelector.cs` — 7 tests

Total: 54 test methods across 9 new test files.

## Dependencies

- Phases 1–3 have no cross-dependencies; order is by complexity.
- Phase 4 (FairShare) should come before Phase 5 due to algorithmic complexity.
- Phase 5 (heap selectors) are independent of Phase 4 and each other but recommended last due to stateful heap logic.
- All tests depend on `PlanningSelectorBase` API remaining stable.

## Risks

1. **RoundRobinPicker reuse**: `EmptyDaySeedSelector`, `LeadingChunkGrowthSelector`, and `TrailingChunkGrowthSelector` compose `RoundRobinPicker` internally (not `RoundRobinSelector`). The picker is `internal` but visible via `InternalsVisibleTo`. Tests for these selectors verify placement order indirectly through the `Plan()` output — no need to test the picker directly.
2. **DayLayout is internal**: Accessible via `InternalsVisibleTo`. Tests do not need reflection for `GetDays`/`FirstChunk`/`LastChunk` — same pattern as `DensePackerSelector` test.
3. **Heap determinism**: `PriorityQueue` in .NET 6+ is stable for equal priorities (FIFO on tie). The `(Length, Date)` tuple priority ensures deterministic ordering when lengths are equal and dates differ. Tests should verify this tie-breaking.
4. **FairShare floating-point**: Sum-of-squared deviations with `double` may have tiny rounding differences. Tests should use approximate equality (e.g. check subject titles and counts rather than exact distance values).
5. **Seeded Random determinism**: `Random` with a fixed seed is deterministic across .NET versions on the same runtime. Tests should validate specific sequences rather than rely on distribution properties.
