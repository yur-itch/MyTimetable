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
        sel.Plan((_, _) => new(true, true)).Should().BeEmpty();
    }

    [Fact]
    public void Plan_EmptyFillable_ReturnsNothing()
    {
        var sel = new SmallestQueueFirstSelector(new Dictionary<string, int> { ["A"] = 3 }, []);
        sel.Plan((_, _) => new(true, true)).Should().BeEmpty();
    }

    [Fact]
    public void Plan_AlwaysPicksSmallestRemaining()
    {
        var queue = new Dictionary<string, int> { ["A"] = 5, ["B"] = 3, ["C"] = 1 };
        var sel = new SmallestQueueFirstSelector(queue, Slots(9));
        var titles = sel.Plan((_, _) => new(true, true)).Select(p => p.Lesson.Title).ToList();

        // C=1 → C; B=3 → B,B,B; A=5 → A,A,A,A,A
        titles.Should().Equal(["C", "B", "B", "B", "A", "A", "A", "A", "A"]);
    }

    [Fact]
    public void Plan_SingleSubject_FillsAllSlots()
    {
        var queue = new Dictionary<string, int> { ["Math"] = 4 };
        var sel = new SmallestQueueFirstSelector(queue, Slots(4));
        var result = sel.Plan((_, _) => new(true, true)).ToList();
        result.Should().HaveCount(4);
        result.Should().OnlyContain(p => p.Lesson.Title == "Math");
    }

    [Fact]
    public void Plan_StopsWhenQueueExhausted()
    {
        var queue = new Dictionary<string, int> { ["A"] = 2 };
        var sel = new SmallestQueueFirstSelector(queue, Slots(10));
        sel.Plan((_, _) => new(true, true)).Should().HaveCount(2);
    }

    [Fact]
    public void Plan_StopsWhenSlotsExhausted()
    {
        var queue = new Dictionary<string, int> { ["A"] = 10 };
        var sel = new SmallestQueueFirstSelector(queue, Slots(3));
        sel.Plan((_, _) => new(true, true)).Should().HaveCount(3);
    }

    [Fact]
    public void Plan_AllSubjectsEqual_GetsDictionaryOrder()
    {
        var queue = new Dictionary<string, int> { ["X"] = 3, ["Y"] = 3, ["Z"] = 3 };
        var sel = new SmallestQueueFirstSelector(queue, Slots(9));
        var titles = sel.Plan((_, _) => new(true, true)).Select(p => p.Lesson.Title).ToList();

        // Все равны, MinBy берёт первый в списке Available:
        // X=3 → X, X=2 → X, X=1 → X, X exhausted
        // Y=3 → Y, Y=2 → Y, Y=1 → Y, Y exhausted
        // Z=3 → Z, Z=2 → Z, Z=1 → Z
        titles.Should().Equal(["X", "X", "X", "Y", "Y", "Y", "Z", "Z", "Z"]);
    }

    [Fact]
    public void Plan_RespectsTiesViaDictionaryOrder()
    {
        var queue = new Dictionary<string, int> { ["A"] = 2, ["B"] = 2, ["C"] = 1 };
        var sel = new SmallestQueueFirstSelector(queue, Slots(5));
        var titles = sel.Plan((_, _) => new(true, true)).Select(p => p.Lesson.Title).ToList();

        // C=1 → C; A=2,B=2 → A (first min); A=1,B=2 → A; A exhausted → B=2,B=2
        titles.Should().Equal(["C", "A", "A", "B", "B"]);
    }

    [Fact]
    public void Plan_OutputHasCorrectProperties()
    {
        var queue = new Dictionary<string, int> { ["A"] = 1 };
        var slots = new List<Slot>
        {
            new() { Date = new DateOnly(2024, 5, 10), Number = 3 }
        };
        var result = new SmallestQueueFirstSelector(queue, slots).Plan((_, _) => new(true, true)).ToList();

        result.Should().HaveCount(1);
        result[0].Slot.Date.Should().Be(new DateOnly(2024, 5, 10));
        result[0].Slot.Number.Should().Be(3);
        result[0].Lesson.Title.Should().Be("A");
        result[0].Lesson.LessonType.Should().Be("PRACTICE");
    }
}
