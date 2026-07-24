using MyTimetable.Planning;

namespace TestMyTimetable.Planning;

public class TestDayLayout
{
    // ── Helpers ───────────────────────────────────────────────────────

    private static List<Slot> SingleDay(DateOnly date, params int[] numbers)
        => numbers.Select(n => new Slot { Date = date, Number = n }).ToList();

    // Генерирует все 2⁶=64 подмножества {1..6} как отсортированные списки.
    private static IEnumerable<List<int>> AllSubsets()
    {
        for (int mask = 0; mask < 64; mask++)
        {
            var set = new List<int>(6);
            for (int i = 1; i <= 6; i++)
                if ((mask & (1 << (i - 1))) != 0) set.Add(i);
            yield return set;
        }
    }

    // ── GetDays ───────────────────────────────────────────────────────

    [Fact]
    public void GetDays_EmptyList_ReturnsEmpty()
    {
        DayLayout.GetDays([]).Should().BeEmpty();
    }

    [Fact]
    public void GetDays_SingleSlot_ReturnsSingleGroup()
    {
        var days = DayLayout.GetDays(SingleDay(new DateOnly(2024, 1, 1), 5)).ToList();
        days.Should().HaveCount(1);
        days[0].Open.Should().Equal([5]);
    }

    [Fact]
    public void GetDays_MultipleSlotsOneDate_ReturnsSingleGroup()
    {
        var date = new DateOnly(2024, 1, 1);
        var days = DayLayout.GetDays(SingleDay(date, 1, 2, 3)).ToList();
        days.Should().HaveCount(1);
        days[0].Open.Should().Equal([1, 2, 3]);
    }

    [Fact]
    public void GetDays_TwoDates_ReturnsTwoGroups()
    {
        var slots = new List<Slot>
        {
            new() { Date = new DateOnly(2024, 1, 1), Number = 1 },
            new() { Date = new DateOnly(2024, 1, 2), Number = 2 },
        };
        var days = DayLayout.GetDays(slots).ToList();
        days.Should().HaveCount(2);
        days[0].Open.Should().Equal([1]);
        days[1].Open.Should().Equal([2]);
    }

    [Fact]
    public void GetDays_ThreeDates_ReturnsThreeGroups()
    {
        var slots = new List<Slot>
        {
            new() { Date = new DateOnly(2024, 1, 1), Number = 1 },
            new() { Date = new DateOnly(2024, 1, 1), Number = 2 },
            new() { Date = new DateOnly(2024, 1, 2), Number = 3 },
            new() { Date = new DateOnly(2024, 1, 3), Number = 4 },
            new() { Date = new DateOnly(2024, 1, 3), Number = 5 },
        };
        var days = DayLayout.GetDays(slots).ToList();
        days.Should().HaveCount(3);
        days[0].Open.Should().Equal([1, 2]);
        days[1].Open.Should().Equal([3]);
        days[2].Open.Should().Equal([4, 5]);
    }

    [Fact]
    public void GetDays_PreservesInputOrderWithinGroup()
    {
        var date = new DateOnly(2024, 1, 1);
        var days = DayLayout.GetDays(SingleDay(date, 10, 3, 7)).ToList();
        days.Should().HaveCount(1);
        days[0].Open.Should().Equal([10, 3, 7]);
    }

    [Fact]
    public void GetDays_OneSlotPerDate_EachGroupHasOneElement()
    {
        var slots = Enumerable.Range(1, 5)
            .Select(d => new Slot { Date = new DateOnly(2024, 1, d), Number = d * 10 })
            .ToList();
        var days = DayLayout.GetDays(slots).ToList();
        days.Should().HaveCount(5);
        days.Select(g => g.Open.Single()).Should().Equal([10, 20, 30, 40, 50]);
    }

    [Fact]
    public void GetDays_SlotsOutOfDateOrder_StillGroupsByAdjacentDates()
    {
        // GetDays не сортирует — группирует по смежности дат в переданном порядке.
        var slots = new List<Slot>
        {
            new() { Date = new DateOnly(2024, 1, 2), Number = 1 },
            new() { Date = new DateOnly(2024, 1, 1), Number = 2 },
            new() { Date = new DateOnly(2024, 1, 1), Number = 3 },
        };
        var days = DayLayout.GetDays(slots).ToList();
        days.Should().HaveCount(2);
        days[0].Open.Should().Equal([1]);   // date 2024-01-02
        days[1].Open.Should().Equal([2, 3]); // date 2024-01-01
    }

    // ── FirstChunk — exhaustive: all 64 subsets of {1..6} ────────────

