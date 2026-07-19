# Implementation Plan

## Goal
Add a **DensePacker** scheduling strategy (already implemented as `DensePackerSelector.cs`) to the strategy registry in `Program.cs` and add unit tests — the implementation itself already exists and matches the spec.

## Background
The file `MyTimetable/Planning/DensePackerSelector.cs` already exists with the exact strategy described:
1. Groups fillable slots into days via `DayLayout.GetDays(Fillable)`
2. Computes occupancy per day as `slotCount - day.Open.Count`
3. Filters to days that have **both** occupied slots (`Occupied > 0`) and remaining gaps (`Day.Open.Count > 0`)
4. Sorts days descending by occupied count, then by date as tiebreaker
5. Takes the **first** open slot (`Day.Open[0]`) from each such day — exactly one lesson per day
6. Uses `RoundRobinPicker` for subject selection
7. When lessons outnumber non-empty days with gaps, excess lessons are not placed (the enumerator ends)

## Tasks

### Task 1: Verify existing implementation correctness
**Description**: Confirm that `DensePackerSelector.cs` correctly implements the three rules (sort by occupancy, first gap, round-robin subject selection) and that its constructor signature matches other slot-strategy selectors (takes `queue, fillable, slotCount` with default 6).

- **File**: `MyTimetable/Planning/DensePackerSelector.cs`
- **Changes**: None needed — already correct.
- **Acceptance**: Code is readable, follows the same pattern as `EmptyDaySeedSelector`/`LeadingChunkGrowthSelector`, `RoundRobinPicker` is composed correctly, and `TakeSlot()` uses an `IEnumerator<Slot>` pull pattern consistent with other slot-strategies.

### Task 2: Register DensePacker in Program.cs
**Description**: Add a `"dense"` entry to the strategy dictionary in `Program.cs` so the controller can resolve it via the `strategies` query parameter.

- **File**: `MyTimetable/Program.cs` (line ~31, after the existing strategies)
- **Changes**: Insert one new dictionary entry:
  ```csharp
  ["dense"] = new PlanningSelectorFactory((q, s) => new DensePackerSelector(q, s, DaySchedule.DefaultSlotCount)),
  ```
- **Acceptance**: `GET /App/Plan?titles=Math=3&strategies=dense` resolves the key and plans without error.

### Task 3: Add unit tests for DensePackerSelector
**Description**: Create `TestMyTimetable/Planning/DensePackerSelector.cs` following the same xunit + FluentAssertions pattern as `TestGapClosingSelector.cs`. Cover:

1. **Empty queue** — no subjects → `Plan()` yields nothing.
2. **Single day, single open slot** — one placement in that slot.
3. **Two days, one crowded (2/6 occupied) and one sparse (0/6 occupied)** — only the crowded day gets a placement (the sparse day has `Occupied == 0`, filtered out).
4. **Two equally crowded days** — order by date (stable tiebreaker).
5. **More subjects than non-empty days with gaps** — only `N` placements (one per qualifying day), remaining queue items not placed.
6. **All days empty** — no placements (all `Occupied == 0`).
7. **All days full** — no placements (all `Open.Count == 0`).
8. **Round-robin subject distribution** — verify subjects cycle correctly across days.
9. **Default 6-slot day boundary** — slotCount integration.

Use `DayLayout.GetDays` (internal, visible via `InternalsVisibleTo`) to build the fillable slot list from `Day` objects, or construct `List<Slot>` directly. Pass `slotCount` explicitly.

- **File**: `TestMyTimetable/Planning/DensePackerSelector.cs` (new)
- **Changes**: Full test class with `[Fact]` and `[Theory]` methods.
- **Acceptance**: All tests pass under `dotnet test TestMyTimetable`.

### Task 4: Run tests and verify build
**Description**: Execute `dotnet build` and `dotnet test TestMyTimetable` to confirm no regressions.

- **Acceptance**: Build succeeds; all existing tests (GapClosingSelector + new DensePacker tests) pass.

## Files to Modify
- `MyTimetable/Program.cs` — add `"dense"` registration

## New Files
- `TestMyTimetable/Planning/DensePackerSelector.cs` — unit tests

## Dependencies
- Task 2 (registration) is independent of Task 3 (tests), but both depend on Task 1 (verification).
- Task 4 depends on both Tasks 2 and 3.

## Risks
1. **No context.md file** — the expected context path did not exist. The plan was derived purely from reading the source tree.
2. **Existing implementation already complete** — the selector already exists and is correct. The main work is registration and testing, not implementation.
3. **Test project requires xunit 2.9.3 + FluentAssertions 8.10.0** — verify compatibility before authoring tests. Already confirmed from `.csproj`.
4. **`DayLayout.GetDays` is `internal`** — tests can access it via `InternalsVisibleTo` (already configured for `TestMyTimetable`), no reflection needed.
5. **Slot-strategy selectors receive a copy of the fillable list** — the constructor copies via `base(queue, fillable)`. Tests must ensure this doesn't leak mutations.

## Acceptance Contract

### Evidence required per criterion
| Criterion | How to satisfy |
|-----------|---------------|
| Implement the requested change without widening scope | Only touch DensePackerSelector (verify), Program.cs (register), and test file. No new infrastructure, no refactoring of other selectors. |
| changed-files | `MyTimetable/Program.cs` |
| tests-added | `TestMyTimetable/Planning/DensePackerSelector.cs` |
| commands-run | `dotnet build`, `dotnet test TestMyTimetable` |
| residual-risks | Listed in Risks above |
| no-staged-files | True (plan-only agent; no git operations performed) |
