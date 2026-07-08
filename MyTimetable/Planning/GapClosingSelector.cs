using MyTimetable.Models;
using System.Collections;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace MyTimetable.Planning
{
    record class Range
    {
        public required int Size;
        public required int Start;
    }

    record class SlotRange
    {
        public required int Size;
        public required Slot First;

        public IEnumerable<Slot> Iterate()
        {
            for (int i = 0; i < Size; i++)
            {
                yield return new Slot()
                {
                    Date = First.Date,
                    Number = First.Number + i
                };
            }
        }
    }

    public class GapClosingSelector : PlanningSelectorBase
    {
        private readonly int _slotCount;

        private static void FirstAndLastOccupied(List<int> day, int slotCount, out int? firstTaken, out int? lastTaken)
        {
            firstTaken = null;
            lastTaken = null;

            if (day.Count == 0)
            {
                firstTaken = 1;
                lastTaken = slotCount;
                return;
            }
            if (day.Count == slotCount) return;

            if (day[0] > 1)
            {
                firstTaken = 1;
                lastTaken = day[0] - 1;
                if (day[day.Count - 1] < slotCount)
                {
                    lastTaken = slotCount;
                }
            }
            else if (day[day.Count - 1] < slotCount)
            {
                lastTaken = slotCount;
                int gap = 1;
                foreach (int v in day)
                {
                    if (v == gap) gap++;
                    else break;
                }
                firstTaken = gap;
            }

            if (lastTaken != slotCount || firstTaken != 1)
            {
                int dayIndex = 1;
                for (int i = day[0] + 1; i < slotCount; i++)
                {
                    if (dayIndex == day.Count)
                    {
                        break;
                    }
                    if (day[dayIndex] > i)
                    {
                        if (firstTaken == null)
                        {
                            firstTaken = i;
                        }
                        if (lastTaken != slotCount)
                        {
                            lastTaken = i;
                        }
                    }
                    else
                    {
                        dayIndex++;
                    }
                }
            }
        }

        private IEnumerable<Range> GetGapsInDay(List<int> day)
        {
            FirstAndLastOccupied(day, _slotCount, out int? firstTaken, out int? lastTaken);

            if (firstTaken == lastTaken || day.Count == 0) yield break;

            int start = 0;
            for (; start < day.Count; start++)
            {
                int val = day[start];
                if (val > firstTaken) break;
            }

            if (start >= day.Count || day[start] >= lastTaken) yield break;

            int prevVal = day[start];
            int startGap = day[start];
            for (int i = start + 1; i < day.Count; i++)
            {
                int val = day[i];
                if (val >= lastTaken)
                {
                    yield return new Range()
                    {
                        Size = prevVal - startGap + 1,
                        Start = startGap
                    };
                    yield break;
                }
                if (prevVal < val - 1)
                {
                    yield return new Range()
                    {
                        Size = prevVal - startGap + 1,
                        Start = startGap
                    };
                    startGap = val;
                }
                prevVal = val;
            }
            if (prevVal < lastTaken)
            {
                yield return new Range()
                {
                    Size = prevVal - startGap + 1,
                    Start = startGap
                };
            }
        }

        // Один курсор по упорядоченной последовательности слотов дырок: TakeSlot тянет из него по одному,
        // null — дырки кончились. Красивый LINQ строит порядок, енумератор отдаёт его pull-семантикой.
        private readonly IEnumerator<Slot> _gapSlots;
        private readonly List<string> _order;
        private int _position = 0;

        protected override string? PickSubject()
        {
            for (int i = 0; i < _order.Count; i++)
            {
                int index = (_position + i) % _order.Count;
                string key = _order[index];
                if (Queue.TryGetValue(key, out int remaining) && remaining > 0)
                {
                    _position = (index + 1) % _order.Count;
                    return key;
                }
            }
            return null; // ни одного предмета с остатком — остановка
        }

        public GapClosingSelector(Dictionary<string, int> queue, List<Slot> fillable, int slotCount = 6) : base(queue, fillable)
        {
            _slotCount = slotCount;
            _order = Queue.Keys.ToList();
            _gapSlots = DayLayout.GetDays(fillable)
                .SelectMany(x =>
                {
                    var ranges = GetGapsInDay(x.Open);
                    return ranges.Select(y => new SlotRange()
                    {
                        First = new Slot()
                        {
                            Date = x.Date,
                            Number = y.Start
                        },
                        Size = y.Size
                    });
                })
                .OrderBy(x => x.Size)
                .ThenBy(x => x.First.Date)
                .ThenBy(x => x.First.Number)
                .SelectMany(x => x.Iterate())
                .GetEnumerator();
        }

        // База сама дёргает PickSubject, декрементит очередь и останавливается на пустой очереди — мы лишь
        // отдаём следующий слот дырки (или null, когда дырки кончились).
        protected override Slot? TakeSlot()
            => _gapSlots.MoveNext() ? _gapSlots.Current : null;
    }
}