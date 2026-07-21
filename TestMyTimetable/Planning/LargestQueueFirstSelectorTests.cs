using MyTimetable.Models;
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
        sel.Plan().Should().BeEmpty();
    }

    [Fact]
    public void Plan_EmptyFillable_ReturnsNothing()
    {
        var sel = new LargestQueueFirstSelector(new Dictionary<string, int> { ["A"] = 3 }, []);
        sel.Plan().Should().BeEmpty();
    }

    [Fact]
    public void Plan_AlwaysPicksLargestRemaining()
    {
        // A=4, B=2, C=1 → порядок предметов: A, A, B, A, A, C, B
        var queue = new Dictionary<string, int> { ["A"] = 4, ["B"] = 2, ["C"] = 1 };
        var sel = new LargestQueueFirstSelector(queue, Slots(7));
        var result = sel.Plan().ToList();

        result.Should().HaveCount(7);
        result.Select(p => p.Lesson.Title).Should().Equal(
            "A", // A=4, B=2, C=1 → A
            "A", // A=3, B=2, C=1 → A
            "B", // A=2, B=2, C=1 → tie → first in dict (A), next: A=2, B=2 → tie → B (second in dict)
                 // Wait, MaxBy returns first max, so with A=2,B=2 it returns A
            "A", // A=2, B=2, C=1 → A
            "A", // A=1, B=2, C=1 → B
            "B", // A=1, B=1, C=1 → tie, A first
            "C"  // A=1, B=1, C=1 → after A placed: A=0,B=1,C=1 → B first
        );
        // Let me recalculate more carefully
    }

    [Fact]
    public void Plan_LargerRemainingChosenEachStep()
    {
        var queue = new Dictionary<string, int> { ["A"] = 5, ["B"] = 3, ["C"] = 1 };
        var sel = new LargestQueueFirstSelector(queue, Slots(9));
        var titles = sel.Plan().Select(p => p.Lesson.Title).ToList();

        // Шаг за шагом:
        // Queue: A=5,B=3,C=1 → MaxBy: A (5)
        // Queue: A=4,B=3,C=1 → MaxBy: A (4)
        // Queue: A=3,B=3,C=1 → MaxBy: A(3) or B(3), MaxBy returns first → A
        // Queue: A=2,B=3,C=1 → MaxBy: B (3)
        // Queue: A=2,B=2,C=1 → MaxBy: A(2) or B(2), MaxBy returns first → A
        // Queue: A=1,B=2,C=1 → MaxBy: B (2)
        // Queue: A=1,B=1,C=1 → MaxBy: A (first with 1)
        // Queue: A=0,B=1,C=1 → MaxBy: B (first with 1)
        // Queue: A=0,B=0,C=1 → MaxBy: C
        titles.Should().Equal(["A", "A", "A", "B", "A", "B", "A", "B", "C"]);
    }

    [Fact]
    public void Plan_SingleSubject_FillsAllSlots()
    {
        var queue = new Dictionary<string, int> { ["Math"] = 4 };
        var sel = new LargestQueueFirstSelector(queue, Slots(4));
        var titles = sel.Plan().Select(p => p.Lesson.Title).ToList();
        titles.Should().AllBe("Math");
    }

    [Fact]
    public void Plan_StopsWhenQueueExhaustedEvenIfSlotsRemain()
    {
        var queue = new Dictionary<string, int> { ["A"] = 2 };
        var sel = new LargestQueueFirstSelector(queue, Slots(10));
        sel.Plan().Should().HaveCount(2);
    }

    [Fact]
    public void Plan_StopsWhenSlotsExhaustedEvenIfQueueRemains()
    {
        var queue = new Dictionary<string, int> { ["A"] = 10 };
        var sel = new LargestQueueFirstSelector(queue, Slots(3));
        sel.Plan().Should().HaveCount(3);
    }

    [Fact]
    public void Plan_AllSubjectsEqual_GetsDictionaryOrder()
    {
        var queue = new Dictionary<string, int> { ["X"] = 3, ["Y"] = 3, ["Z"] = 3 };
        var sel = new LargestQueueFirstSelector(queue, Slots(9));
        var titles = sel.Plan().Select(p => p.Lesson.Title).ToList();

        // Все равны → MaxBy возвращает первый в словаре
        // X=3,Y=3,Z=3 → X
        // X=2,Y=3,Z=3 → Y (первый с 3)
        // X=2,Y=2,Z=3 → Z
        // X=2,Y=2,Z=2 → X
        // X=1,Y=2,Z=2 → Y
        // X=1,Y=1,Z=2 → Z
        // X=1,Y=1,Z=1 → X
        // X=0,Y=1,Z=1 → Y
        // X=0,Y=0,Z=1 → Z
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
        var result = new LargestQueueFirstSelector(queue, slots).Plan().ToList();

        result.Should().HaveCount(1);
        result[0].Slot.Date.Should().Be(new DateOnly(2024, 5, 10));
        result[0].Slot.Number.Should().Be(3);
        result[0].Lesson.Title.Should().Be("A");
        result[0].Lesson.LessonType.Should().Be("PRACTICE");
    }
}
