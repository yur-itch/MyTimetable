# Scout Summary: Planning Strategy Unit Tests Needed

## Files Retrieved

| # | File | Why it matters |
|---|------|----------------|
| 1 | `MyTimetable/Planning/IPlanningSelector.cs` (all) | Interface contract — `Plan()` returns `IEnumerable<PlannedSlot>` |
| 2 | `MyTimetable/Planning/PlanningSelectorBase.cs` (all) | Abstract base — subclasses override `PickSubject()`, optionally `TakeSlot()` |
| 3 | `MyTimetable/Planning/EmptyDaySeedSelector.cs` (all) | Strategy needing tests |
| 4 | `MyTimetable/Planning/FairShareSelector.cs` (all) | Strategy needing tests |
| 5 | `MyTimetable/Planning/RandomSelector.cs` (all) | Strategy needing tests |
| 6 | `MyTimetable/Planning/RoundRobinSelector.cs` (all) | Strategy needing tests |
| 7 | `MyTimetable/Planning/LargestQueueFirstSelector.cs` (all) | Strategy needing tests |
| 8 | `MyTimetable/Planning/SmallestQueueFirstSelector.cs` (all) | Strategy needing tests |
| 9 | `MyTimetable/Planning/LeadingChunkGrowthSelector.cs` (all) | Strategy needing tests |
| 10 | `MyTimetable/Planning/TrailingChunkGrowthSelector.cs` (all) | Strategy needing tests |
| 11 | `MyTimetable/Planning/WeightedRandomSelector.cs` (all) | Strategy needing tests |
| 12 | `TestMyTimetable/Planning/DensePackerSelector.cs` (first 80 lines) | Existing test pattern reference |
| 13 | `TestMyTimetable/Planning/GapClosingSelector.cs` (first 80 lines) | Existing test pattern reference |

## Existing Tests (already covered)

- `TestMyTimetable/Planning/DensePackerSelector.cs` — `TestDensePackerSelector` class
- `TestMyTimetable/Planning/GapClosingSelector.cs` — `TestGapClosingSelector` class

## Strategy Classes That Need Unit Tests

### 1. `EmptyDaySeedSelector` (Lines 1–28)
- **File:** `MyTimetable/Planning/EmptyDaySeedSelector.cs`
- **Logic:** Seeds one lesson into each **completely empty** day (no occupied slots). Uses `DayLayout.FirstChunk(d.Open, slotCount)` to find days where the first chunk is `null` (empty). Stops when seeds exhausted or queue empty.
- **Picker:** `RoundRobinPicker` for subject selection.
- **Constructor params:** `(Dictionary<string, int> queue, List<Slot> fillable, int slotCount = 6)`
- **Test considerations:** Empty days, days with some occupied slots, queue empties before all empty days seeded, varying slotCount.

### 2. `FairShareSelector` (Lines 1–64)
- **File:** `MyTimetable/Planning/FairShareSelector.cs`
- **Logic:** Picks the subject whose addition minimizes the squared deviation from desired proportions. Maintains `_placed` dictionary and `_placedTotal`. Clones proportions at construction via `CaptureDesired()`.
- **Constructor params:** `(Dictionary<string, int> queue, List<Slot> fillable)`
- **Test considerations:** Single subject, multiple subjects with varying proportions, tie-breaking, empty queue.

### 3. `RandomSelector` (Lines 1–20)
- **File:** `MyTimetable/Planning/RandomSelector.cs`
- **Logic:** Uniform random choice from available subjects. Uses `Random` (injectable via constructor, default `Random.Shared`).
- **Constructor params:** `(Dictionary<string, int> queue, List<Slot> fillable, Random? random = null)`
- **Test considerations:** Deterministic with seeded `Random`, empty queue, single subject, multi-subject distribution.

### 4. `RoundRobinSelector` (Lines 1–25)
- **File:** `MyTimetable/Planning/RoundRobinSelector.cs`
- **Logic:** Cycles through subjects in fixed order (captured at construction from `Queue.Keys`), skipping exhausted ones.
- **Constructor params:** `(Dictionary<string, int> queue, List<Slot> fillable)`
- **Test considerations:** All subjects available, some exhaust mid-cycle, single subject, empty queue, order preservation.

