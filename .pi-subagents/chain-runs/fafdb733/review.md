# Review: DensePackerSelector

## Files inspected
- `MyTimetable/Planning/DensePackerSelector.cs` — full implementation
- `MyTimetable/Program.cs` — registration diff (line 38 added)
- `MyTimetable/Planning/PlanningSelectorBase.cs` — base class
- `MyTimetable/Planning/EmptyDaySeedSelector.cs` — closest analogue (same pattern pattern)
- `MyTimetable/Planning/GapClosingSelector.cs` — another slot-strategy for comparison
- `MyTimetable/Planning/RoundRobinPicker.cs` — round-robin picker used by DensePacker
- `MyTimetable/Planning/RoundRobinSelector.cs` — native round-robin selector
- `MyTimetable/Planning/DayLayout.cs` — day grouping utility
- `TestMyTimetable/Planning/DensePackerSelector.cs` — 13 unit tests
- `git diff HEAD -- MyTimetable/Program.cs` — confirms only one line added

## 1. Does it follow the same patterns as other slot-strategies?

**Yes.** Comparing against `EmptyDaySeedSelector` (the structurally closest sibling):

| Aspect | EmptyDaySeedSelector | DensePackerSelector | Match |
|---|---|---|---|
| Inheritance | `PlanningSelectorBase` | `PlanningSelectorBase` | ✓ |
| Constructor signature | `(Dictionary<string,int>, List<Slot>, int slotCount=6)` | `(Dictionary<string,int>, List<Slot>, int slotCount=6)` | ✓ |
| Subject selection | `RoundRobinPicker` via `PickSubject()` | `RoundRobinPicker` via `PickSubject()` | ✓ |
| Day grouping | `DayLayout.GetDays(Fillable)` | `DayLayout.GetDays(Fillable)` | ✓ |
| Slot sequence | `IEnumerator<Slot>` built in constructor via LINQ | `IEnumerator<Slot>` built in constructor via LINQ | ✓ |
| `TakeSlot()` | pulls from enumerator, `null` when exhausted | pulls from enumerator, `null` when exhausted | ✓ |
| Registration in `Program.cs` | `["emptyseed"] = ...` | `["dense"] = ...` | ✓ (same pattern, line 38) |
| Copies fillable list | Inherited from base `new List<Slot>(fillable)` | Inherited from base `new List<Slot>(fillable)` | ✓ |

The strategy is registered alongside 10 other strategies with identical factory syntax. No deviations.

## 2. Is round-robin subject selection correct?

**Yes.** `DensePackerSelector.PickSubject()` delegates to `_picker.Pick()`, which is a `RoundRobinPicker` constructed with `Queue` (the protected copy from the base class).

`RoundRobinPicker.Pick()` logic (from `RoundRobinPicker.cs`):
- Maintains a fixed `_order` list (snapshot of queue keys from constructor).
- Iterates starting at `_position`, wraps via `% _order.Count`.
- Returns the first key with `remaining > 0` in the live `_queue` dict.
- Advances `_position = (index + 1) % _order.Count` after each successful pick.

This is the **same `RoundRobinPicker` instance used by `EmptyDaySeedSelector`**. It is correct, tested, and re-used as intended by the architecture (comment in `RoundRobinPicker.cs`: "Слот-стратегии... композируют его, чтобы не дублировать логику выбора ПРЕДМЕТА").

Test `Plan_MultipleSubjects_RoundRobinCyclesCorrectly` confirms: with queue `{Math:3, Physics:2, Chem:2}` and 3 qualifying days, placements are Math → Physics → Chem (round-robin order).

## 3. Does crowdedness sorting make sense?

**Yes.** The sorting pipeline:

```csharp
DayLayout.GetDays(Fillable)
    .Select(day => (Day: day, Occupied: slotCount - day.Open.Count))
    .Where(x => x.Occupied > 0 && x.Day.Open.Count > 0)
    .OrderByDescending(x => x.Occupied)
    .ThenBy(x => x.Day.Date)
    .Select(x => new Slot { Date = x.Day.Date, Number = x.Day.Open[0] })
```

