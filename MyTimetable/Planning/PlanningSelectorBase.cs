using MyTimetable.Models;

namespace MyTimetable.Planning
{
    // Стратегии планирования: получают КОПИЮ очереди и КОПИЮ пула свободных слотов, выдают размещения.
    // Селектор создаётся заново на каждый Planner.Plan с актуальными очередью и пулом, поэтому снимки
    // (порядок RoundRobin, пропорции FairShare) можно брать прямо в конструкторе — устаревать нечему.
    //
    // Обе входные коллекции копируются:
    //  • Queue — потому что Planner декрементит свою живую _queue по каждому выданному PlannedSlot;
    //    селектор ведёт собственный счётчик и чужую очередь не трогает (иначе двойной декремент).
    //  • Fillable — рабочее промежуточное состояние: стратегия потребляет/переупорядочивает пул по ходу
    //    (например, заполнение дырок выбирает слоты не по порядку). Список вызывающего остаётся нетронут.
    //
    // На каждом шаге база берёт слот через TakeSlot() (по умолчанию ближайший по порядку — слот-стратегии
    // переопределяют) и предмет через PickSubject(). «Пропуск» — это просто меньше выданных PlannedSlot.
    public abstract class PlanningSelectorBase : IPlanningSelector
    {
        protected readonly Dictionary<string, int> Queue;
        protected readonly List<Slot> Fillable;

        protected PlanningSelectorBase(Dictionary<string, int> queue, List<Slot> fillable)
        {
            Queue = new Dictionary<string, int>(queue);
            Fillable = new List<Slot>(fillable);
        }

        // Предметы, у которых ещё есть что ставить.
        protected IEnumerable<KeyValuePair<string, int>> Available
            => Queue.Where(kv => kv.Value > 0);

        // Берёт следующий слот для заполнения. По умолчанию — ближайший по порядку (убирая его из Fillable);
        // слот-стратегии (дырки, равномерный разброс, лимит на день) переопределяют выбор. null — слотов
        // для размещения больше нет: Plan на этом заканчивает (даже если очередь ещё не пуста).
        protected virtual Slot? TakeSlot()
        {
            if (Fillable.Count == 0) return null;
            Slot slot = Fillable[0];
            Fillable.RemoveAt(0);
            return slot;
        }

        // Выбор предмета для очередного размещения. null — подходящего предмета нет (например, очередь
        // пуста): Plan на этом заканчивает, симметрично null из TakeSlot.
        protected abstract string? PickSubject();

        // Цикл: тянем слот и предмет, любой null означает конец. Никаких предварительных проверок —
        // оба условия остановки выражены одинаково через null.
        public IEnumerable<PlannedSlot> Plan()
        {
            while (true)
            {
                Slot? slot = TakeSlot();
                if (slot is null) yield break;
                string? title = PickSubject();
                if (title is null) yield break;
                Queue[title]--;
                yield return new PlannedSlot
                {
                    Slot = slot,
                    Lesson = new CustomLesson { LessonType = "PRACTICE", Title = title }
                };
            }
        }
    }
}
