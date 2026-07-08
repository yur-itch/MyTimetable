using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using MyTimetable.Planning;
using MyTimetable.Models;
using FluentAssertions;
using SlotRange = MyTimetable.Planning.Range;

namespace TestMyTimetable.Planning
{
    public class TestGapClosingSelector
    {
        // GetDays переехал в DayLayout (internal, публичный метод) — рефлексия больше не нужна,
        // тест проекта видит internal-члены через InternalsVisibleTo.
        private static IEnumerable<List<int>> CallGetDays(List<Slot> slots)
            => DayLayout.GetDays(slots).Select(d => d.Open);

        // ── Пустой список ────────────────────────────────────────────

        [Fact]
        public void GetDays_EmptyList_ReturnsEmpty()
        {
            var result = CallGetDays([]);

            result.Should().BeEmpty();
        }

        // ── Один слот ────────────────────────────────────────────────

        [Fact]
        public void GetDays_SingleSlot_ReturnsSingleGroupWithOneNumber()
        {
            var slots = new List<Slot>
        {
            new() { Date = new DateOnly(2024, 1, 1), Number = 5 }
        };

            var result = CallGetDays(slots).ToList();

            result.Should().HaveCount(1);
            result[0].Should().Equal([5]);
        }

        // ── Несколько слотов, одна дата ───────────────────────────────

        [Fact]
        public void GetDays_MultipleSlotsOneDate_ReturnsSingleGroup()
        {
            var date = new DateOnly(2024, 1, 1);
            var slots = new List<Slot>
        {
            new() { Date = date, Number = 1 },
            new() { Date = date, Number = 2 },
            new() { Date = date, Number = 3 },
        };

            var result = CallGetDays(slots).ToList();

            result.Should().HaveCount(1);
            result[0].Should().Equal([1, 2, 3]);
        }

        // ── Слоты на разных датах ─────────────────────────────────────

        [Fact]
        public void GetDays_TwoDates_ReturnsTwoGroups()
        {
            var slots = new List<Slot>
        {
            new() { Date = new DateOnly(2024, 1, 1), Number = 1 },
            new() { Date = new DateOnly(2024, 1, 2), Number = 2 },
        };

            var result = CallGetDays(slots).ToList();

            result.Should().HaveCount(2);
            result[0].Should().Equal([1]);
            result[1].Should().Equal([2]);
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

            var result = CallGetDays(slots).ToList();

            result.Should().HaveCount(3);
            result[0].Should().Equal([1, 2]);
            result[1].Should().Equal([3]);
            result[2].Should().Equal([4, 5]);
        }

        // ── Порядок групп ─────────────────────────────────────────────

        [Fact]
        public void GetDays_PreservesOrderWithinGroup()
        {
            var date = new DateOnly(2024, 1, 1);
            var slots = new List<Slot>
        {
            new() { Date = date, Number = 10 },
            new() { Date = date, Number = 3 },
            new() { Date = date, Number = 7 },
        };

            var result = CallGetDays(slots).ToList();

            result[0].Should().Equal([10, 3, 7]); // порядок не меняется
        }

        // ── Один слот на каждую дату ──────────────────────────────────

        [Fact]
        public void GetDays_OneSlotPerDate_EachGroupHasOneElement()
        {
            var slots = Enumerable.Range(1, 5)
                .Select(d => new Slot
                {
                    Date = new DateOnly(2024, 1, d),
                    Number = d * 10
                })
                .ToList();

            var result = CallGetDays(slots).ToList();

            result.Should().HaveCount(5);
            result.Select(g => g.Single()).Should().Equal([10, 20, 30, 40, 50]);
        }

