using MyTimetable.Models;
using MyTimetable.Planning;

namespace TestMyTimetable.Planning;

public class TestEmptyDaySeedSelector
{
    private static List<Slot> DaySlots(DateOnly date, List<int> open)
        => open.Select(n => new Slot { Date = date, Number = n }).ToList();

    [Fact]
    public void Plan_EmptyQueue_ReturnsNothing()
    {
        var fillable = DaySlots(new DateOnly(2024, 1, 1), [1, 2, 3, 4, 5, 6]);
        var sel = new EmptyDaySeedSelector(new Dictionary<string, int>(), fillable);
        sel.Plan((_, _) => new(true, true)).Should().BeEmpty();
    }

    [Fact]
    public void Plan_EmptyFillable_ReturnsNothing()
    {
        var sel = new EmptyDaySeedSelector(new Dictionary<string, int> { ["A"] = 3 }, []);
        sel.Plan((_, _) => new(true, true)).Should().BeEmpty();
    }

    [Fact]
    public void Plan_StopsWhenQueueExhausted()
    {
        // Один пустой день, 2 предмета — пустой день выдаёт 1 слот (первый свободный)
        var fillable = DaySlots(new DateOnly(2024, 1, 1), [1, 2, 3, 4, 5, 6]);
        var sel = new EmptyDaySeedSelector(new Dictionary<string, int> { ["A"] = 2 }, fillable);
        // Один пустой день → один seed-слот → Plan даёт 1 результат (потом _seeds кончился)
        var result = sel.Plan((_, _) => new(true, true)).ToList();
        result.Should().HaveCount(1);
    }

    [Fact]
    public void Plan_SingleSubjectOneEmptyDay_SeedsFirstSlot()
    {
        var fillable = DaySlots(new DateOnly(2024, 1, 1), [1, 2, 3, 4, 5, 6]);
        var sel = new EmptyDaySeedSelector(new Dictionary<string, int> { ["A"] = 1 }, fillable);
        var result = sel.Plan((_, _) => new(true, true)).ToList();

        result.Should().HaveCount(1);
        result[0].Slot.Number.Should().Be(1);
        result[0].Slot.Date.Should().Be(new DateOnly(2024, 1, 1));
    }

    [Fact]
    public void Plan_NotAllDaysEmpty_OnlySeedsEmptyDays()
    {
        // День 1: пустой. День 2: непустой (слоты 3,4 свободны). День 3: пустой.
        var fillable = new List<Slot>
        {
            new() { Date = new DateOnly(2024, 1, 1), Number = 1 },
            new() { Date = new DateOnly(2024, 1, 1), Number = 2 },
            new() { Date = new DateOnly(2024, 1, 1), Number = 3 },
            new() { Date = new DateOnly(2024, 1, 1), Number = 4 },
            new() { Date = new DateOnly(2024, 1, 1), Number = 5 },
            new() { Date = new DateOnly(2024, 1, 1), Number = 6 },
            new() { Date = new DateOnly(2024, 1, 2), Number = 3 },
            new() { Date = new DateOnly(2024, 1, 2), Number = 4 },
            new() { Date = new DateOnly(2024, 1, 3), Number = 1 },
            new() { Date = new DateOnly(2024, 1, 3), Number = 2 },
            new() { Date = new DateOnly(2024, 1, 3), Number = 3 },
            new() { Date = new DateOnly(2024, 1, 3), Number = 4 },
            new() { Date = new DateOnly(2024, 1, 3), Number = 5 },
            new() { Date = new DateOnly(2024, 1, 3), Number = 6 },
        };
        var queue = new Dictionary<string, int> { ["A"] = 3, ["B"] = 3 };
        var sel = new EmptyDaySeedSelector(queue, fillable);
        var results = sel.Plan((_, _) => new(true, true)).ToList();

        // Дни 1 и 3 — пустые → по одному seed'у в первый слот каждого
        results.Should().HaveCount(2);
        results[0].Slot.Date.Should().Be(new DateOnly(2024, 1, 1));
        results[0].Slot.Number.Should().Be(1);
        results[1].Slot.Date.Should().Be(new DateOnly(2024, 1, 3));
        results[1].Slot.Number.Should().Be(1);
    }

