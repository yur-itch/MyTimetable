using MyTimetable.Planning;

namespace TestMyTimetable.Planning;

public class TestLargestQueueFirstSelector
{
    private static List<Slot> Slots(int count = 10)
        => Enumerable.Range(1, count)
            .Select(n => new Slot { Date = new DateOnly(2024, 1, 1), Number = n })
            .ToList();

    [Fact]
    public void Plan_EmptyQueue_ReturnsNothing()
    {
        var sel = new LargestQueueFirstSelector(new Dictionary<string, int>(), Slots(5));
        sel.Plan((_, _) => new(true, true)).Should().BeEmpty();
    }

    [Fact]
    public void Plan_EmptyFillable_ReturnsNothing()
    {
        var sel = new LargestQueueFirstSelector(new Dictionary<string, int> { ["A"] = 3 }, []);
        sel.Plan((_, _) => new(true, true)).Should().BeEmpty();
    }

    [Fact]
    public void Plan_AlwaysPicksLargestRemaining()
    {
        // A=4, B=2, C=1
        // Шаг 1: A=4,B=2,C=1 → A(4), Queue: A=3
        // Шаг 2: A=3,B=2,C=1 → A(3), Queue: A=2
        // Шаг 3: A=2,B=2,C=1 → A(2, первый с max=2), Queue: A=1
        // Шаг 4: A=1,B=2,C=1 → B(2), Queue: B=1
        // Шаг 5: A=1,B=1,C=1 → A(1, первый с max=1), Queue: A=0
        // Шаг 6: B=1,C=1 → B(1, первый), Queue: B=0
        // Шаг 7: C=1 → C
        var queue = new Dictionary<string, int> { ["A"] = 4, ["B"] = 2, ["C"] = 1 };
        var sel = new LargestQueueFirstSelector(queue, Slots(7));
        var titles = sel.Plan((_, _) => new(true, true)).Select(p => p.Lesson.Title).ToList();

        titles.Should().Equal(["A", "A", "A", "B", "A", "B", "C"]);
    }

    [Fact]
    public void Plan_LargerRemainingChosenEachStep()
    {
        var queue = new Dictionary<string, int> { ["A"] = 5, ["B"] = 3, ["C"] = 1 };
        var sel = new LargestQueueFirstSelector(queue, Slots(9));
        var titles = sel.Plan((_, _) => new(true, true)).Select(p => p.Lesson.Title).ToList();

        // A=5,B=3,C=1 → A→A=4
        // A=4,B=3,C=1 → A→A=3
        // A=3,B=3,C=1 → A(first with 3)→A=2
        // A=2,B=3,C=1 → B→B=2
        // A=2,B=2,C=1 → A(first with 2)→A=1
        // A=1,B=2,C=1 → B→B=1
        // A=1,B=1,C=1 → A(first with 1)→A=0
        // B=1,C=1 → B(first)→B=0
        // C=1 → C
        titles.Should().Equal(["A", "A", "A", "B", "A", "B", "A", "B", "C"]);
    }

    [Fact]
    public void Plan_SingleSubject_FillsAllSlots()
    {
        var queue = new Dictionary<string, int> { ["Math"] = 4 };
        var sel = new LargestQueueFirstSelector(queue, Slots(4));
        var result = sel.Plan((_, _) => new(true, true)).ToList();
        result.Should().HaveCount(4);
        result.Should().OnlyContain(p => p.Lesson.Title == "Math");
    }

    [Fact]
    public void Plan_StopsWhenQueueExhausted()
    {
        var queue = new Dictionary<string, int> { ["A"] = 2 };
        var sel = new LargestQueueFirstSelector(queue, Slots(10));
        sel.Plan((_, _) => new(true, true)).Should().HaveCount(2);
    }

    [Fact]
    public void Plan_StopsWhenSlotsExhausted()
    {
        var queue = new Dictionary<string, int> { ["A"] = 10 };
        var sel = new LargestQueueFirstSelector(queue, Slots(3));
        sel.Plan((_, _) => new(true, true)).Should().HaveCount(3);
    }

    [Fact]
    public void Plan_AllSubjectsEqual_GetsDictionaryOrder()
    {
        var queue = new Dictionary<string, int> { ["X"] = 3, ["Y"] = 3, ["Z"] = 3 };
        var sel = new LargestQueueFirstSelector(queue, Slots(9));
        var titles = sel.Plan((_, _) => new(true, true)).Select(p => p.Lesson.Title).ToList();

        // Все равны, MaxBy берёт первый в Available:
        // X=3,Y=3,Z=3 → X; X=2,Y=3,Z=3 → Y; X=2,Y=2,Z=3 → Z
        // X=2,Y=2,Z=2 → X; X=1,Y=2,Z=2 → Y; X=1,Y=1,Z=2 → Z
        // X=1,Y=1,Z=1 → X; X=0,Y=1,Z=1 → Y; X=0,Y=0,Z=1 → Z
        titles.Should().Equal(["X", "Y", "Z", "X", "Y", "Z", "X", "Y", "Z"]);
    }

    [Fact]
    public void Plan_OutputHasCorrectSlotAndLessonProperties()
    {
        var queue = new Dictionary<string, int> { ["A"] = 1 };
        var slots = new List<Slot>
        {
            new() { Date = new DateOnly(2024, 5, 10), Number = 3 }
        };
        var result = new LargestQueueFirstSelector(queue, slots).Plan((_, _) => new(true, true)).ToList();

        result.Should().HaveCount(1);
        result[0].Slot.Date.Should().Be(new DateOnly(2024, 5, 10));
        result[0].Slot.Number.Should().Be(3);
        result[0].Lesson.Title.Should().Be("A");
        result[0].Lesson.LessonType.Should().Be("PRACTICE");
    }
}
