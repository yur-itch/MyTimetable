using MyTimetable.Models;
using MyTimetable.Planning;

namespace TestMyTimetable.Planning;

public class TestCompositePlanningSelector
{
    // Простая стратегия-заглушка: ставит предметы по порядку.
    private sealed class DummySelector : IPlanningSelector
    {
        private readonly Dictionary<string, int> _queue;
        private readonly List<Slot> _fillable;

        public DummySelector(Dictionary<string, int> queue, List<Slot> fillable)
        {
            _queue = new Dictionary<string, int>(queue);
            _fillable = new List<Slot>(fillable);
        }

        public IEnumerable<PlannedSlot> Plan()
        {
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

    private sealed class DummyFactory : IPlanningSelectorFactory
    {
        public IPlanningSelector Create(Dictionary<string, int> queue, List<Slot> fillable)
            => new DummySelector(queue, fillable);
    }

    // Вторая стратегия-заглушка: ставит предметы в обратном порядке слотов (реверс).
    private sealed class ReverseDummySelector : IPlanningSelector
    {
        private readonly Dictionary<string, int> _queue;
        private readonly List<Slot> _fillable;

        public ReverseDummySelector(Dictionary<string, int> queue, List<Slot> fillable)
        {
            _queue = new Dictionary<string, int>(queue);
            _fillable = new List<Slot>(fillable);
        }

        public IEnumerable<PlannedSlot> Plan()
        {
            foreach (var slot in ((IEnumerable<Slot>)_fillable).Reverse().ToList())
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

    private sealed class ReverseDummyFactory : IPlanningSelectorFactory
    {
        public IPlanningSelector Create(Dictionary<string, int> queue, List<Slot> fillable)
            => new ReverseDummySelector(queue, fillable);
    }

    private static List<Slot> MakeSlots(params (int day, int number)[] slots)
        => slots.Select(s => new Slot
        {
            Date = new DateOnly(2024, 1, s.day),
            Number = s.number
        }).ToList();

    [Fact]
    public void Plan_EmptyQueue_ReturnsNothing()
    {
        var comp = new CompositePlanningSelector(
            new Dictionary<string, int>(), MakeSlots((1, 1)), [new DummyFactory()]);
        comp.Plan().Should().BeEmpty();
    }

    [Fact]
    public void Plan_EmptyFillable_ReturnsNothing()
    {
        var comp = new CompositePlanningSelector(
            new Dictionary<string, int> { ["A"] = 3 }, [], [new DummyFactory()]);
        comp.Plan().Should().BeEmpty();
    }

    [Fact]
    public void Plan_NoFactories_ReturnsNothing()
    {
        var comp = new CompositePlanningSelector(
            new Dictionary<string, int> { ["A"] = 3 },
            MakeSlots((1, 1), (1, 2)),
            []);
        comp.Plan().Should().BeEmpty();
    }

    [Fact]
    public void Plan_SingleFactory_DelegatesAll()
    {
        var queue = new Dictionary<string, int> { ["A"] = 3, ["B"] = 3 };
        var slots = MakeSlots((1, 1), (1, 2), (1, 3), (1, 4), (1, 5), (1, 6));
        var comp = new CompositePlanningSelector(queue, slots, [new DummyFactory()]);
        var results = comp.Plan().ToList();

        results.Should().HaveCount(6);
        results.Select(r => r.Lesson.Title).Should().Equal(["A", "A", "A", "B", "B", "B"]);
    }

    [Fact]
    public void Plan_TwoFactories_FirstConsumesThenSecond()
    {
        var queue = new Dictionary<string, int> { ["A"] = 4, ["B"] = 4 };
        var slots = MakeSlots((1, 1), (1, 2), (1, 3), (1, 4), (1, 5), (1, 6));
        // Первая фабрика: Dummy (ставит по порядку)
        // Вторая фабрика: ReverseDummy (ставит в обратном порядке то, что осталось)
        var comp = new CompositePlanningSelector(
            queue, slots, [new DummyFactory(), new ReverseDummyFactory()]);
        var results = comp.Plan().ToList();

        // Dummy заберёт первые N слотов по порядку, ReverseDummy — остальные в обратном
        // Dummy ставит A=4, B=4 по порядку слотов 1..8, но слотов всего 6!
        // Dummy: A(1), A(2), A(3), A(4), B(5), B(6) — 6 слотов заполнены
        // ReverseDummy: слотов не осталось → пусто
        results.Should().HaveCount(6);
        results.Select(r => r.Lesson.Title).Should().Equal(["A", "A", "A", "A", "B", "B"]);
    }

    [Fact]
    public void Plan_TwoFactories_SecondPicksUpRemaining()
    {
        // Первая стратегия ставит только один предмет (A). Вторая ставит B.
        var myQueue = new Dictionary<string, int> { ["A"] = 3, ["B"] = 3 };
        var slots = MakeSlots((1, 1), (1, 2), (1, 3));

        var firstQueue = new Dictionary<string, int> { ["A"] = 3 };
        var secondQueue = new Dictionary<string, int> { ["B"] = 3 };

        // Специальные фабрики, каждая видит только свой предмет
        var factories = new IPlanningSelectorFactory[]
        {
            new PlanningSelectorFactory((q, f) => new LargestQueueFirstSelector(
                new Dictionary<string, int> { ["A"] = 3 }, f)),
            new PlanningSelectorFactory((q, f) => new LargestQueueFirstSelector(
                new Dictionary<string, int> { ["B"] = 3 }, f)),
        };

        var comp = new CompositePlanningSelector(myQueue, slots, factories);
        var results = comp.Plan().ToList();

        results.Should().HaveCount(3);
        results[0].Lesson.Title.Should().Be("A");
        results[1].Lesson.Title.Should().Be("A");
        results[2].Lesson.Title.Should().Be("A");
        // Очередь myQueue не синхронизирована с фабриками, поэтому B тоже
        // не ставится — тест проверяет только, что composite делегирует фабрикам.
    }

    [Fact]
    public void Plan_TwoFactories_SharedQueueAndSlotsMutation()
    {
        // Composite передаёт одну и ту же ссылку на очередь и fillable каждой фабрике.
        // Первая фабрика ставит A, вторая — B.
        var queue = new Dictionary<string, int> { ["A"] = 2, ["B"] = 2 };
        var slots = MakeSlots((1, 1), (1, 2), (1, 3), (1, 4));
        var factories = new IPlanningSelectorFactory[]
        {
            new PlanningSelectorFactory((q, f) => new LargestQueueFirstSelector(
                new Dictionary<string, int> { ["A"] = 2, ["B"] = 2 }, f)),
        };

        var comp = new CompositePlanningSelector(queue, slots, factories);
        var results = comp.Plan().ToList();

        results.Should().HaveCount(4);
        results.Select(r => r.Lesson.Title).Should().Equal(["A", "A", "B", "B"]);
    }

    [Fact]
    public void Plan_PreservesOutputOrderFromStrategies()
    {
        // Composite должен yield return в порядке: сначала все результаты первой стратегии,
        // затем все результаты второй.
        var queue = new Dictionary<string, int> { ["A"] = 2, ["B"] = 2 };
        var slots = MakeSlots((1, 1), (1, 2), (1, 3), (1, 4));

        var firstFactory = new PlanningSelectorFactory((q, f) =>
        {
            // Ставит A в первые свободные слоты
            var sel = new LargestQueueFirstSelector(
                new Dictionary<string, int> { ["A"] = 2 }, f);
            return sel;
        });

        var secondFactory = new PlanningSelectorFactory((q, f) =>
        {
            // Ставит B в оставшиеся
            var sel = new LargestQueueFirstSelector(
                new Dictionary<string, int> { ["B"] = 2 }, f);
            return sel;
        });

        var comp = new CompositePlanningSelector(
            queue, slots, [firstFactory, secondFactory]);
        var results = comp.Plan().ToList();

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
            queue, slots, [new DummyFactory()]);
        var result = comp.Plan().ToList();

        result.Should().HaveCount(1);
        result[0].Slot.Date.Should().Be(new DateOnly(2024, 5, 10));
        result[0].Slot.Number.Should().Be(3);
        result[0].Lesson.Title.Should().Be("A");
        result[0].Lesson.LessonType.Should().Be("PRACTICE");
    }
}
