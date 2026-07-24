using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace MyTimetable.Models
{
    public record class Cell
    {
        public required DefaultLesson? DefaultLesson { get; set; }
        public required CustomLesson? CustomLesson { get; set; }
        public required bool Hidden { get; set; }
    }

    public record class DaySchedule
    {
        public const int DefaultSlotCount = 6;

        public required DateOnly Date;
        public required Cell[] Cells;

        public IEnumerable<Slot> GetSlots()
            => Cells.Select((_, index) => SlotAt(index));

        public IEnumerable<Slot> GetDefaultOccupiedSlots()
            => Cells
                .Select((cell, index) => (cell, index))
                .Where(x => x.cell.DefaultLesson != null)
                .Select(x => SlotAt(x.index));

        public IEnumerable<Slot> GetCustomOccupiedSlots()
            => Cells
                .Select((cell, index) => (cell, index))
                .Where(x => x.cell.CustomLesson != null)
                .Select(x => SlotAt(x.index));

        public IEnumerable<Slot> GetHiddenSlots()
            => Cells
                .Select((cell, index) => (cell, index))
                .Where(x => x.cell.Hidden)
                .Select(x => SlotAt(x.index));

        private Slot SlotAt(int index) => new()
        {
            Date = Date,
            Number = index + 1
        };

        public static DaySchedule Empty(DateOnly date, int slotCount = DefaultSlotCount)
            => new()
            {
                Date = date,
                Cells = Enumerable.Range(0, slotCount)
                    .Select(_ => new Cell
                    {
                        DefaultLesson = null,
                        CustomLesson = null,
                        Hidden = false
                    })
                    .ToArray()
            };
    }

    public sealed class CalendarSchedule : IReadOnlyList<DaySchedule>
    {
        private readonly IReadOnlyList<DaySchedule> _days;

        public CalendarSchedule(IEnumerable<DaySchedule> days)
        {
            var source = days.ToList();
            if (source.Count == 0)
            {
                _days = Array.Empty<DaySchedule>();
                return;
            }

            var duplicates = source
                .GroupBy(x => x.Date)
                .Where(x => x.Count() > 1)
                .Select(x => x.Key)
                .ToArray();

            if (duplicates.Length > 0)
            {
                throw new ArgumentException(
                    $"Calendar schedule contains duplicate dates: {string.Join(", ", duplicates)}",
                    nameof(days));
            }

            int slotCount = source.Max(x => x.Cells.Length);
            var byDate = source.ToDictionary(x => x.Date);
            DateOnly from = byDate.Keys.Min();
            DateOnly to = byDate.Keys.Max();

            _days = Enumerable.Range(0, to.DayNumber - from.DayNumber + 1)
                .Select(offset => from.AddDays(offset))
                .Select(date => byDate.TryGetValue(date, out DaySchedule? day)
                    ? day
                    : DaySchedule.Empty(date, slotCount))
                .ToArray();
        }

        public int Count => _days.Count;
        public DateOnly From => _days.Count > 0 ? _days[0].Date : throw new InvalidOperationException("Schedule is empty.");
        public DateOnly To => _days.Count > 0 ? _days[^1].Date : throw new InvalidOperationException("Schedule is empty.");
        public DaySchedule this[int index] => _days[index];

        public Cell? SlotToCell(Slot slot)
        {
            if (_days.Count == 0 || slot.Date < From || slot.Date > To)
            {
                return null;
            }

            DaySchedule day = DateToDaySchedule(slot.Date)!;
            int cellIndex = slot.Number - 1;

            return cellIndex >= 0 && cellIndex < day.Cells.Length
                ? day.Cells[cellIndex]
                : null;
        }

        public DaySchedule? DateToDaySchedule(DateOnly date)
        {
            return _days[date.DayNumber - From.DayNumber];
        }

        public IEnumerable<IReadOnlyList<DaySchedule>> GetWeeks()
        {
            if (_days.Count == 0)
            {
                yield break;
            }

            int previousWeek = WeekNumber(_days[0].Date);
            List<DaySchedule> week = new();

            foreach (DaySchedule day in _days)
            {
                int currentWeek = WeekNumber(day.Date);
                if (currentWeek != previousWeek)
                {
                    yield return week;
                    week = new List<DaySchedule>();
                    previousWeek = currentWeek;
                }

                week.Add(day);
            }

            if (week.Count > 0)
            {
                yield return week;
            }
        }

        public IEnumerator<DaySchedule> GetEnumerator() => _days.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private static int WeekNumber(DateOnly date)
        {
            int dayOfWeek = ((int)date.DayOfWeek + 6) % 7;
            return (date.DayNumber - dayOfWeek) / 7;
        }
    }

    public record class Professor
    {
        public required string Name;
        public required bool IsShown;
    }

    public record class Lesson
    {
        public required string Title { get; set; }
        public required string LessonType { get; set; }
    }

    public record class DefaultLesson : Lesson
    {
        public required Professor Professor { get; set; }
        public required string Room { get; set; }
    }
    public record class CustomLesson : Lesson { }

}
