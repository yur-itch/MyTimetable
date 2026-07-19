# Implementation Plan

## Goal
Create a complete suite of xUnit+FluentAssertions unit tests for the 9 untested planning strategies in `MyTimetable/Planning/`, following the established patterns from `TestDensePackerSelector` and `TestGapClosingSelector`.

## Tasks

### Phase 1: Simple-subject-strategy selectors (~8 LOC each, no extra deps)

**1. `TestLargestQueueFirstSelector`** — 6 test methods
- File: `TestMyTimetable/Planning/LargestQueueFirstSelector.cs`
- Changes: new test class
- Tests:
  - `Plan_EmptyQueue_YieldsNothing` — empty dictionary → Plan() empty
  - `Plan_SingleSubject_PlacesAllSlots` — {"Math":3} with 3 fillable slots → 3 results, all "Math"
  - `Plan_TwoSubjectsDifferentCounts_LargestPickedFirst` — {"Math":3,"Physics":1} with 4 fillable → first 3 are "Math", last is "Physics"
  - `Plan_TwoSubjectsEqualCount_TieBrokenByEnumeration` — {"Math":2,"Physics":2} with 4 fillable → alternation depends on enumeration order (Math first)
  - `Plan_CountReachesZeroMidPlan_StopsWhenRemainingSubjectExhausted` — {"Math":1} with 3 fillable → only 1 result
  - `Plan_MultipleSlotsExhaustsQueue_QueueDepletedBeforeFillable` — {"Math":2} with 5 fillable → 2 results
- Acceptance: all tests pass

**2. `TestSmallestQueueFirstSelector`** — 6 test methods
- File: `TestMyTimetable/Planning/SmallestQueueFirstSelector.cs`
- Changes: new test class
- Tests:
  - `Plan_EmptyQueue_YieldsNothing` — empty dictionary
  - `Plan_SingleSubject_PlacesAllSlots` — {"Math":3} → 3 results
  - `Plan_TwoSubjectsDifferentCounts_SmallestPickedFirst` — {"Math":3,"Physics":1} → first result is "Physics", then "Math"
  - `Plan_TwoSubjectsEqualCount_TieBrokenByEnumeration` — {"Math":2,"Physics":2} → Math first (enumeration order)
  - `Plan_CountReachesZeroMidPlan_StopsWhenSubjectExhausted` — {"Math":0,"Physics":2} → starts Physics, continues Physics
  - `Plan_MultipleSlotsExhaustsQueue_QueueDepletedBeforeFillable` — {"Math":2} with 5 fillable → 2 results
- Acceptance: all tests pass

**3. `TestRoundRobinSelector`** — 7 test methods
- File: `TestMyTimetable/Planning/RoundRobinSelector.cs`
- Changes: new test class
- Tests:
  - `Plan_EmptyQueue_YieldsNothing` — empty dictionary
  - `Plan_SingleSubject_CyclesCorrectly` — {"Math":3} → 3 results all "Math"
  - `Plan_MultipleSubjectsWithEqualCounts_CyclesInConstructorKeyOrder` — {"Math":2,"Physics":2,"Chem":2} with 6 fillable → Math, Physics, Chem, Math, Physics, Chem
  - `Plan_UnequalCounts_ExhaustedSubjectSkipped` — {"Math":1,"Physics":2,"Chem":1} with 4 fillable → Math, Physics, Chem, Physics
  - `Plan_ConstructorOrderMatchesQueueKeysOrder` — verify order stability
  - `Plan_RunsToCompletionAllSubjectsExhausted` — 3 subjects, sum total = fillable count
  - `Plan_QueueLargerThanFillable_StopsWhenSlotsRunOut` — more queue items than slots
- Acceptance: all tests pass

### Phase 2: Random-with-seeding selectors

**4. `TestRandomSelector`** — 5 test methods
- File: `TestMyTimetable/Planning/RandomSelector.cs`
- Changes: new test class
- Tests:
  - `Plan_EmptyQueue_YieldsNothing` — empty dictionary
  - `Plan_DeterministicSeed_ReproducesSequence` — same seed produces same subjects same order
  - `Plan_SingleSubjectAlwaysPicked` — {"Math":5} → 5 results all "Math"
  - `Plan_MoreQueueThanSlots_StopsWhenSlotsExhausted` — larger queue than fillable
  - `Plan_SlotsExhaustedBeforeQueue_YieldsNothing` — no fillable slots → empty result
