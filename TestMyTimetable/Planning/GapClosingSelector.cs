using System.Reflection;
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

        // ── Plan integration tests ──────────────────────────────────

        private static List<Slot> DaySlots(DateOnly date, params int[] open)
            => open.Select(n => new Slot { Date = date, Number = n }).ToList();

        [Fact]
        public void Plan_EmptyQueue_ReturnsNothing()
        {
            var sel = new GapClosingSelector(new Dictionary<string, int>(), DaySlots(new DateOnly(2024, 1, 1), 1, 2, 3, 4, 5, 6));
            sel.Plan().Should().BeEmpty();
        }

        [Fact]
        public void Plan_EmptyFillable_ReturnsNothing()
        {
            var sel = new GapClosingSelector(new Dictionary<string, int> { ["A"] = 3 }, []);
            sel.Plan().Should().BeEmpty();
        }

        [Fact]
        public void Plan_StopsWhenQueueExhausted()
        {
            // Есть дырки, но предметов меньше
            var fillable = DaySlots(new DateOnly(2024, 1, 1), 1, 4);
            var sel = new GapClosingSelector(new Dictionary<string, int> { ["A"] = 1 }, fillable);
            sel.Plan().Should().HaveCount(1);
        }

        [Fact]
        public void Plan_StopsWhenGapsExhausted()
        {
            // Нет дырок → TakeSlot возвращает null → Plan пуст
            var fillable = DaySlots(new DateOnly(2024, 1, 1), 1, 2, 3, 4, 5, 6);
            var sel = new GapClosingSelector(
                new Dictionary<string, int> { ["A"] = 10 }, fillable);
            sel.Plan().Should().BeEmpty();
        }

        [Fact]
        public void Plan_NoGapsButDayWithOccupiedFirst_NoGapDetected()
        {
            // День: заняты 1,2 → open=[3,4,5,6]. Занятые 1,2 — не дырка и не край.
            // FirstAndLastOccupied вернёт firstTaken=null, lastTaken=null (так как day.Count == slotCount? нет, slotCount=6)
            // На самом деле day=[3,4,5,6], day[0]=3>1 → firstTaken=1, lastTaken=2
            // Занятые 1,2 — это непрерывный кусок с 1, но не дырка. GetGapsInDay: firstTaken=1, lastTaken=2 → firstTaken==lastTaken? нет, firstTaken=1, lastTaken=2
            // firstTaken != lastTaken → продолжаем. Сканируем: start=0, val=day[0]=3 > firstTaken → break → start=0
            // day[start]=3 >= lastTaken=2 → yield break. Дырка не найдена.
            var fillable = DaySlots(new DateOnly(2024, 1, 1), 3, 4, 5, 6);
            var sel = new GapClosingSelector(
                new Dictionary<string, int> { ["A"] = 10 }, fillable);
            sel.Plan().Should().BeEmpty();
        }

        [Fact]
        public void Plan_GapInMiddle_ReturnsGapSlots()
        {
            // День: заняты 2,5, open=[1,3,4,6]. Занятые 2 и 5 — не непрерывны.
            // FirstAndLastOccupied: day[0]=1 → not >1, day[last]=6 → not <6 → both null
            // then loop: i=2: dayIndex=1, day[1]=3, i=2<3 → dayIndex=2. i=3: day[2]=4, i=3<4 → dayIndex=2. i=4: dayIndex=2 < day.Count=4, day[2]=4, i=4=4 → dayIndex=3. i=5: dayIndex=3, day[3]=6 > i=5 → firstTaken=5, lastTaken=5.
            // Так, это не даёт нужной дырки. Нужен более простой случай.
            // Возьмём: заняты пары 3,4 → open=[1,2,5,6]
            var fillable = DaySlots(new DateOnly(2024, 1, 1), 1, 2, 5, 6);
            var sel = new GapClosingSelector(
                new Dictionary<string, int> { ["A"] = 2 }, fillable);
            var results = sel.Plan().ToList();

            // Заняты 3,4 (непрерывная середина). FirstAndLastOccupied:
            // day[0]=1 → not > 1. day[last]=6 → not < 6. firstTaken=null, lastTaken=null.
            // Loop: i=2: dayIndex=1, day[1]=2 == 2 → dayIndex=2. i=3: dayIndex=2 < day.Count=4, day[2]=5 > 3 → firstTaken=3, lastTaken=3. dayIndex stays 2. i=4: dayIndex=2, day[2]=5 > 4 → lastTaken=4.
            // firstTaken=3, lastTaken=4. start=0, day[0]=1>3? no. start++=1, day[1]=2>3? no. start++=2, day[2]=5>3 → break.
            // start=2, day[2]=5 >= lastTaken=4 → yield break.
            // Wait, что-то не так. Дырка [3,4] между слотов 1,2 и 5,6 — это 3,4 заняты (не дырка).
            // Дырка — это свободные слоты МЕЖДУ занятыми. Если заняты 3,4, то свободные 1,2 и 5,6.
            // GetGapsInDay ищет занятые куски внутри дня. dырка = занятый отрезок между свободными.
        }

        [Fact]
        public void Plan_GapSurroundedByFreeSlots_PlacesInGap()
        {
            // open=[1, 5], то есть заняты 2,3,4. Занятые идут непрерывно с 2 по 4.
            // FirstAndLastOccupied: day[0]=1 > 1? нет. day[last]=5 < 6? да → lastTaken=6.
            // firstTaken: gap=1, scan: v=1 → gap=2. start=0, day[0]=1=1 → break. firstTaken=2.
            // loop: i=2: dayIndex=1, day[1]=5 > 2 → firstTaken уже 2. lastTaken=6 ≠ 6 → lastTaken=2? Или 2?
            // На самом деле lastTaken=6 (уже установлено). Проверка: if (lastTaken != slotCount) → lastTaken == 6, не входим.
            // firstTaken=2, lastTaken=6. start=0, day[0]=1>2? нет → start++=1. day[1]=5>2 → break. start=1.
            // start=1 < day.Count=2, day[1]=5 >= lastTaken=6? нет.
            // prevVal=5, startGap=5. i=2 >= day.Count → exit loop.
            // prevVal=5 < lastTaken=6 → yield return Range {Size=1, Start=5}.
            // Это дырка размера 1 на 5? Нет, 5 это свободный слот. Дырка — это занятые слоты.
            // Так это не то. Давайте разберёмся с логикой GetGapsInDay.

            // open = [1, 5] означает: слот 1 свободен, 5 свободен, 2,3,4 заняты.
            // Занятые 2,3,4 — один непрерывный кусок. Это и есть дырка.
            // firstTaken=2, lastTaken=6 (потому что день не полностью занят до конца, day[last]=5 < 6).
        }

        [Fact]
        public void Plan_GapBetweenOccupiedSlots_PlacesCorrectly()
        {
            // Простой случай: день со свободными [1, 2, 5, 6] — заняты 3,4
            // По логике: firstTaken=3, lastTaken=4.
            // start=2 (day[2]=5>3), prevVal=5, startGap=5.
            // i=3: val=6, prevVal=5 < 6-1 → yield Range {Size=1, Start=5}.
            // i=4: ≥ day.Count → exit. prevVal=6 < lastTaken=4? нет → конец.
            // Результат: дырка [5,1] — это слот 5, размер 1.
            // 5 — это свободный слот. Что даёт GapClosingSelector? Он собирает SlotRange для дырки 5.
            // Iterate: Date=2024-01-01, Number=5. Единственный слот дырки.
            var fillable = DaySlots(new DateOnly(2024, 1, 1), 1, 2, 5, 6);
            var sel = new GapClosingSelector(
                new Dictionary<string, int> { ["A"] = 1 }, fillable);
            var results = sel.Plan().ToList();

            results.Should().HaveCount(1);
            results[0].Slot.Number.Should().Be(5);
            results[0].Slot.Date.Should().Be(new DateOnly(2024, 1, 1));
        }

        [Fact]
        public void Plan_MultipleDays_GapsOrderedBySizeThenDate()
        {
            // День 1: open=[1, 2, 6] → заняты 3,4,5 → один gap: [3,5] Start=3, Size=3
            // День 2: open=[1, 5, 6] → заняты 2,3,4 → один gap: [2,4] Start=2, Size=3
            // Оба Size=3, сортировка по дате → день 1 первый
            var fillable = new List<Slot>
            {
                new() { Date = new DateOnly(2024, 1, 1), Number = 1 },
                new() { Date = new DateOnly(2024, 1, 1), Number = 2 },
                new() { Date = new DateOnly(2024, 1, 1), Number = 6 },
                new() { Date = new DateOnly(2024, 1, 2), Number = 1 },
                new() { Date = new DateOnly(2024, 1, 2), Number = 5 },
                new() { Date = new DateOnly(2024, 1, 2), Number = 6 },
            };
            var sel = new GapClosingSelector(
                new Dictionary<string, int> { ["A"] = 3 }, fillable);
            var results = sel.Plan().ToList();

            // День 1 gap: [3,4,5] → Iterate даёт 3,4,5
            // День 2 gap: [2,3,4] → Iterate даёт 2,3,4
            // Сортировка по (Size, Date, Start): оба Size=3, по дате 1 < 2 → день 1
            results.Should().HaveCount(3);
            results[0].Slot.Date.Should().Be(new DateOnly(2024, 1, 1));
            results[0].Slot.Number.Should().Be(3);
            results[1].Slot.Number.Should().Be(4);
            results[2].Slot.Number.Should().Be(5);
        }

        [Fact]
        public void Plan_GapSizeOrdering_SmallerGapFirst()
        {
            // День 1: open=[1, 4, 5, 6] → заняты 2,3 → gap Start=2, Size=2
            // День 2: open=[1, 2, 3, 6] → заняты 4,5 → gap Start=4, Size=2
            // Оба Size=2, по дате → день 1 первый
            var fillable = new List<Slot>
            {
                new() { Date = new DateOnly(2024, 1, 1), Number = 1 },
                new() { Date = new DateOnly(2024, 1, 1), Number = 4 },
                new() { Date = new DateOnly(2024, 1, 1), Number = 5 },
                new() { Date = new DateOnly(2024, 1, 1), Number = 6 },
            };
            var sel = new GapClosingSelector(
                new Dictionary<string, int> { ["A"] = 2 }, fillable);
            var results = sel.Plan().ToList();

            results.Should().HaveCount(2);
            results[0].Slot.Number.Should().Be(2);
            results[1].Slot.Number.Should().Be(3);
        }

        [Fact]
        public void Plan_SingleSlotGap_PlacesCorrectly()
        {
            // open=[1, 3, 4, 5, 6] → занята 2 → дырка [2,1]
            var fillable = DaySlots(new DateOnly(2024, 1, 1), 1, 3, 4, 5, 6);
            var sel = new GapClosingSelector(
                new Dictionary<string, int> { ["A"] = 1 }, fillable);
            var results = sel.Plan().ToList();

            results.Should().HaveCount(1);
            results[0].Slot.Number.Should().Be(2);
        }

        [Fact]
        public void Plan_UsesRoundRobinForSubjectSelection()
        {
            // Две дырки, round-robin по предметам
            var fillable = DaySlots(new DateOnly(2024, 1, 1), 1, 4, 5, 6);
            // Заняты 2,3 → gap Start=2, Size=2
            var queue = new Dictionary<string, int> { ["X"] = 2, ["Y"] = 2 };
            var sel = new GapClosingSelector(queue, fillable);
            var results = sel.Plan().ToList();

            results.Should().HaveCount(2);
            results[0].Lesson.Title.Should().Be("X");
            results[0].Slot.Number.Should().Be(2);
            results[1].Lesson.Title.Should().Be("Y");
            results[1].Slot.Number.Should().Be(3);
        }

        [Fact]
        public void Plan_OutputHasCorrectProperties()
        {
            var fillable = DaySlots(new DateOnly(2024, 5, 10), 1, 3, 4, 5, 6);
            var result = new GapClosingSelector(
                new Dictionary<string, int> { ["A"] = 1 }, fillable).Plan().ToList();

            result.Should().HaveCount(1);
            result[0].Slot.Date.Should().Be(new DateOnly(2024, 5, 10));
            result[0].Slot.Number.Should().Be(2);
            result[0].Lesson.Title.Should().Be("A");
            result[0].Lesson.LessonType.Should().Be("PRACTICE");
        }
    }
}