### 5. `LargestQueueFirstSelector` (Lines 1–11)
- **File:** `MyTimetable/Planning/LargestQueueFirstSelector.cs`
- **Logic:** `Available.MaxBy(kv => kv.Value).Key` — picks subject with largest remaining count.
- **Constructor params:** `(Dictionary<string, int> queue, List<Slot> fillable)`
- **Test considerations:** Single line, tie-breaking (MaxBy is stable but picks last max on equal values), empty queue, queue becomes empty mid-plan.

### 6. `SmallestQueueFirstSelector` (Lines 1–11)
- **File:** `MyTimetable/Planning/SmallestQueueFirstSelector.cs`
- **Logic:** `Available.MinBy(kv => kv.Value).Key` — picks subject with smallest remaining count.
- **Constructor params:** `(Dictionary<string, int> queue, List<Slot> fillable)`
- **Test considerations:** Same pattern as LargestQueueFirst but reversed.

### 7. `LeadingChunkGrowthSelector` (Lines 1–53)
- **File:** `MyTimetable/Planning/LeadingChunkGrowthSelector.cs`
- **Logic:** Grows the **first** chunk of occupied slots **backwards** (toward start of day). Only non-empty days where the first chunk has room before it (`Start > 1`). Uses a `PriorityQueue<(int Length, DateOnly Date)>` — picks day with smallest first chunk.
- **Picker:** `RoundRobinPicker` for subject selection.
- **Constructor params:** `(Dictionary<string, int> queue, List<Slot> fillable, int slotCount = 6)`
- **Test considerations:** Single day multi-slot, multiple days with different chunk sizes, day with first chunk at slot 1 (no room), empty days (skipped), all days filled (no room before any chunk).

### 8. `TrailingChunkGrowthSelector` (Lines 1–55)
- **File:** `MyTimetable/Planning/TrailingChunkGrowthSelector.cs`
- **Logic:** Mirror of Leading but grows the **last** chunk **forward** (toward end of day). Only days where last chunk `End < slotCount`. `PriorityQueue<(int Length, DateOnly Date)>`.
- **Picker:** `RoundRobinPicker` for subject selection.
- **Constructor params:** `(Dictionary<string, int> queue, List<Slot> fillable, int slotCount = 6)`
- **Test considerations:** Same structure as Leading but at end of day.

### 9. `WeightedRandomSelector` (Lines 1–30)
- **File:** `MyTimetable/Planning/WeightedRandomSelector.cs`
- **Logic:** Roulette-wheel selection — pick probability proportional to remaining count. Uses `Random` (injectable, default `Random.Shared`).
- **Constructor params:** `(Dictionary<string, int> queue, List<Slot> fillable, Random? random = null)`
- **Test considerations:** Deterministic with seeded `Random`, empty queue, single subject always picked, multi-subject distribution matches weights.

## Architecture & Test Patterns

### Base contract
All strategies extend `PlanningSelectorBase : IPlanningSelector`, override `PickSubject()` (abstract). Some also override `TakeSlot()` (virtual, defaults to first-in-list). The public API is `Plan()` → `IEnumerable<PlannedSlot>`.

### Data flow
```
Queue (Dictionary<string, int>) ──→ PlanningSelectorBase copies it
Fillable (List<Slot>)           ──→ PlanningSelectorBase copies it
Plan() loop:
  Slot? = TakeSlot()            // null → stop
  string? = PickSubject()       // null → stop
  yield return PlannedSlot { Slot, Lesson }
```

### Existing test patterns (from `TestDensePackerSelector`, `TestGapClosingSelector`)
- **Framework:** xUnit (`[Fact]`)
- **Assertions:** `FluentAssertions` (`Should()`, `Be()`, `HaveCount()`, `BeEmpty()`, etc.)
- **Namespace:** `TestMyTimetable.Planning`
- **Class naming:** `Test{ClassName}` (e.g., `TestDensePackerSelector`)
- **Pattern:**
  1. Arrange: create `Dictionary<string, int> queue` + `List<Slot> fillable`
  2. Act: `new Strategy(queue, fillable).Plan().ToList()`
  3. Assert: check count, slot values, lesson titles, dates