- Acceptance: all tests pass

**5. `TestWeightedRandomSelector`** — 6 test methods
- File: `TestMyTimetable/Planning/WeightedRandomSelector.cs`
- Changes: new test class
- Tests:
  - `Plan_EmptyQueue_YieldsNothing` — empty dictionary
  - `Plan_DeterministicSeed_ReproducesSequence` — same seed → same results
  - `Plan_SingleSubjectAlwaysPicked` — {"Math":5} → 5 results all "Math"
  - `Plan_TwoSubjectsOneDominates_DominantSubjectMoreFrequent` — {"Math":10,"Physics":1} with deterministic seed → verify proportion
  - `Plan_EqualWeights_UniformDistribution` — {"Math":3,"Physics":3} with seed → verify sequence
  - `Plan_FallbackBranch_LastAvailableKeySelected` — edge case where cumulative sum rounding would miss
- Acceptance: all tests pass

### Phase 3: FairShareSelector (moderate complexity)

**6. `TestFairShareSelector`** — 7 test methods
- File: `TestMyTimetable/Planning/FairShareSelector.cs`
- Changes: new test class
- Tests:
  - `Plan_EmptyQueue_YieldsNothing` — empty dictionary
  - `Plan_SingleSubject_PlacesAll` — {"Math":4} with 4 fillable → 4 results all "Math"
  - `Plan_MultipleSubjectsEqualProportions_AlternatesFairly` — {"Math":2,"Physics":2} with 4 fillable → alternates to maintain equal proportions
  - `Plan_SkewedProportions_MatchesDesiredDistribution` — {"Math":3,"Physics":1} with 4 fillable → 3 Math, 1 Physics
  - `Plan_SingleSlotVsManySlots_ProportionsHold` — large queue vs small fillable
  - `Plan_ExactFitVsLeftovers_RemainingSlotsGoToLargerSubject` — proportions drive tie-breaking
  - `Plan_ZeroWeightSubjects_NotSelected` — {"Math":0,"Physics":2} → only Physics selected
- Acceptance: all tests pass

### Phase 4: Slot-strategy classes (depend on DayLayout helpers)

**7. `TestEmptyDaySeedSelector`** — 6 test methods
- File: `TestMyTimetable/Planning/EmptyDaySeedSelector.cs`
- Changes: new test class
- Tests:
  - `Plan_EmptyQueue_YieldsNothing` — empty queue, single empty day → no results
  - `Plan_SingleEmptyDay_SeedsOneLesson` — {"Math":2}, fillable with entirely empty day (slots 1-6 all open) → 1 result at slot 1 of that day
  - `Plan_MultipleEmptyDays_SeedsOnePerDay` — 3 empty days, queue has 3+ → 3 results, one per day, first slot each
  - `Plan_MixedEmptyAndNonEmptyDays_OnlyEmptyDaysSeeded` — 1 empty day + 1 partially occupied day → only empty day gets a seed
  - `Plan_AllDaysOccupied_YieldsNothing` — all days have at least one occupied pair → no results
  - `Plan_QueueLargerThanEmptyDayCount_SeedsOnlyEmptyDays` — 2 empty days, queue of 5 → 2 results only
- Acceptance: all tests pass

**8. `TestLeadingChunkGrowthSelector`** — 7 test methods
- File: `TestMyTimetable/Planning/LeadingChunkGrowthSelector.cs`
- Changes: new test class
- Tests:
  - `Plan_EmptyQueue_YieldsNothing` — empty queue
  - `Plan_NoFillableDays_YieldsNothing` — no days with leading chunk having free slot before it
  - `Plan_SingleDayOneSlotBeforeChunk_PlacesInThatSlot` — day with occupied chunk starting at slot 2 → places at slot 1
  - `Plan_MultipleDaysDifferentChunkLengths_ShortestChunkGrowsFirst` — two days with leading chunks of length 2 and 3 → length-2 day gets slot first
  - `Plan_ChunkReachesSlot1_StopsGrowingThatDay` — chunk at slot 2, after placement chunk starts at slot 1 → no more placements in that day
  - `Plan_NoQualifyingDays_ReturnsEmpty` — all leading chunks already at slot 1
  - `Plan_RoundRobinCycling_MultipleSubjects` — verify subject cycling via RoundRobinPicker across multiple placements