    [Fact]
    public void Plan_DayNotCompletelyEmpty_NotSeeded()
    {
        var fillable = DaySlots(new DateOnly(2024, 1, 1), [2, 3, 4, 5, 6]);
        var sel = new EmptyDaySeedSelector(new Dictionary<string, int> { ["A"] = 1 }, fillable);
        sel.Plan((_, _) => new(true, true)).Should().BeEmpty();
    }

    [Fact]
    public void Plan_DayMiddleSlotOccupied_NotSeeded()
    {
        var fillable = DaySlots(new DateOnly(2024, 1, 1), [1, 2, 4, 5, 6]);
        var sel = new EmptyDaySeedSelector(new Dictionary<string, int> { ["A"] = 1 }, fillable);
        sel.Plan((_, _) => new(true, true)).Should().BeEmpty();
    }

    [Fact]
    public void Plan_TwoEmptyDays_SeedsBothInOrder()
    {
        var fillable = new List<Slot>
        {
            new() { Date = new DateOnly(2024, 1, 1), Number = 1 },
            new() { Date = new DateOnly(2024, 1, 1), Number = 2 },
            new() { Date = new DateOnly(2024, 1, 1), Number = 3 },
            new() { Date = new DateOnly(2024, 1, 1), Number = 4 },
            new() { Date = new DateOnly(2024, 1, 1), Number = 5 },
            new() { Date = new DateOnly(2024, 1, 1), Number = 6 },
            new() { Date = new DateOnly(2024, 1, 2), Number = 1 },
            new() { Date = new DateOnly(2024, 1, 2), Number = 2 },
            new() { Date = new DateOnly(2024, 1, 2), Number = 3 },
            new() { Date = new DateOnly(2024, 1, 2), Number = 4 },
            new() { Date = new DateOnly(2024, 1, 2), Number = 5 },
            new() { Date = new DateOnly(2024, 1, 2), Number = 6 },
        };
        var queue = new Dictionary<string, int> { ["A"] = 2 };
        var sel = new EmptyDaySeedSelector(queue, fillable);
        var results = sel.Plan((_, _) => new(true, true)).ToList();

        results.Should().HaveCount(2);
        results[0].Slot.Date.Should().Be(new DateOnly(2024, 1, 1));
        results[0].Slot.Number.Should().Be(1);
        results[1].Slot.Date.Should().Be(new DateOnly(2024, 1, 2));
        results[1].Slot.Number.Should().Be(1);
    }

    [Fact]
    public void Plan_UsesRoundRobinForSubjectSelection()
    {
        var fillable = new List<Slot>
        {
            new() { Date = new DateOnly(2024, 1, 1), Number = 1 },
            new() { Date = new DateOnly(2024, 1, 1), Number = 2 },
            new() { Date = new DateOnly(2024, 1, 1), Number = 3 },
            new() { Date = new DateOnly(2024, 1, 1), Number = 4 },
            new() { Date = new DateOnly(2024, 1, 1), Number = 5 },
            new() { Date = new DateOnly(2024, 1, 1), Number = 6 },
            new() { Date = new DateOnly(2024, 1, 2), Number = 1 },
            new() { Date = new DateOnly(2024, 1, 2), Number = 2 },
            new() { Date = new DateOnly(2024, 1, 2), Number = 3 },
            new() { Date = new DateOnly(2024, 1, 2), Number = 4 },
            new() { Date = new DateOnly(2024, 1, 2), Number = 5 },
            new() { Date = new DateOnly(2024, 1, 2), Number = 6 },
        };
        var queue = new Dictionary<string, int> { ["X"] = 2, ["Y"] = 2 };
        var sel = new EmptyDaySeedSelector(queue, fillable);
        var results = sel.Plan((_, _) => new(true, true)).ToList();

        results.Should().HaveCount(2);
        results[0].Lesson.Title.Should().Be("X");
        results[1].Lesson.Title.Should().Be("Y");
    }

