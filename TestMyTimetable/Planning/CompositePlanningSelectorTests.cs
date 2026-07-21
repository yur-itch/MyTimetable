using MyTimetable.Models;
using MyTimetable.Planning;

namespace TestMyTimetable.Planning;

public class TestCompositePlanningSelector
{
    private sealed class SimpleSelector : IPlanningSelector
    {
        private readonly Dictionary<string, int> _queue;
        private readonly List<Slot> _fillable;

        public SimpleSelector(Dictionary<string, int> queue, List<Slot> fillable)
        {
            _queue = new Dictionary<string, int>(queue);
            _fillable = new List<Slot>(fillable);
        }

        public IEnumerable<PlannedSlot> Plan(Func<Slot, string, ProposeResult> accept)
        {
            _ = accept;
            foreach (var slot in _fillable.ToList())
            {
                var available = _queue.Where(kv => kv.Value > 0).ToList();
                if (available.Count == 0) yield break;
                string key = available[0].Key;
                _queue[key]--;
                _fillable.Remove(slot);
                yield return new PlannedSlot
                {
                    Slot = slot,
                    Lesson = new CustomLesson { LessonType = "PRACTICE", Title = key }
                };
            }
        }
    }

    private sealed class SimpleFactory : IPlanningSelectorFactory
    {
        public IPlanningSelector Create(Dictionary<string, int> queue, List<Slot> fillable)
            => new SimpleSelector(queue, fillable);
    }

    private static List<Slot> MakeSlots(params (int day, int number)[] slots)
        => slots.Select(s => new Slot
        {
            Date = new DateOnly(2024, 1, s.day),
            Number = s.number
        }).ToList();

    private static Func<Slot, string, ProposeResult> AcceptAll => (_, _) => new(true, true);

    [Fact]
    public void Plan_EmptyQueue_ReturnsNothing()
    {
        var comp = new CompositePlanningSelector(
            new Dictionary<string, int>(), MakeSlots((1, 1)), [new SimpleFactory()]);
        comp.Plan(AcceptAll).Should().BeEmpty();
    }

    [Fact]
    public void Plan_EmptyFillable_ReturnsNothing()
    {
        var comp = new CompositePlanningSelector(
            new Dictionary<string, int> { ["A"] = 3 }, [], [new SimpleFactory()]);
        comp.Plan(AcceptAll).Should().BeEmpty();
    }

    [Fact]
    public void Plan_NoFactories_ReturnsNothing()
    {
        var comp = new CompositePlanningSelector(
            new Dictionary<string, int> { ["A"] = 3 },
            MakeSlots((1, 1), (1, 2)),
            []);
        comp.Plan(AcceptAll).Should().BeEmpty();
    }

    [Fact]
    public void Plan_SingleFactory_DelegatesAll()
    {
        var queue = new Dictionary<string, int> { ["A"] = 3, ["B"] = 3 };
        var slots = MakeSlots((1, 1), (1, 2), (1, 3), (1, 4), (1, 5), (1, 6));
        var comp = new CompositePlanningSelector(queue, slots, [new SimpleFactory()]);
        var results = comp.Plan(AcceptAll).ToList();

        results.Should().HaveCount(6);
        results.Select(r => r.Lesson.Title).Should().Equal(["A", "A", "A", "B", "B", "B"]);
    }

    [Fact]
    public void Plan_TwoFactories_FirstConsumesSlotsThenSecond()
    {
        var queue = new Dictionary<string, int> { ["A"] = 4, ["B"] = 4 };
        var slots = MakeSlots((1, 1), (1, 2), (1, 3), (1, 4), (1, 5), (1, 6));
        var comp = new CompositePlanningSelector(
            queue, slots, [new SimpleFactory(), new SimpleFactory()]);
        var results = comp.Plan(AcceptAll).ToList();

        // Первый SimpleSelector: A=4 → 4 слота, B=4 → 2 слота (всего 6)
        // Второй SimpleSelector: слотов не осталось → пусто
        results.Should().HaveCount(6);
        results.Select(r => r.Lesson.Title).Should().Equal(["A", "A", "A", "A", "B", "B"]);
    }

    [Fact]
    public void Plan_TwoDifferentStrategies_OrderedByFactory()
    {
        // Первая фабрика ставит A в первые слоты, вторая — B в оставшиеся
        var queue = new Dictionary<string, int> { ["A"] = 2, ["B"] = 2 };
        var slots = MakeSlots((1, 1), (1, 2), (1, 3), (1, 4));

        var firstFactory = new PlanningSelectorFactory((q, f) =>
            new LargestQueueFirstSelector(
                new Dictionary<string, int> { ["A"] = 2 }, f));

        var secondFactory = new PlanningSelectorFactory((q, f) =>
            new LargestQueueFirstSelector(
                new Dictionary<string, int> { ["B"] = 2 }, f));

        var comp = new CompositePlanningSelector(
            queue, slots, [firstFactory, secondFactory]);
        var results = comp.Plan(AcceptAll).ToList();

        results.Should().HaveCount(4);
        results[0].Lesson.Title.Should().Be("A");
        results[1].Lesson.Title.Should().Be("A");
        results[2].Lesson.Title.Should().Be("B");
        results[3].Lesson.Title.Should().Be("B");
    }

    [Fact]
    public void Plan_OutputHasCorrectProperties()
    {
        var queue = new Dictionary<string, int> { ["A"] = 1 };
        var slots = new List<Slot>
        {
            new() { Date = new DateOnly(2024, 5, 10), Number = 3 }
        };
        var comp = new CompositePlanningSelector(
            queue, slots, [new SimpleFactory()]);
        var result = comp.Plan(AcceptAll).ToList();

        result.Should().HaveCount(1);
        result[0].Slot.Date.Should().Be(new DateOnly(2024, 5, 10));
        result[0].Slot.Number.Should().Be(3);
        result[0].Lesson.Title.Should().Be("A");
        result[0].Lesson.LessonType.Should().Be("PRACTICE");
    }
}
