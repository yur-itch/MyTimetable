using System.Reflection;
using MyTimetable.Planning;
using FluentAssertions;

namespace TestMyTimetable.Planning
{
    public class TestGapClosingSelector
    {
        private static List<GapRange> Invoke(int[] emptySlots, int slotCount = 6)
        {
            var method = typeof(GapClosingSlotter)
                .GetMethod("GetGapsInDay", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException("GetGapsInDay not found");

            return ((IEnumerable<GapRange>)method.Invoke(null, [emptySlots.ToList(), slotCount])!).ToList();
        }

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