    [Fact]
    public void Plan_MultipleDays_SomeEmpty_OnlyEmptySeeded()
    {
        // 5 дней, пустые: 1 и 4 (все 6 слотов свободны)
        // День 2: занят слот 1 → fillable [2,3,4,5,6]
        // День 3: занят слот 3 → fillable [1,2,4,5,6]
        // День 5: заняты 1,6 → fillable [2,3,4,5]
        var fillable = new List<Slot>
        {
            new() { Date = new DateOnly(2024, 1, 1), Number = 1 },
            new() { Date = new DateOnly(2024, 1, 1), Number = 2 },
            new() { Date = new DateOnly(2024, 1, 1), Number = 3 },
            new() { Date = new DateOnly(2024, 1, 1), Number = 4 },
            new() { Date = new DateOnly(2024, 1, 1), Number = 5 },
            new() { Date = new DateOnly(2024, 1, 1), Number = 6 },
            new() { Date = new DateOnly(2024, 1, 2), Number = 2 },
            new() { Date = new DateOnly(2024, 1, 2), Number = 3 },
            new() { Date = new DateOnly(2024, 1, 2), Number = 4 },
            new() { Date = new DateOnly(2024, 1, 2), Number = 5 },
            new() { Date = new DateOnly(2024, 1, 2), Number = 6 },
            new() { Date = new DateOnly(2024, 1, 3), Number = 1 },
            new() { Date = new DateOnly(2024, 1, 3), Number = 2 },
            new() { Date = new DateOnly(2024, 1, 3), Number = 4 },
            new() { Date = new DateOnly(2024, 1, 3), Number = 5 },
            new() { Date = new DateOnly(2024, 1, 3), Number = 6 },
            new() { Date = new DateOnly(2024, 1, 4), Number = 1 },
            new() { Date = new DateOnly(2024, 1, 4), Number = 2 },
            new() { Date = new DateOnly(2024, 1, 4), Number = 3 },
            new() { Date = new DateOnly(2024, 1, 4), Number = 4 },
            new() { Date = new DateOnly(2024, 1, 4), Number = 5 },
            new() { Date = new DateOnly(2024, 1, 4), Number = 6 },
            new() { Date = new DateOnly(2024, 1, 5), Number = 2 },
            new() { Date = new DateOnly(2024, 1, 5), Number = 3 },
            new() { Date = new DateOnly(2024, 1, 5), Number = 4 },
            new() { Date = new DateOnly(2024, 1, 5), Number = 5 },
        };
        var queue = new Dictionary<string, int> { ["A"] = 3 };
        var sel = new EmptyDaySeedSelector(queue, fillable);
        var results = sel.Plan((_, _) => new(true, true)).ToList();

        results.Should().HaveCount(2);
        results[0].Slot.Date.Should().Be(new DateOnly(2024, 1, 1));
        results[1].Slot.Date.Should().Be(new DateOnly(2024, 1, 4));
    }

    [Fact]
    public void Plan_OutputHasCorrectProperties()
    {
        var fillable = DaySlots(new DateOnly(2024, 5, 10), [1, 2, 3, 4, 5, 6]);
        var result = new EmptyDaySeedSelector(
            new Dictionary<string, int> { ["A"] = 1 }, fillable).Plan((_, _) => new(true, true)).ToList();

        result.Should().HaveCount(1);
        result[0].Slot.Date.Should().Be(new DateOnly(2024, 5, 10));
        result[0].Slot.Number.Should().Be(1);
        result[0].Lesson.Title.Should().Be("A");
        result[0].Lesson.LessonType.Should().Be("PRACTICE");
    }
}
