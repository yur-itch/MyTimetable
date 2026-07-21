using MyTimetable.Models;
using MyTimetable.Planning;

namespace TestMyTimetable.Planning;

public class TestRandomSelector
{
    private static List<Slot> Slots(int count = 10)
        => Enumerable.Range(1, count)
            .Select(n => new Slot { Date = new DateOnly(2024, 1, 1), Number = n })
            .ToList();

    [Fact]
    public void Plan_EmptyQueue_ReturnsNothing()
    {
        var sel = new RandomSelector(new Dictionary<string, int>(), Slots(5));
        sel.Plan().Should().BeEmpty();
    }

    [Fact]
    public void Plan_EmptyFillable_ReturnsNothing()
    {
        var sel = new RandomSelector(new Dictionary<string, int> { ["A"] = 3 }, []);
        sel.Plan().Should().BeEmpty();
    }

    [Fact]
    public void Plan_SingleSubject_FillsAllSlots()
    {
        var queue = new Dictionary<string, int> { ["Math"] = 4 };
        var sel = new RandomSelector(queue, Slots(4));
        sel.Plan().Select(p => p.Lesson.Title).Should().AllBe("Math");
    }

    [Fact]
    public void Plan_StopsWhenQueueExhausted()
    {
        var queue = new Dictionary<string, int> { ["A"] = 2 };
        var sel = new RandomSelector(queue, Slots(10));
        sel.Plan().Should().HaveCount(2);
    }

    [Fact]
    public void Plan_StopsWhenSlotsExhausted()
    {
        var queue = new Dictionary<string, int> { ["A"] = 10 };
        var sel = new RandomSelector(queue, Slots(3));
        sel.Plan().Should().HaveCount(3);
    }

    [Fact]
    public void Plan_SeededRandom_GivesDeterministicOutput()
    {
        // С фиксированным Random(42) результат предсказуем
        var queue = new Dictionary<string, int> { ["A"] = 5, ["B"] = 5 };
        var sel = new RandomSelector(queue, Slots(10), new Random(42));
        var titles = sel.Plan().Select(p => p.Lesson.Title).ToList();

        // Просто проверяем, что все 10 слотов заполнены и только A/B
        titles.Should().HaveCount(10);
        titles.Should().OnlyContain(t => t == "A" || t == "B");
    }

    [Fact]
    public void Plan_SeededRandom_DeterministicReplay()
    {
        var queue = new Dictionary<string, int> { ["A"] = 10, ["B"] = 10, ["C"] = 10 };
        var sel1 = new RandomSelector(queue, Slots(15), new Random(123));
        var sel2 = new RandomSelector(queue, Slots(15), new Random(123));

        sel1.Plan().Select(p => p.Lesson.Title).Should().Equal(
            sel2.Plan().Select(p => p.Lesson.Title));
    }

    [Fact]
    public void Plan_DifferentSeeds_ProduceDifferentSequences()
    {
        var queue = new Dictionary<string, int> { ["A"] = 10, ["B"] = 10 };
        var sel1 = new RandomSelector(queue, Slots(10), new Random(42));
        var sel2 = new RandomSelector(queue, Slots(10), new Random(9999));

        seq1 = sel1.Plan().Select(p => p.Lesson.Title).ToList();
        seq2 = sel2.Plan().Select(p => p.Lesson.Title).ToList();

        seq1.Should().NotEqual(seq2, "разные seed'ы должны давать разные последовательности");
    }

    private List<string>? seq1, seq2; // для хранения промежуточных результатов

    [Fact]
    public void Plan_OutputHasCorrectProperties()
    {
        var queue = new Dictionary<string, int> { ["A"] = 1 };
        var slots = new List<Slot>
        {
            new() { Date = new DateOnly(2024, 5, 10), Number = 3 }
        };
        var result = new RandomSelector(queue, slots, new Random(0)).Plan().ToList();

        result.Should().HaveCount(1);
        result[0].Slot.Date.Should().Be(new DateOnly(2024, 5, 10));
        result[0].Slot.Number.Should().Be(3);
        result[0].Lesson.Title.Should().Be("A");
        result[0].Lesson.LessonType.Should().Be("PRACTICE");
    }
}
