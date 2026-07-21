using MyTimetable.Models;
using MyTimetable.Planning;

namespace TestMyTimetable.Planning;

public class TestEmptyDaySeedSelector
{
    // ── Helpers ───────────────────────────────────────────────────────

    // Генерирует все 2⁶=64 подмножества {1..6} как отсортированные списки номеров свободных пар.
    private static IEnumerable<List<int>> AllOpenSubsets()
    {
        for (int mask = 0; mask < 64; mask++)
        {
            var set = new List<int>(6);
            for (int i = 1; i <= 6; i++)
                if ((mask & (1 << (i - 1))) != 0) set.Add(i);
            yield return set;
        }
    }

    // Создаёт слоты для одного дня по списку свободных номеров; остальные считаем занятыми.
    // Для EmptyDaySeedSelector fillable пула содержит ТОЛЬКО свободные слоты.
    private static List<Slot> DaySlots(DateOnly date, List<int> open)
        => open.Select(n => new Slot { Date = date, Number = n }).ToList();

    [Fact]
    public void Plan_EmptyQueue_ReturnsNothing()
    {
        var fillable = DaySlots(new DateOnly(2024, 1, 1), [1, 2, 3, 4, 5, 6]);
        var sel = new EmptyDaySeedSelector(new Dictionary<string, int>(), fillable);
        sel.Plan().Should().BeEmpty();
    }

    [Fact]
    public void Plan_EmptyFillable_ReturnsNothing()
    {
        var sel = new EmptyDaySeedSelector(new Dictionary<string, int> { ["A"] = 3}, []);
        sel.Plan().Should().BeEmpty();
    }

    [Fact]
    public void Plan_StopsWhenQueueExhausted()
    {
        // Один пустой день, но предметов меньше чем слотов
        var fillable = DaySlots(new DateOnly(2024, 1, 1), [1, 2, 3, 4, 5, 6]);
        var sel = new EmptyDaySeedSelector(new Dictionary<string, int> { ["A"] = 2 }, fillable);
        sel.Plan().Should().HaveCount(2);
    }

    [Fact]
    public void Plan_SingleSubjectOneEmptyDay_SeedsFirstSlot()
    {
        // Пустой день → сеем в первый свободный слот (номер 1)
        var fillable = DaySlots(new DateOnly(2024, 1, 1), [1, 2, 3, 4, 5, 6]);
        var sel = new EmptyDaySeedSelector(new Dictionary<string, int> { ["A"] = 1 }, fillable);
        var result = sel.Plan().ToList();

        result.Should().HaveCount(1);
        result[0].Slot.Number.Should().Be(1);
        result[0].Slot.Date.Should().Be(new DateOnly(2024, 1, 1));
    }

    [Fact]
    public void Plan_NotAllDaysEmpty_OnlySeedsEmptyDays()
    {
        // День 1: пустой (все слоты свободны)
        // День 2: непустой (слоты 3,4 свободны, остальные заняты — но fillable содержит только 3,4)
        // День 3: пустой
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
        var results = sel.Plan().ToList();

        // Должны быть посеяны только дни 1 и 3 (пустые), по одному разу каждый
        results.Should().HaveCount(2);
        results[0].Slot.Date.Should().Be(new DateOnly(2024, 1, 1));
        results[0].Slot.Number.Should().Be(1);
        results[1].Slot.Date.Should().Be(new DateOnly(2024, 1, 3));
        results[1].Slot.Number.Should().Be(1);
    }

    [Fact]
    public void Plan_DayNotCompletelyEmpty_NotSeeded()
    {
        // День с парой 1 занятой, fillable = [2,3,4,5,6] — не пустой (FirstChunk=[1,1])
        var fillable = DaySlots(new DateOnly(2024, 1, 1), [2, 3, 4, 5, 6]);
        var sel = new EmptyDaySeedSelector(new Dictionary<string, int> { ["A"] = 1 }, fillable);
        sel.Plan().Should().BeEmpty();
    }