        // GetGapsInDay стал instance-методом (нужен _slotCount из конструктора) — рефлексия теперь
        // бьёт по инстансу, а не по типу; сам метод остался private, InternalsVisibleTo тут не помогает.
        private static List<SlotRange> Invoke(int[] emptySlots)
        {
            var selector = new GapClosingSelector(new Dictionary<string, int>(), new List<Slot>());
            var method = typeof(GapClosingSelector)
                .GetMethod("GetGapsInDay", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("GetGapsInDay not found");

            return ((IEnumerable<SlotRange>)method.Invoke(selector, [emptySlots.ToList()])!).ToList();
        }

        // Каждый кейс: [пустые слоты, ожидаемые Start[], ожидаемые Size[]]
        public static IEnumerable<object[]> Cases()
        {
            yield return new object[] { new int[] { }, new int[] { }, new int[] { } };
            yield return new object[] { new int[] { 1 }, new int[] { }, new int[] { } };
            yield return new object[] { new int[] { 2 }, new int[] { 2 }, new int[] { 1 } };
            yield return new object[] { new int[] { 3 }, new int[] { 3 }, new int[] { 1 } };
            yield return new object[] { new int[] { 4 }, new int[] { 4 }, new int[] { 1 } };
            yield return new object[] { new int[] { 5 }, new int[] { 5 }, new int[] { 1 } };
            yield return new object[] { new int[] { 6 }, new int[] { }, new int[] { } };
            yield return new object[] { new int[] { 1, 2 }, new int[] { }, new int[] { } };
            yield return new object[] { new int[] { 1, 3 }, new int[] { 3 }, new int[] { 1 } };
            yield return new object[] { new int[] { 1, 4 }, new int[] { 4 }, new int[] { 1 } };
            yield return new object[] { new int[] { 1, 5 }, new int[] { 5 }, new int[] { 1 } };
            yield return new object[] { new int[] { 1, 6 }, new int[] { }, new int[] { } };
            yield return new object[] { new int[] { 2, 3 }, new int[] { 2 }, new int[] { 2 } };
            yield return new object[] { new int[] { 2, 4 }, new int[] { 2, 4 }, new int[] { 1, 1 } };
            yield return new object[] { new int[] { 2, 5 }, new int[] { 2, 5 }, new int[] { 1, 1 } };
            yield return new object[] { new int[] { 2, 6 }, new int[] { 2 }, new int[] { 1 } };
            yield return new object[] { new int[] { 3, 4 }, new int[] { 3 }, new int[] { 2 } };
            yield return new object[] { new int[] { 3, 5 }, new int[] { 3, 5 }, new int[] { 1, 1 } };
            yield return new object[] { new int[] { 3, 6 }, new int[] { 3 }, new int[] { 1 } };
            yield return new object[] { new int[] { 4, 5 }, new int[] { 4 }, new int[] { 2 } };
            yield return new object[] { new int[] { 4, 6 }, new int[] { 4 }, new int[] { 1 } };
            yield return new object[] { new int[] { 5, 6 }, new int[] { }, new int[] { } };
            yield return new object[] { new int[] { 1, 2, 3 }, new int[] { }, new int[] { } };
            yield return new object[] { new int[] { 1, 2, 4 }, new int[] { 4 }, new int[] { 1 } };
            yield return new object[] { new int[] { 1, 2, 5 }, new int[] { 5 }, new int[] { 1 } };
            yield return new object[] { new int[] { 1, 2, 6 }, new int[] { }, new int[] { } };
            yield return new object[] { new int[] { 1, 3, 4 }, new int[] { 3 }, new int[] { 2 } };
            yield return new object[] { new int[] { 1, 3, 5 }, new int[] { 3, 5 }, new int[] { 1, 1 } };
            yield return new object[] { new int[] { 1, 3, 6 }, new int[] { 3 }, new int[] { 1 } };
            yield return new object[] { new int[] { 1, 4, 5 }, new int[] { 4 }, new int[] { 2 } };
            yield return new object[] { new int[] { 1, 4, 6 }, new int[] { 4 }, new int[] { 1 } };
            yield return new object[] { new int[] { 1, 5, 6 }, new int[] { }, new int[] { } };
            yield return new object[] { new int[] { 2, 3, 4 }, new int[] { 2 }, new int[] { 3 } };
            yield return new object[] { new int[] { 2, 3, 5 }, new int[] { 2, 5 }, new int[] { 2, 1 } };
            yield return new object[] { new int[] { 2, 3, 6 }, new int[] { 2 }, new int[] { 2 } };
            yield return new object[] { new int[] { 2, 4, 5 }, new int[] { 2, 4 }, new int[] { 1, 2 } };
            yield return new object[] { new int[] { 2, 4, 6 }, new int[] { 2, 4 }, new int[] { 1, 1 } };
            yield return new object[] { new int[] { 2, 5, 6 }, new int[] { 2 }, new int[] { 1 } };
            yield return new object[] { new int[] { 3, 4, 5 }, new int[] { 3 }, new int[] { 3 } };
            yield return new object[] { new int[] { 3, 4, 6 }, new int[] { 3 }, new int[] { 2 } };
            yield return new object[] { new int[] { 3, 5, 6 }, new int[] { 3 }, new int[] { 1 } };
            yield return new object[] { new int[] { 4, 5, 6 }, new int[] { }, new int[] { } };
            yield return new object[] { new int[] { 1, 2, 3, 4 }, new int[] { }, new int[] { } };
            yield return new object[] { new int[] { 1, 2, 3, 5 }, new int[] { 5 }, new int[] { 1 } };
            yield return new object[] { new int[] { 1, 2, 3, 6 }, new int[] { }, new int[] { } };
            yield return new object[] { new int[] { 1, 2, 4, 5 }, new int[] { 4 }, new int[] { 2 } };
            yield return new object[] { new int[] { 1, 2, 4, 6 }, new int[] { 4 }, new int[] { 1 } };
            yield return new object[] { new int[] { 1, 2, 5, 6 }, new int[] { }, new int[] { } };
            yield return new object[] { new int[] { 1, 3, 4, 5 }, new int[] { 3 }, new int[] { 3 } };
            yield return new object[] { new int[] { 1, 3, 4, 6 }, new int[] { 3 }, new int[] { 2 } };
            yield return new object[] { new int[] { 1, 3, 5, 6 }, new int[] { 3 }, new int[] { 1 } };
            yield return new object[] { new int[] { 1, 4, 5, 6 }, new int[] { }, new int[] { } };
            yield return new object[] { new int[] { 2, 3, 4, 5 }, new int[] { 2 }, new int[] { 4 } };
            yield return new object[] { new int[] { 2, 3, 4, 6 }, new int[] { 2 }, new int[] { 3 } };
            yield return new object[] { new int[] { 2, 3, 5, 6 }, new int[] { 2 }, new int[] { 2 } };
            yield return new object[] { new int[] { 2, 4, 5, 6 }, new int[] { 2 }, new int[] { 1 } };
            yield return new object[] { new int[] { 3, 4, 5, 6 }, new int[] { }, new int[] { } };
            yield return new object[] { new int[] { 1, 2, 3, 4, 5 }, new int[] { }, new int[] { } };
            yield return new object[] { new int[] { 1, 2, 3, 4, 6 }, new int[] { }, new int[] { } };
            yield return new object[] { new int[] { 1, 2, 3, 5, 6 }, new int[] { }, new int[] { } };
            yield return new object[] { new int[] { 1, 2, 4, 5, 6 }, new int[] { }, new int[] { } };
            yield return new object[] { new int[] { 1, 3, 4, 5, 6 }, new int[] { }, new int[] { } };
            yield return new object[] { new int[] { 2, 3, 4, 5, 6 }, new int[] { }, new int[] { } };
            yield return new object[] { new int[] { 1, 2, 3, 4, 5, 6 }, new int[] { }, new int[] { } };
        }

        [Theory]
        [MemberData(nameof(Cases))]
        public void GetGapsInDay_AllCombinations(int[] emptySlots, int[] expectedStarts, int[] expectedSizes)
        {
            var result = Invoke(emptySlots);

            result.Select(r => r.Start).Should().Equal(expectedStarts,
                because: $"empty={{[{string.Join(",", emptySlots)}]}}");
            result.Select(r => r.Size).Should().Equal(expectedSizes,
                because: $"empty={{[{string.Join(",", emptySlots)}]}}");
        }
    }
}