- **InternalsVisibleTo:** Test project can see `internal` members via `InternalsVisibleTo` (verified — no reflection needed for `DayLayout.GetDays`)
- **Model types used:** `Slot`, `PlannedSlot`, `CustomLesson` (from `MyTimetable.Models`)

### Key model types
- `Slot { DateOnly Date, int Number }` — a free time slot
- `PlannedSlot { Slot Slot, CustomLesson Lesson }` — result of placement
- `CustomLesson { string LessonType, string Title }` — lesson placed (always `"PRACTICE"` in base)

## Key Dependencies
- `RoundRobinPicker` — used by EmptyDaySeedSelector, LeadingChunkGrowthSelector, TrailingChunkGrowthSelector (internal to Planning namespace)
- `DayLayout` — static helper for day grouping, `GetDays`, `FirstChunk`, `LastChunk` (internal, visible to tests)
- `PlanningSelectorBase` — provides `Available`, `TakeSlot()`, `Plan()`, `Fillable`, `Queue` copies

## Start Here

1. **`MyTimetable/Planning/PlanningSelectorBase.cs`** — Read first to understand the base contract (`Plan()` loop, `TakeSlot()`, `PickSubject()`, `Available`).
2. **`TestMyTimetable/Planning/DensePackerSelector.cs`** — Use as a template for new tests.
3. **`MyTimetable/Planning/FairShareSelector.cs`** — Highest priority for testing (non-trivial fairness logic with deviation calculations).

## Constraints & Risks

- All strategies are `sealed class : PlanningSelectorBase` in namespace `MyTimetable.Planning`
- `PlanningSelectorBase` does **not** reset state between iterations — `Plan()` is a single-use enumerator
- `RoundRobinPicker` is internal; tests may need to verify behavior indirectly via Plan() output
- Stubbed `Random` (seeded instance) is required for deterministic `RandomSelector` and `WeightedRandomSelector` tests
- `slotCount` parameter (default 6) in EmptyDaySeedSelector, LeadingChunkGrowthSelector, TrailingChunkGrowthSelector affects day layout logic

## Acceptance Report

```acceptance-report
{
  "criteriaSatisfied": [
    {
      "id": "criterion-1",
      "status": "satisfied",
      "evidence": "Scouted both MyTimetable/Planning/ and TestMyTimetable/Planning/. Listed all 18 source files, excluded 7 non-strategies and 2 already-tested strategies. Confirmed 9 strategy classes needing tests with file paths, code snippets, and test patterns."
    }
  ],
  "changedFiles": [],
  "testsAddedOrUpdated": [],
  "commandsRun": [
    {
      "command": "ls MyTimetable/Planning/",
      "result": "passed",
      "summary": "Listed 18 source files"
    },
    {
      "command": "ls TestMyTimetable/Planning/",
      "result": "passed",
      "summary": "Listed 2 existing test files"
    },
    {
      "command": "read on all 9 strategy .cs files and existing test files",
      "result": "passed",
      "summary": "Reviewed all strategy implementations and existing test patterns"
    }
  ],
  "validationOutput": [
    "All 9 identified strategy classes inherit from PlanningSelectorBase",
    "All 7 excluded files are correctly non-strategies per the skip list",
    "Existing tests use xUnit + FluentAssertions with Plan().ToList() pattern"
  ],
  "residualRisks": [
    "RoundRobinPicker is internal — tests may need indirect verification via Plan() output",
    "DayLayout helpers (FirstChunk, LastChunk) are internal but accessible via InternalsVisibleTo"
  ],
  "noStagedFiles": true,
  "diffSummary": "No source files changed — this is a scouting-only task. Output written to scout-summary.md and progress.md.",
  "reviewFindings": [
    "no blockers — all strategies clearly scoped and testable"
  ],
  "manualNotes": "9 strategy classes need tests. FairShareSelector is the most complex (deviation minimization). LeadingChunkGrowthSelector and TrailingChunkGrowthSelector are moderately complex (PriorityQueue + chunk logic). The remaining 6 are simple one-liner or straightforward strategies. Test file naming convention: Test{className}.cs in TestMyTimetable/Planning/."
}
```
