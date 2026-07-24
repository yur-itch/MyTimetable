using MyTimetable.Planning;

namespace TestMyTimetable.Planning;

public class TestWeightedRandomSelector
{
    private static List<Slot> Slots(int count = 10)
        => Enumerable.Range(1, count)
            .Select(n => new Slot { Date = new DateOnly(2024, 1, 1), Number = n })
            .ToList();

    [Fact]
    public void Plan_EmptyQueue_ReturnsNothing()
    {
        var sel = new WeightedRandomSelector(new Dictionary<string, int>(), Slots(5));
        sel.Plan((_, _) => new(true, true)).Should().BeEmpty();
    }

    [Fact]
    public void Plan_EmptyFillable_ReturnsNothing()
    {
        var sel = new WeightedRandomSelector(new Dictionary<string, int> { ["A"] = 3 }, []);
        sel.Plan((_, _) => new(true, true)).Should().BeEmpty();
    }

    [Fact]
    public void Plan_SingleSubject_FillsAllSlots()
    {
        var queue = new Dictionary<string, int> { ["Math"] = 4 };
        var sel = new WeightedRandomSelector(queue, Slots(4));
        var result = sel.Plan((_, _) => new(true, true)).ToList();
        result.Should().HaveCount(4);
        result.Should().OnlyContain(p => p.Lesson.Title == "Math");
    }

    [Fact]
    public void Plan_StopsWhenQueueExhausted()
    {
        var queue = new Dictionary<string, int> { ["A"] = 2 };
        var sel = new WeightedRandomSelector(queue, Slots(10));
        sel.Plan((_, _) => new(true, true)).Should().HaveCount(2);
    }

    [Fact]
    public void Plan_StopsWhenSlotsExhausted()
    {
        var queue = new Dictionary<string, int> { ["A"] = 10 };
        var sel = new WeightedRandomSelector(queue, Slots(3));
        sel.Plan((_, _) => new(true, true)).Should().HaveCount(3);
    }

    [Fact]
    public void Plan_SeededRandom_DeterministicReplay()
    {
        var queue = new Dictionary<string, int> { ["A"] = 10, ["B"] = 10, ["C"] = 10 };
        var sel1 = new WeightedRandomSelector(queue, Slots(15), new Random(123));
        var sel2 = new WeightedRandomSelector(queue, Slots(15), new Random(123));

        sel1.Plan((_, _) => new(true, true)).Select(p => p.Lesson.Title).Should().Equal(
            sel2.Plan((_, _) => new(true, true)).Select(p => p.Lesson.Title));
    }

    [Fact]
    public void Plan_SeededRandom_AllItemsGetPickedEventually()
    {
        var queue = new Dictionary<string, int>
        {
            ["A"] = 20, ["B"] = 20, ["C"] = 20, ["D"] = 20, ["E"] = 20
        };
        var sel = new WeightedRandomSelector(queue, Slots(100), new Random(42));
        var titles = sel.Plan((_, _) => new(true, true)).Select(p => p.Lesson.Title).Distinct().ToList();

        titles.Should().HaveCount(5);
    }

    [Fact]
    public void Plan_WeightsAffectDistribution()
    {
        var queue = new Dictionary<string, int> { ["A"] = 1, ["B"] = 100 };
        var sel = new WeightedRandomSelector(queue, Slots(101), new Random(42));
        var titles = sel.Plan((_, _) => new(true, true)).Select(p => p.Lesson.Title).ToList();

        titles.Should().Contain("A");
        titles.Count(t => t == "B").Should().BeGreaterThan(titles.Count(t => t == "A"));
    }

    [Fact]
    public void Plan_OutputHasCorrectProperties()
    {
        var queue = new Dictionary<string, int> { ["A"] = 1 };
        var slots = new List<Slot>
        {
            new() { Date = new DateOnly(2024, 5, 10), Number = 3 }
        };
        var result = new WeightedRandomSelector(queue, slots, new Random(0)).Plan((_, _) => new(true, true)).ToList();

        result.Should().HaveCount(1);
        result[0].Slot.Date.Should().Be(new DateOnly(2024, 5, 10));
        result[0].Slot.Number.Should().Be(3);
        result[0].Lesson.Title.Should().Be("A");
        result[0].Lesson.LessonType.Should().Be("PRACTICE");
    }
}
