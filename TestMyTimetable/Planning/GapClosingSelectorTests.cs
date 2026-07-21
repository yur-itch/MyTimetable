using System.Reflection;
using MyTimetable.Planning;
using MyTimetable.Models;
using FluentAssertions;
using SlotRange = MyTimetable.Planning.Range;

namespace TestMyTimetable.Planning
{
    public class TestGapClosingSelector
    {
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
