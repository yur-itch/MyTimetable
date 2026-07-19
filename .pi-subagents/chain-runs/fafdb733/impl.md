# Implementation: DensePackerSelector

## Summary
Implemented the **DensePacker** scheduling strategy registration and unit tests. The strategy implementation itself (`DensePackerSelector.cs`) already existed and was verified correct.

## Changes Made

### 1. Verified existing implementation (Task 1)
- **File**: `MyTimetable/Planning/DensePackerSelector.cs`
- **Result**: Already correct — follows `PlanningSelectorBase` pattern, uses `DayLayout.GetDays` for day grouping, `RoundRobinPicker` for subject selection, and `IEnumerator<Slot>` pull pattern in `TakeSlot()`
- Algorithm matches spec: groups by day, computes occupancy, filters to days with both occupied slots and gaps, sorts by occupancy desc then by date, takes first open slot per day, uses round-robin subject selection

### 2. Registered in Program.cs (Task 2)
- **File**: `MyTimetable/Program.cs`
- **Change**: Added line 38:
  ```csharp
  ["dense"] = new PlanningSelectorFactory((q, s) => new DensePackerSelector(q, s, DaySchedule.DefaultSlotCount)),
  ```
- **Result**: Controller can now resolve via `strategies=dense` query parameter

### 3. Created unit tests (Task 3)
- **File**: `TestMyTimetable/Planning/DensePackerSelector.cs`
- **13 tests covering all required scenarios**:
  1. `Plan_EmptyQueue_YieldsNothing` — empty queue yields nothing
  2. `Plan_SingleDaySingleOpenSlot_PlacesOneLesson` — one placement in one open slot
  3. `Plan_CrowdedAndEmptyDay_OnlyCrowdedDayGetsPlacement` — only crowded day (Occupied>0) qualifies
  4. `Plan_TwoEquallyCrowdedDays_OrdersByDate` — tiebreaker by date ascending
  5. `Plan_MoreSubjectsThanQualifyingDays_LimitedToQualifyingDays` — excess queue items not placed
  6. `Plan_AllDaysEmpty_YieldsNothing` — all Occupied=0, filtered out
  7. `Plan_AllDaysFull_YieldsNothing` — all Open.Count=0, filtered out
  8. `Plan_MultipleSubjects_RoundRobinCyclesCorrectly` — round-robin: Math, Physics, Chem
  9. `Plan_DefaultSlotCount_ComputesOccupiedCorrectly` — slotCount boundary respected
  10. `Plan_QueueLargerThanQualifyingDays_NoPlacementBeyondLimit` — capped at qualifying days count
  11. `Plan_DayWithGapsAndDayWithoutGaps_SkipsFullDay` — full day (no open slots) skipped
  12. `Plan_HigherOccupancyDaysComeFirst` — higher occupancy sorts before lower
  13. `Plan_SameOccupancyDifferentOpenSlots_FirstOpenSlotUsed` — uses Open[0] slot

### 4. Build and test (Task 4)
- `dotnet build` — **succeeded** (0 errors, pre-existing warnings only)
- `dotnet test TestMyTimetable` — **84 passed, 0 failed, 0 skipped** (includes existing GapClosingSelector tests + new tests)

## Open Risks
1. No pre-existing `context.md` file — plan derived from source tree reading
2. MyTimetable.Proxy project has a pre-existing file-locking issue (unrelated to this change)
3. All tests use default `slotCount=6` and `InternalsVisibleTo` for `DayLayout` access — no reflection needed
4. The strategy copies fillable list in constructor, preventing mutation leaks (confirmed)

## Recommendations
- Strategy is meant to be used in a chain: `dense → roundrobin` — first fills dense days (1 lesson each), then fills remaining gaps in round-robin
