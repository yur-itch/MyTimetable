using MyTimetable.Planning;

namespace TestMyTimetable.Planning;

public class TestFairShareSelector
{
    private static List<Slot> Slots(int count = 10, DateOnly? date = null)
        => Enumerable.Range(1, count)
            .Select(n => new Slot { Date = date ?? new DateOnly(2024, 1, 1), Number = n })
            .ToList();

    [Fact]
    public void Plan_EmptyQueue_ReturnsNothing()
    {
        var sel = new FairShareSelector(new Dictionary<string, int>(), Slots(5));
        sel.Plan((_, _) => new(true, true)).Should().BeEmpty();
    }

    [Fact]
    public void Plan_EmptyFillable_ReturnsNothing()
    {
        var sel = new FairShareSelector(new Dictionary<string, int> { ["A"] = 3 }, []);
        sel.Plan((_, _) => new(true, true)).Should().BeEmpty();
    }

    [Fact]
    public void Plan_SingleSubject_FillsAll()
    {
        var queue = new Dictionary<string, int> { ["Math"] = 4 };
        var sel = new FairShareSelector(queue, Slots(4));
        var result = sel.Plan((_, _) => new(true, true)).ToList();
        result.Should().HaveCount(4);
        result.Should().OnlyContain(p => p.Lesson.Title == "Math");
    }

    [Fact]
    public void Plan_StopsWhenQueueExhausted()
    {
        var queue = new Dictionary<string, int> { ["A"] = 2 };
        var sel = new FairShareSelector(queue, Slots(10));
        sel.Plan((_, _) => new(true, true)).Should().HaveCount(2);
    }

    [Fact]
    public void Plan_StopsWhenSlotsExhausted()
    {
        var queue = new Dictionary<string, int> { ["A"] = 10 };
        var sel = new FairShareSelector(queue, Slots(3));
        sel.Plan((_, _) => new(true, true)).Should().HaveCount(3);
    }

    [Fact]
    public void Plan_EqualProportions_AlternatesFairly()
    {
        var queue = new Dictionary<string, int> { ["A"] = 3, ["B"] = 3 };
        var sel = new FairShareSelector(queue, Slots(6));
        var titles = sel.Plan((_, _) => new(true, true)).Select(p => p.Lesson.Title).ToList();

        // A=50%, B=50% → с равными расстояниями берём первый в Available: A
        // Затем чередуем для балансировки
        titles.Should().Equal(["A", "B", "A", "B", "A", "B"]);
    }

    [Fact]
    public void Plan_UnequalProportions_DistributesFairly()
    {
        // A=3 (75%), B=1 (25%)
        // Шаг 1: placed={}, total=0. Если A: A=1, B=0, dist² = (1-0.75)² + (0-0.25)² = 0.0625+0.0625=0.125
        //   Если B: A=0, B=1, dist² = (0-0.75)² + (1-0.25)² = 0.5625+0.5625=1.125
        //   → A (0.125 < 1.125)
        // Шаг 2: placed={A=1}, total=1. Если A: A=2/2=1,B=0/2=0, dist²=0.125
        //   Если B: A=1/2=0.5,B=1/2=0.5, dist²=(0.5-0.75)²+(0.5-0.25)²=0.0625+0.0625=0.125
        //   → равны → первый в Available: A
        // Шаг 3: placed={A=2}, total=2. Если A: A=3/3=1,B=0/3=0, dist²=0.125
        //   Если B: A=2/3=0.667,B=1/3=0.333, dist²=(0.667-0.75)²+(0.333-0.25)²=0.0069+0.0069=0.0139
        //   → B (0.0139 < 0.125)
        // Шаг 4: placed={A=2,B=1}, total=3. Остался A=1, только A в Available → A
        var queue = new Dictionary<string, int> { ["A"] = 3, ["B"] = 1 };
        var sel = new FairShareSelector(queue, Slots(4));
        var titles = sel.Plan((_, _) => new(true, true)).Select(p => p.Lesson.Title).ToList();

        titles.Should().Equal(["A", "A", "B", "A"]);
    }

    [Fact]
    public void Plan_FourToOneRatio()
    {
        var queue = new Dictionary<string, int> { ["A"] = 4, ["B"] = 1 };
        var sel = new FairShareSelector(queue, Slots(5));
        var titles = sel.Plan((_, _) => new(true, true)).Select(p => p.Lesson.Title).ToList();

        // Шаг 1: пусто → если A: dist²=0.125, если B: dist²=1.125 → A
        // Шаг 2: A=1. Если A: dist²=0.125, если B: dist²=0.125 → равны → A (первый)
        // Шаг 3: A=2. Если A: dist²=0.125, если B: dist²=0.035 → B
        // Шаг 4: A=2,B=1. Если A: dist²=0.005, если B: dist²=0.18 → A
        // Шаг 5: A=3,B=1. Остался A=1 → A
        titles.Should().Equal(["A", "A", "B", "A", "A"]);
    }

    [Fact]
    public void Plan_ThreeEqualSubjects()
    {
        var queue = new Dictionary<string, int> { ["X"] = 2, ["Y"] = 2, ["Z"] = 2 };
        var sel = new FairShareSelector(queue, Slots(6));
        var titles = sel.Plan((_, _) => new(true, true)).Select(p => p.Lesson.Title).ToList();

        titles.Should().HaveCount(6);
        titles.Should().OnlyContain(t => t == "X" || t == "Y" || t == "Z");
        titles.Count(t => t == "X").Should().Be(2);
        titles.Count(t => t == "Y").Should().Be(2);
        titles.Count(t => t == "Z").Should().Be(2);
    }

    [Fact]
    public void Plan_OutputHasCorrectProperties()
    {
        var queue = new Dictionary<string, int> { ["A"] = 1 };
        var slots = new List<Slot>
        {
            new() { Date = new DateOnly(2024, 5, 10), Number = 3 }
        };
        var result = new FairShareSelector(queue, slots).Plan((_, _) => new(true, true)).ToList();

        result.Should().HaveCount(1);
        result[0].Slot.Date.Should().Be(new DateOnly(2024, 5, 10));
        result[0].Slot.Number.Should().Be(3);
        result[0].Lesson.Title.Should().Be("A");
        result[0].Lesson.LessonType.Should().Be("PRACTICE");
    }
}