    [Fact]
    public void Plan_DayMiddleSlotOccupied_NotSeeded()
    {
        // День с парой 3 занятой, fillable = [1,2,4,5,6] — не пустой (FirstChunk=[1,2])
        var fillable = DaySlots(new DateOnly(2024, 1, 1), [1, 2, 4, 5, 6]);
        var sel = new EmptyDaySeedSelector(new Dictionary<string, int> { ["A"] = 1 }, fillable);
        sel.Plan().Should().BeEmpty();
    }

    [Fact]
    public void Plan_DayLastSlotOccupied_NotSeeded()
    {
        // День с парой 6 занятой, fillable = [1,2,3,4,5] — не пустой (FirstChunk=[6,1])
        var fillable = DaySlots(new DateOnly(2024, 1, 1), [1, 2, 3, 4, 5]);
        var sel = new EmptyDaySeedSelector(new Dictionary<string, int> { ["A"] = 1 }, fillable);
        sel.Plan().Should().BeEmpty();
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
        var results = sel.Plan().ToList();

        results.Should().HaveCount(2);
        results[0].Slot.Date.Should().Be(new DateOnly(2024, 1, 1));
        results[0].Slot.Number.Should().Be(1);
        results[1].Slot.Date.Should().Be(new DateOnly(2024, 1, 2));
        results[1].Slot.Number.Should().Be(1);
    }

    [Fact]
    public void Plan_UsesFirstFreeSlotNumber()
    {
        // Пустой день, но fillable начинается не с 1 (хотя для пустого дня все 1..6 свободны)
        var fillable = DaySlots(new DateOnly(2024, 1, 1), [3, 4, 5, 6]);
        // У филлабл только [3,4,5,6], значит слот 1 и 2 не входят в fillable — это день НЕ пустой
        // (заняты 1 и 2). Так что этот тест не про пустой день.
        // Сделаем fillable=[1,2,3,4,5,6], а селектор сам возьмёт первый — 1.
        fillable = DaySlots(new DateOnly(2024, 1, 1), [1, 2, 3, 4, 5, 6]);
        var sel = new EmptyDaySeedSelector(new Dictionary<string, int> { ["A"] = 1 }, fillable);
        var results = sel.Plan().ToList();

        results[0].Slot.Number.Should().Be(1);
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
        var results = sel.Plan().ToList();

        // 2 пустых дня, round-robin: X, Y
        results.Should().HaveCount(2);
        results[0].Lesson.Title.Should().Be("X");
        results[1].Lesson.Title.Should().Be("Y");
    }

    [Fact]
    public void Plan_MultipleDays_SomeEmpty_OnlyEmptySeeded()
    {
        // 5 дней, пустые: 1 и 4 (все 6 слотов свободны)
        // День 2: занята пара 1 → fillable [2,3,4,5,6]
        // День 3: занята пара 3 → fillable [1,2,4,5,6]
        // День 5: заняты пары 1,6 → fillable [2,3,4,5]
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
        var results = sel.Plan().ToList();

        // Должны быть посеяны дни 1 и 4 (пустые)
        results.Should().HaveCount(2);
        results[0].Slot.Date.Should().Be(new DateOnly(2024, 1, 1));
        results[1].Slot.Date.Should().Be(new DateOnly(2024, 1, 4));
    }

    [Fact]
    public void Plan_OutputHasCorrectProperties()
    {
        var fillable = DaySlots(new DateOnly(2024, 5, 10), [1, 2, 3, 4, 5, 6]);
        var result = new EmptyDaySeedSelector(
            new Dictionary<string, int> { ["A"] = 1 }, fillable).Plan().ToList();

        result.Should().HaveCount(1);
        result[0].Slot.Date.Should().Be(new DateOnly(2024, 5, 10));
        result[0].Slot.Number.Should().Be(1);
        result[0].Lesson.Title.Should().Be("A");
        result[0].Lesson.LessonType.Should().Be("PRACTICE");
    }
}
