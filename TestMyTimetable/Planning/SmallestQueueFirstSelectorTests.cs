using MyTimetable.Models;
using MyTimetable.Planning;

namespace TestMyTimetable.Planning;

public class TestSmallestQueueFirstSelector
{
    private static List<Slot> Slots(int count = 10)
        => Enumerable.Range(1, count)
            .Select(n => new Slot { Date = new DateOnly(2024, 1, 1), Number = n })
            .ToList();

    [Fact]
    public void Plan_EmptyQueue_ReturnsNothing()
    {
        var sel = new SmallestQueueFirstSelector(new Dictionary<string, int>(), Slots(5));
        sel.Plan().Should().BeEmpty();
    }

    [Fact]
    public void Plan_EmptyFillable_ReturnsNothing()
    {
        var sel = new SmallestQueueFirstSelector(new Dictionary<string, int> { ["A"] = 3 }, []);
        sel.Plan().Should().BeEmpty();
    }

    [Fact]
    public void Plan_AlwaysPicksSmallestRemaining()
    {
        // A=5, B=3, C=1
        // C(1) → C exhausted
        // B(3) vs A(5) → B (3)
        // B(2) vs A(5) → B (2)
        // B(1) vs A(5) → B (1)
        // B exhausted → A(5) → A repeatedly
        var queue = new Dictionary<string, int> { ["A"] = 5, ["B"] = 3, ["C"] = 1 };
        var sel = new SmallestQueueFirstSelector(queue, Slots(9));
        var titles = sel.Plan().Select(p => p.Lesson.Title).ToList();

        titles.Should().Equal(["C", "B", "B", "B", "A", "A", "A", "A", "A"]);
    }

    [Fact]
    public void Plan_SingleSubject_FillsAllSlots()
    {
        var queue = new Dictionary<string, int> { ["Math"] = 4 };
        var sel = new SmallestQueueFirstSelector(queue, Slots(4));
        sel.Plan().Select(p => p.Lesson.Title).Should().AllBe("Math");
    }

    [Fact]
    public void Plan_StopsWhenQueueExhausted()
    {
        var queue = new Dictionary<string, int> { ["A"] = 2 };
        var sel = new SmallestQueueFirstSelector(queue, Slots(10));
        sel.Plan().Should().HaveCount(2);
    }

    [Fact]
    public void Plan_StopsWhenSlotsExhausted()
    {
        var queue = new Dictionary<string, int> { ["A"] = 10 };
        var sel = new SmallestQueueFirstSelector(queue, Slots(3));
        sel.Plan().Should().HaveCount(3);
    }

    [Fact]
    public void Plan_AllSubjectsEqual_GetsDictionaryOrder()
    {
        // Все равны → MinBy возвращает первый в словаре
        var queue = new Dictionary<string, int> { ["X"] = 3, ["Y"] = 3, ["Z"] = 3 };
        var sel = new SmallestQueueFirstSelector(queue, Slots(9));
        var titles = sel.Plan().Select(p => p.Lesson.Title).ToList();

        // Шаг 1: X=3,Y=3,Z=3 → X (первый min)
        // Шаг 2: X=2,Y=3,Z=3 → X (2)
        // Шаг 3: X=1,Y=3,Z=3 → X (1)
        // Шаг 4: X=0 → X exhausted, Y=3,Z=3 → Y
        // Шаг 5: Y=2,Z=3 → Y
        // Шаг 6: Y=1,Z=3 → Y
        // Шаг 7: Y=0 → Z=3
        // Шаг 8: Z=2
        // Шаг 9: Z=1
        titles.Should().Equal(["X", "X", "X", "Y", "Y", "Y", "Z", "Z", "Z"]);
    }

    [Fact]
    public void Plan_RespectsTiesViaDictionaryOrder()
    {
        // A=2, B=2, C=1
        // C(1) → C exhausted
        // A=2,B=2 → A (first)
        // A=1,B=2 → A (1)
        // A exhausted → B=2
        var queue = new Dictionary<string, int> { ["A"] = 2, ["B"] = 2, ["C"] = 1 };
        var sel = new SmallestQueueFirstSelector(queue, Slots(5));
        var titles = sel.Plan().Select(p => p.Lesson.Title).ToList();

        titles.Should().Equal(["C", "A", "A", "B", "B"]);
    }

    [Fact]
    public void Plan_OutputHasCorrectSlotAndLessonProperties()
    {
        var queue = new Dictionary<string, int> { ["A"] = 1 };
        var slots = new List<Slot>
        {
            new() { Date = new DateOnly(2024, 5, 10), Number = 3 }
        };
        var result = new SmallestQueueFirstSelector(queue, slots).Plan().ToList();

        result.Should().HaveCount(1);
        result[0].Slot.Date.Should().Be(new DateOnly(2024, 5, 10));
        result[0].Slot.Number.Should().Be(3);
        result[0].Lesson.Title.Should().Be("A");
        result[0].Lesson.LessonType.Should().Be("PRACTICE");
    }
}
