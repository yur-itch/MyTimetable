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

        // Основной цикл с обратной связью от планировщика.
        // Планировщик через accept говорит, OK ли слот и OK ли предмет.
        // Селектор механически реагирует на два бита:
        //   (T,T) — размещение принято: коммитим слот и предмет, выдаём PlannedSlot.
        //   (T,F) — слот подходит, но предмет нет: пробуем другой предмет в тот же слот.
        //   (F,T) — предмет подходит, но слот нет: держим предмет, берём другой слот.
        //   (F,F) — не подходит ни то, ни другое: оба новые.
        public IEnumerable<PlannedSlot> Plan(Func<Slot, string, ProposeResult> accept)
        {
            Slot? slot = TakeSlot();
            if (slot is null) yield break;
            string? title = PickSubject();
            if (title is null) yield break;

            bool newSlot = false;
            bool newSubject = false;

            while (true)
            {
                if (newSlot)
                {
                    slot = TakeSlot();
                    if (slot is null) yield break;
                    newSlot = false;
                }
                if (newSubject)
                {
                    title = PickSubject();
                    if (title is null) yield break;
                    newSubject = false;
                }

                ProposeResult result = accept(slot, title);

                if (result.SlotOK && result.SubjectOK)
                {
                    Queue[title]--;
                    yield return new PlannedSlot
                    {
                        Slot = slot,
                        Lesson = new CustomLesson { LessonType = "PRACTICE", Title = title }
                    };
                    newSlot = true;
                    newSubject = true;
                }
                else if (result.SlotOK && !result.SubjectOK)
                {
                    newSubject = true;
                }
                else if (!result.SlotOK && result.SubjectOK)
                {
                    newSlot = true;
                }
                else
                {
                    newSlot = true;
                    newSubject = true;
                }
            }
        }
    }
}
