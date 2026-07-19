# Implementation Plan: Write Unit Tests for All 9 Untested Strategy Classes

## Goal
Add comprehensive unit tests (using xUnit + FluentAssertions) for all 9 untested planning strategy classes, following the established testing patterns in `TestDensePackerSelector` and `TestGapClosingSelector`.

---

## Tasks

Detailed task descriptions and test method names for each strategy.

## JSON Test Plan

The complete structured JSON test plan has been written to:
`C:/Users/PCyur/source/repos/MyTimetable/.pi-subagents/chain-runs/eb2e2b0e/test-plans.json`

That file contains the full `strategies` array with all 9 strategies, each having:
- `name`, `sourceFile`, `testFile`, `testClassName`, and `tests` array with method-name + description strings.

See `test-plans.json` for the authoritative structured output.

## Files to Modify
None — all files are new test files.

## New Files
- `TestMyTimetable/Planning/EmptyDaySeedSelector.cs` – unit tests for `EmptyDaySeedSelector`
- `TestMyTimetable/Planning/FairShareSelector.cs` – unit tests for `FairShareSelector`
- `TestMyTimetable/Planning/LargestQueueFirstSelector.cs` – unit tests for `LargestQueueFirstSelector`
- `TestMyTimetable/Planning/LeadingChunkGrowthSelector.cs` – unit tests for `LeadingChunkGrowthSelector`
- `TestMyTimetable/Planning/RandomSelector.cs` – unit tests for `RandomSelector`
- `TestMyTimetable/Planning/RoundRobinSelector.cs` – unit tests for `RoundRobinSelector`
- `TestMyTimetable/Planning/SmallestQueueFirstSelector.cs` – unit tests for `SmallestQueueFirstSelector`
- `TestMyTimetable/Planning/TrailingChunkGrowthSelector.cs` – unit tests for `TrailingChunkGrowthSelector`
- `TestMyTimetable/Planning/WeightedRandomSelector.cs` – unit tests for `WeightedRandomSelector`

## Dependencies
All test files are independent of each other. Each depends on its corresponding strategy class, `FluentAssertions`, and xUnit (already set up).

## Risks
1. **`DayLayout` and `RoundRobinPicker` are `internal`** — Test project uses `InternalsVisibleTo` (confirmed in existing test files).
2. **`PriorityQueue` usage** — Requires .NET 6+. Verify `.csproj` target.
3. **`FairShareSelector` proportional math** — Tests should verify pick sequence, not just final counts.
4. **Seeded random tests** — Must construct with explicit `Random` seed for determinism.
5. **Empty fillable list edge case** — Constructor may iterate fillable; should not throw.
6. **Test naming pattern** — Match existing: `GapClosingSelector.cs`, `DensePackerSelector.cs` (no `Tests` suffix).