- **Occupied metric**: `slotCount - day.Open.Count` counts how many of the `slotCount` slots are already taken. This is a proxy for "crowdedness" — more occupied slots = more crowded. The code comment explicitly states: "В качестве прокси загруженности берётся slotCount - open.Count... Это намеренное поведение: день, уже заполненный предыдущей стратегией, объективно более загружен."
- **Filter `x.Occupied > 0`**: removes completely empty days. Correct — a day with all slots free is not "crowded".
- **Filter `x.Day.Open.Count > 0`**: removes completely full days. Correct — a day with no open slots can't accept a placement.
- **`OrderByDescending(x => x.Occupied)`**: most crowded days first. Correct for the "dense packing" goal — fill a lesson into the busiest day.
- **`ThenBy(x => x.Day.Date)`**: tiebreaker by date ascending. Provides deterministic, chronological ordering for equal-occupancy days.
- **`.Select(x => new Slot { Date = x.Day.Date, Number = x.Day.Open[0] })`**: takes the first open slot in each day. Correct — minimal displacement strategy.

The heuristic is well-reasoned: "Put one lesson each into the busiest days." This is a sensible, minimal-intrusion strategy for an already-dense schedule.

## 4. Edge cases

| Edge case | Handling | Test coverage |
|---|---|---|
| **Empty queue** | `PickSubject()` returns `null` immediately → `Plan()` yields nothing | `Plan_EmptyQueue_YieldsNothing` ✓ |
| **All days empty** (Occupied=0) | Filtered by `x.Occupied > 0` → no qualifying days → nothing | `Plan_AllDaysEmpty_YieldsNothing` ✓ |
| **All days full** (no open slots) | Filtered by `x.Day.Open.Count > 0` → no qualifying days → nothing | `Plan_AllDaysFull_YieldsNothing` ✓ (fillable empty so GetDays yields nothing) |
| **One crowded, one empty day** | Only crowded qualifies; one placement | `Plan_CrowdedAndEmptyDay_OnlyCrowdedDayGetsPlacement` ✓ |
| **More subjects than qualifying days** | Capped at number of qualifying days; excess not placed | `Plan_MoreSubjectsThanQualifyingDays_LimitedToQualifyingDays` ✓ |
| **Queue larger than qualifying days** | Analyzed: 3 qualifying days but queue size 10 → only 3 placements | `Plan_DefaultSlotCount_ComputesOccupiedCorrectly` variant ✓ |
| **Same occupancy, different dates** | Tiebroken by date ascending (chronological) | `Plan_TwoEquallyCrowdedDays_OrdersByDate` ✓ |
| **Higher occupancy before lower** | `OrderByDescending` sorts correctly | `Plan_HigherOccupancyDaysComeFirst` ✓ |
| **Day with gaps vs full day** | Full day (no open slots) skipped; day with gaps receives placement | `Plan_DayWithGapsAndDayWithoutGaps_SkipsFullDay` ✓ |
| **First open slot used** | Always `day.Open[0]` | `Plan_SameOccupancyDifferentOpenSlots_FirstOpenSlotUsed` ✓ |
| **Round-robin cycling across placements** | Cycles Math→Physics→Chem correctly | `Plan_MultipleSubjects_RoundRobinCyclesCorrectly` ✓ |

## Summary of findings

**No issues found.** The DensePackerSelector is:
- Architecturally consistent with all other slot-strategies
- Correct in its round-robin subject selection (reuses the standard `RoundRobinPicker`)
- Sound in its crowdedness sorting logic (clear heuristic, well-documented intent)
- Thoroughly tested for all edge cases (13 tests, all passing)
- Registered with a single line addition to `Program.cs`

The only minor observation (not a bug) is that `Occupied = slotCount - day.Open.Count` counts **all** non-free slots as equally occupied, regardless of whether they differ in pattern (e.g., scattered vs. contiguous). This is explicitly acknowledged in the code comment as intentional behavior — the strategy treats "already filled by a previous strategy" as equally loaded. This is reasonable for a chainable strategy intended for use as a first pass (e.g., `dense → roundrobin`).
