
namespace MyTimetable.Planning
{
    // Один день как список НОМЕРОВ свободных пар (Open). Занятые пары — дополнение Open в 1..slotCount.
    internal sealed record Day
    {
        public required DateOnly Date;
        public required List<int> Open;
    }

    // Максимальный непрерывный отрезок ЗАНЯТЫХ пар внутри одного дня.
    internal readonly record struct Chunk
    {
        public required int Start { get; init; }   // первая занятая пара (нумерация с 1)
        public required int Length { get; init; }  // сколько занятых пар подряд
        public int End => Start + Length - 1;
    }

    // Общая геометрия дня для слот-стратегий: группировка пула в дни и поиск краевых кусков занятых пар.
    internal static class DayLayout
    {
        // Группирует плоский упорядоченный пул свободных слотов в дни с их свободными номерами.
        public static IEnumerable<Day> GetDays(List<Slot> slots)
        {
            if (slots.Count == 0) yield break;
            DateOnly date = slots.First().Date;
            List<int> day = [slots[0].Number];
            for (int i = 1; i < slots.Count; i++)
            {
                Slot slot = slots[i];
                if (slot.Date != date)
                {
                    yield return new Day { Date = date, Open = new List<int>(day) };
                    date = slot.Date;
                    day.Clear();
                }
                day.Add(slot.Number);
            }
            if (day.Any())
            {
                yield return new Day { Date = date, Open = new List<int>(day) };
            }
        }

        // Первый кусок занятых пар, либо null если занятых пар нет (день полностью свободен).
        // open — отсортированные по возрастанию номера свободных пар в пределах 1..slotCount.
        public static Chunk? FirstChunk(List<int> open, int slotCount)
        {
            int expected = 1;
            int idx = 0;
            while (idx < open.Count && open[idx] == expected) { idx++; expected++; }
            if (expected > slotCount) return null; // занятых пар нет

            int start = expected;
            // Следующая свободная пара после куска ограничивает его; если её нет — кусок идёт до конца дня.
            int nextOpen = idx < open.Count ? open[idx] : slotCount + 1;
            return new Chunk { Start = start, Length = nextOpen - start };
        }

        // Последний кусок занятых пар, либо null если занятых пар нет.
        public static Chunk? LastChunk(List<int> open, int slotCount)
        {
            int expected = slotCount;
            int idx = open.Count - 1;
            while (idx >= 0 && open[idx] == expected) { idx--; expected--; }
            if (expected < 1) return null; // занятых пар нет

            int end = expected;
            // Предыдущая свободная пара под куском ограничивает его снизу; если её нет — кусок с начала дня.
            int prevOpen = idx >= 0 ? open[idx] : 0;
            return new Chunk { Start = prevOpen + 1, Length = end - prevOpen };
        }
    }
}