- Acceptance: all tests pass

**9. `TestTrailingChunkGrowthSelector`** — 7 test methods
- File: `TestMyTimetable/Planning/TrailingChunkGrowthSelector.cs`
- Changes: new test class
- Tests:
  - `Plan_EmptyQueue_YieldsNothing`
  - `Plan_NoFillableDays_YieldsNothing` — no days with trailing chunk having free slot after it
  - `Plan_SingleDayOneSlotAfterChunk_PlacesInThatSlot` — day with occupied chunk ending at slot 5 (slotCount=6) → places at slot 6
  - `Plan_MultipleDaysDifferentChunkLengths_ShortestChunkGrowsFirst` — two days with trailing chunks of length 2 and 3 → length-2 day gets slot first
  - `Plan_ChunkReachesSlot6_StopsGrowingThatDay` — chunk at slot 5, after placement chunk ends at slot 6 → no more placements in that day
  - `Plan_NoQualifyingDays_ReturnsEmpty`
  - `Plan_RoundRobinCycling_MultipleSubjects`
- Acceptance: all tests pass

## Files to Modify
None — all changes are new files.

## New Files (9 test files)
- `TestMyTimetable/Planning/LargestQueueFirstSelector.cs`
- `TestMyTimetable/Planning/SmallestQueueFirstSelector.cs`
- `TestMyTimetable/Planning/RoundRobinSelector.cs`
- `TestMyTimetable/Planning/RandomSelector.cs`
- `TestMyTimetable/Planning/WeightedRandomSelector.cs`
- `TestMyTimetable/Planning/FairShareSelector.cs`
- `TestMyTimetable/Planning/EmptyDaySeedSelector.cs`
- `TestMyTimetable/Planning/LeadingChunkGrowthSelector.cs`
- `TestMyTimetable/Planning/TrailingChunkGrowthSelector.cs`

## Dependencies
- Phase 1 (tasks 1-3): No dependencies — can be done in any order
- Phase 2 (tasks 4-5): No dependencies — can be done in any order
- Phase 3 (task 6): No dependencies
- Phase 4 (tasks 7-9): No dependencies on each other; depend on understanding of `DayLayout.GetDays()`, `DayLayout.FirstChunk()`, `DayLayout.LastChunk()`, and `RoundRobinPicker`
- All phases are independent and can be parallelized

## Risks
1. **DayLayout.FirstChunk/LastChunk internal types**: `Day` and `Chunk` are `internal` — but `TestMyTimetable` has `InternalsVisibleTo`, so direct usage of `DayLayout` static methods in tests is fine (confirmed by GapClosingSelector test calling `DayLayout.GetDays()`).
2. **Empty day detection in EmptyDaySeedSelector**: `DayLayout.FirstChunk(d.Open, slotCount) is null` means day is fully empty. Test fillable must represent an entirely free day (all slots 1-6 present in Open).
3. **Leading/Trailing chunk start/end conditions**: LeadingChunkGrowthSelector only considers days where `c.Start > 1`; TrailingChunkGrowthSelector only where `c.End < _slotCount`. Tests must set up fillable accordingly.
4. **RoundRobinPicker is internal**: Accessed via `InternalsVisibleTo` already — no issue.
5. **PriorityQueue behavior**: The `(Length, Date)` tuple ordering in min-heap means ties broken by Date. Tests should verify that.
6. **FairShareSelector's DistanceAfterPlacing**: This computes sum-of-squared deviations from desired proportions. Tests with exact-fit queue counts may produce deterministic ordering that matches equal proportion alternation — verify carefully.
7. **Random seed determinism**: Both `RandomSelector` and `WeightedRandomSelector` accept optional `Random` parameter. Tests should pass a seeded `Random` (e.g., `new Random(42)`) for deterministic verification.

## Acceptance Criteria
- All 9 test files compile and pass
- Each file follows the established `Test<StrategyName>` class pattern
- Each method uses `[Fact]` (or `[Theory]` where appropriate) + `FluentAssertions`
- Tests are isolated (no shared state between test methods)
- Empty queue edge case covered for every strategy
- Subject ordering behavior verified where deterministic
