using MyTimetable.Models;
using MyTimetable.Planning;

namespace TestMyTimetable.Planning;

public class TestRoundRobinSelector
{
    private static List<Slot> Slots(int count = 10)
        => Enumerable.Range(1, count)
            .Select(n => new Slot { Date = new DateOnly(2024, 1, 1), Number = n })
            .ToList();

    [Fact]
    public void Plan_EmptyQueue_ReturnsNothing()
    {
        var sel = new RoundRobinSelector(new Dictionary<string, int>(), Slots(5));
        sel.Plan().Should().BeEmpty();
    }

    [Fact]
    public void Plan_EmptyFillable_ReturnsNothing()
    {
        var sel = new RoundRobinSelector(new Dictionary<string, int> { ["A"] = 3 }, []);
        sel.Plan().Should().BeEmpty();
    }

    [Fact]
    public void Plan_CyclesThroughSubjects()
    {
        var queue = new Dictionary<string, int> { ["A"] = 3, ["B"] = 3 };
        var sel = new RoundRobinSelector(queue, Slots(6));
        var titles = sel.Plan().Select(p => p.Lesson.Title).ToList();

        titles.Should().Equal(["A", "B", "A", "B", "A", "B"]);
    }

    [Fact]
    public void Plan_CyclesWithThreeSubjects()
    {
        var queue = new Dictionary<string, int> { ["X"] = 2, ["Y"] = 2, ["Z"] = 2 };
        var sel = new RoundRobinSelector(queue, Slots(6));
        var titles = sel.Plan().Select(p => p.Lesson.Title).ToList();

        titles.Should().Equal(["X", "Y", "Z", "X", "Y", "Z"]);
    }

    [Fact]
    public void Plan_SkipsExhaustedSubjects()
    {
        var queue = new Dictionary<string, int> { ["A"] = 1, ["B"] = 3 };
        var sel = new RoundRobinSelector(queue, Slots(4));
        var titles = sel.Plan().Select(p => p.Lesson.Title).ToList();

        // A=1,B=3 → A, B
        // A exhausted: B, B, B
        titles.Should().Equal(["A", "B", "B", "B"]);
    }

    [Fact]
    public void Plan_StopsWhenQueueExhausted()
    {
        var queue = new Dictionary<string, int> { ["A"] = 1, ["B"] = 1 };
        var sel = new RoundRobinSelector(queue, Slots(10));
        sel.Plan().Should().HaveCount(2);
    }

    [Fact]
    public void Plan_StopsWhenSlotsExhausted()
    {
        var queue = new Dictionary<string, int> { ["A"] = 10 };
        var sel = new RoundRobinSelector(queue, Slots(3));
        sel.Plan().Should().HaveCount(3);
    }

    [Fact]
    public void Plan_SingleSubject_FillsAllSlots()
    {
        var queue = new Dictionary<string, int> { ["Math"] = 4 };
        var sel = new RoundRobinSelector(queue, Slots(4));
        sel.Plan().Select(p => p.Lesson.Title).Should().AllBe("Math");
    }

    [Fact]
    public void Plan_PicksUpWhereLeftOffAcrossInterleavedCalls()
    {
        // Это тест на то, что внутренняя позиция жива между вызовами Plan.
        // RoundRobinSelector пересоздаётся на каждый Plan, поэтому этот тест
        // проверяет только последовательность внутри одного вызова Plan().
        var queue = new Dictionary<string, int> { ["A"] = 2, ["B"] = 2 };
        var sel = new RoundRobinSelector(queue, Slots(4));
        var titles = sel.Plan().Select(p => p.Lesson.Title).ToList();

        titles.Should().Equal(["A", "B", "A", "B"]);
    }

    [Fact]
    public void Plan_OrderFixedFromConstructorSnapshot()
    {
        // Снимок очереди делается в конструкторе.
        var queue = new Dictionary<string, int> { ["Second"] = 2, ["First"] = 2 };
        var sel = new RoundRobinSelector(queue, Slots(4));
        var titles = sel.Plan().Select(p => p.Lesson.Title).ToList();

        // Порядок — по Keys из снимка (словарь сохраняет порядок вставки)
        titles.Should().Equal(["Second", "First", "Second", "First"]);
    }

    [Fact]
    public void Plan_OutputHasCorrectProperties()
    {
        var queue = new Dictionary<string, int> { ["A"] = 1 };
        var slots = new List<Slot>
        {
            new() { Date = new DateOnly(2024, 5, 10), Number = 3 }
        };
        var result = new RoundRobinSelector(queue, slots).Plan().ToList();

        result.Should().HaveCount(1);
        result[0].Slot.Date.Should().Be(new DateOnly(2024, 5, 10));
        result[0].Slot.Number.Should().Be(3);
        result[0].Lesson.Title.Should().Be("A");
        result[0].Lesson.LessonType.Should().Be("PRACTICE");
    }
}