    public static IEnumerable<object[]> FirstChunkCases()
    {
        foreach (var open in AllSubsets())
        {
            var openArr = open.ToArray();
            var occupied = Enumerable.Range(1, 6).Except(open).ToArray();

            int? expectedStart = occupied.Length > 0 ? occupied.Min() : null;
            int? expectedLength = null;
            if (expectedStart is { } start)
            {
                // Первый непрерывный отрезок занятых от старта
                int end = start;
                while (occupied.Contains(end + 1)) end++;
                expectedLength = end - start + 1;
            }

            yield return new object[]
            {
                openArr,
                6,
                (object?)expectedStart,
                (object?)expectedLength,
            };
        }
    }

    [Theory]
    [MemberData(nameof(FirstChunkCases))]
    public void FirstChunk_AllSubsets(int[] open, int slotCount, int? expectedStart, int? expectedLength)
    {
        var result = DayLayout.FirstChunk(open.ToList(), slotCount);
        if (expectedStart is null)
        {
            result.Should().BeNull($"open=[{string.Join(",", open)}] should have no occupied slots");
        }
        else
        {
            result.Should().NotBeNull($"open=[{string.Join(",", open)}] should have first chunk");
            result!.Value.Start.Should().Be(expectedStart.Value);
            result!.Value.Length.Should().Be(expectedLength!.Value);
        }
    }

    // ── LastChunk — exhaustive: all 64 subsets of {1..6} ─────────────

    public static IEnumerable<object[]> LastChunkCases()
    {
        foreach (var open in AllSubsets())
        {
            var openArr = open.ToArray();
            var occupied = Enumerable.Range(1, 6).Except(open).ToArray();

            int? expectedStart = null;
            int? expectedLength = null;
            if (occupied.Length > 0)
            {
                int end = occupied.Max();
                int start = end;
                while (occupied.Contains(start - 1)) start--;
                expectedStart = start;
                expectedLength = end - start + 1;
            }

            yield return new object[]
            {
                openArr,
                6,
                (object?)expectedStart,
                (object?)expectedLength,
            };
        }
    }

    [Theory]
    [MemberData(nameof(LastChunkCases))]
    public void LastChunk_AllSubsets(int[] open, int slotCount, int? expectedStart, int? expectedLength)
    {
        var result = DayLayout.LastChunk(open.ToList(), slotCount);
        if (expectedStart is null)
        {
            result.Should().BeNull($"open=[{string.Join(",", open)}] should have no occupied slots");
        }
        else
        {
            result.Should().NotBeNull($"open=[{string.Join(",", open)}] should have last chunk");
            result!.Value.Start.Should().Be(expectedStart.Value);
            result!.Value.Length.Should().Be(expectedLength!.Value);
        }
    }

    // ── Edge cases for FirstChunk/LastChunk ──────────────────────────

    [Fact]
    public void FirstChunk_FullEmptyDay6_ReturnsNull()
    {
        // День из 6 слотов, все свободны
        DayLayout.FirstChunk([1, 2, 3, 4, 5, 6], 6).Should().BeNull();
        DayLayout.LastChunk([1, 2, 3, 4, 5, 6], 6).Should().BeNull();
    }

    [Fact]
    public void FirstChunk_FullEmptyDayWithLargerSlotCount_HasOccupiedSlots()
    {
        // open=[1..6], slotCount=10 → заняты 7,8,9,10 → есть первый кусок
        DayLayout.FirstChunk([1, 2, 3, 4, 5, 6], 10)!.Value.Should().Be(new Chunk { Start = 7, Length = 4 });
        DayLayout.LastChunk([1, 2, 3, 4, 5, 6], 10)!.Value.Should().Be(new Chunk { Start = 7, Length = 4 });
    }

    [Theory]
    [InlineData(6)]
    [InlineData(10)]
    public void FirstChunk_FullOccupiedDay_ReturnsWholeRange(int slotCount)
    {
        var all = Enumerable.Range(1, slotCount).ToList();
        var fc = DayLayout.FirstChunk([], slotCount);
        fc.Should().NotBeNull();
        fc!.Value.Start.Should().Be(1);
        fc!.Value.Length.Should().Be(slotCount);

        var lc = DayLayout.LastChunk([], slotCount);
        lc.Should().NotBeNull();
        lc!.Value.Start.Should().Be(1);
        lc!.Value.Length.Should().Be(slotCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void FirstChunk_InvalidSlotCount_StillProcesses(int slotCount)
    {
        // Граничное поведение: FirstChunk и LastChunk не валидируют slotCount,
        // но с пустым open возвращают null
        DayLayout.FirstChunk([], slotCount).Should().BeNull();
        DayLayout.LastChunk([], slotCount).Should().BeNull();
    }
}
