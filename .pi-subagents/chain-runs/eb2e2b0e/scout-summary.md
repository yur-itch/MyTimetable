# Scout Summary: Planning Strategy Classes

## Base Context

- **Interface:** `IPlanningSelector` — single method `IEnumerable<PlannedSlot> Plan()`
- **Abstract base:** `PlanningSelectorBase` — copies queue + fillable on construction, implements Plan loop:
  1. `TakeSlot()` (virtual, default = pop front of fillable)
  2. `PickSubject()` (abstract — each strategy defines this)
  3. Both returning `null` ends the loop
- **Factory:** `PlanningSelectorFactory` — generic wrapper taking a factory delegate
- **Test project:** `TestMyTimetable/Planning/` — only has tests for `GapClosingSelector` and `DensePackerSelector`

## Strategy Classes (11 total)

### Already Tested (2)

| Class | File | Strategy |
|-------|------|----------|
| `GapClosingSelector` | `MyTimetable/Planning/GapClosingSelector.cs` | Fills gaps in partially-occupied days before opening new days |
| `DensePackerSelector` | `MyTimetable/Planning/DensePackerSelector.cs` | Packs lessons into first available slots |

### Untested (9)

| # | Class | File | Strategy |
|---|-------|------|----------|
| 1 | `EmptyDaySeedSelector` | `MyTimetable/Planning/EmptyDaySeedSelector.cs` | Places one lesson into each **empty** day (no occupied pairs), first free pair of that day. Each empty day touched exactly once. Uses `RoundRobinPicker` for subject selection. |
| 2 | `FairShareSelector` | `MyTimetable/Planning/FairShareSelector.cs` | Picks subject so that post-placement proportions stay as close as possible to initial queue proportions. Minimizes sum of squared deviations (largest remainder method style). |
| 3 | `LargestQueueFirstSelector` | `MyTimetable/Planning/LargestQueueFirstSelector.cs` | Always picks the subject with the largest remaining count (`Available.MaxBy`). |
| 4 | `LeadingChunkGrowthSelector` | `MyTimetable/Planning/LeadingChunkGrowthSelector.cs` | Grows the **shortest first chunk** of occupied pairs in non-empty days. Places lesson in the free pair immediately **before** the chunk (grows toward start of day). Uses a priority queue keyed by `(chunk length, date)`. Uses `RoundRobinPicker` for subject. |
| 5 | `RandomSelector` | `MyTimetable/Planning/RandomSelector.cs` | Uniform random selection among available subjects. Accepts optional `Random` seed. |
| 6 | `RoundRobinSelector` | `MyTimetable/Planning/RoundRobinSelector.cs` | Cycles through subjects in fixed order, skipping exhausted ones. |
| 7 | `SmallestQueueFirstSelector` | `MyTimetable/Planning/SmallestQueueFirstSelector.cs` | Always picks the subject with the smallest remaining count (`Available.MinBy`). |
| 8 | `TrailingChunkGrowthSelector` | `MyTimetable/Planning/TrailingChunkGrowthSelector.cs` | Mirror of `LeadingChunkGrowthSelector` for the **end of day**. Grows the shortest **last chunk** of occupied pairs by placing lessons immediately **after** it. Uses priority queue + `RoundRobinPicker`. |
| 9 | `WeightedRandomSelector` | `MyTimetable/Planning/WeightedRandomSelector.cs` | Random selection weighted by remaining queue count (roulette wheel). Accepts optional `Random` seed. |

## Classification by Subject-Selection Strategy

| Subject Selection | Strategy Classes |
|---|---|
| **RoundRobinPicker** (delegate) | `EmptyDaySeedSelector`, `LeadingChunkGrowthSelector`, `TrailingChunkGrowthSelector` |
| **RoundRobinSelector** (self-contained) | `RoundRobinSelector` |
| **Largest remaining** | `LargestQueueFirstSelector` |
| **Smallest remaining** | `SmallestQueueFirstSelector` |
| **Proportional (least-squares)** | `FairShareSelector` |
| **Uniform random** | `RandomSelector` |
| **Weighted random** | `WeightedRandomSelector` |
| **Fixed by base class** | `DensePackerSelector`, `GapClosingSelector` |

## Classification by Slot-Selection Strategy

| Slot Selection | Strategy Classes |
|---|---|
| **Default (pop front)** | `LargestQueueFirstSelector`, `SmallestQueueFirstSelector`, `RandomSelector`, `RoundRobinSelector`, `WeightedRandomSelector`, `FairShareSelector` |
| **Fill gaps** | `GapClosingSelector` |
| **First free per day** | `DensePackerSelector` |
| **Each empty day once** | `EmptyDaySeedSelector` |
| **Grow shortest leading chunk** | `LeadingChunkGrowthSelector` |
| **Grow shortest trailing chunk** | `TrailingChunkGrowthSelector` |

## Key Dependencies

- All strategies depend on `MyTimetable.Models` (`Slot`, `CustomLesson`, date types)
- `EmptyDaySeedSelector`, `LeadingChunkGrowthSelector`, `TrailingChunkGrowthSelector` use `DayLayout` helper
- `EmptyDaySeedSelector`, `LeadingChunkGrowthSelector`, `TrailingChunkGrowthSelector` use `RoundRobinPicker` for subject selection
- `PlanningSelectorFactory` uses `IPlanningSelectorFactory` interface

## Files That Are Likely to Need Changes for New Strategy

1. **`MyTimetable/Planning/<NewStrategy>.cs`** — implement the new class extending `PlanningSelectorBase`
2. **`MyTimetable/Planning/PlanningSelectorFactory.cs`** — if adding factory method/registration for the new strategy
3. **`TestMyTimetable/Planning/<NewStrategy>Tests.cs`** — add tests
4. Possibly **`DayLayout.cs`** if new slot-access pattern needs support

## Open Questions / Constraints

- `EmptyDaySeedSelector` and the chunk-growth selectors hardcode `slotCount = 6` as default; `TrailingChunkGrowthSelector` stores it as a field. Are there plans to make slot count configurable?
- `FairShareSelector` captures desired proportions from the initial queue snapshot only. If a subject is missing from the initial queue, it won't be placed even if added later — is this by design?
- No strategies currently handle edge cases like empty queue, empty fillable, or single-item fillable differently — all rely on the base `Plan()` loop terminating on `null`.
