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
    // Слоты и предметы разделены на Peek/Commit:
    //  • Peek выдаёт кандидата, не мутируя внутреннее состояние.
    //  • Commit фиксирует выбор только после того, как планировщик подтвердил размещение.
    // Это позволяет планировщику отклонять предложения (например, "нет пар по воскресеньям"),
    // и селектор пробует альтернативы без потери состояния.
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

        // --- Слоты ---

        // По умолчанию — ближайший по порядку слот (без удаления из Fillable).
        // Слот-стратегии переопределяют, делегируя своему ISlotter.Peek().
        // null — слотов больше нет.
        protected virtual Slot? PeekSlot()
        {
            if (Fillable.Count == 0) return null;
            return Fillable[0];
        }

        // Фиксирует занятие слота. По умолчанию — удаляет из Fillable.
        // Слот-стратегии переопределяют, делегируя своему ISlotter.Commit().
        protected virtual void CommitSlot(Slot slot)
        {
            Fillable.RemoveAt(0);
        }

        // --- Предметы ---

        // Выбор предмета-кандидата. Не мутирует состояние пикера.
        // null — подходящих предметов нет.
        protected abstract string? PeekSubject();

        // Фиксирует выбор предмета. По умолчанию пусто — для stateless-пикеров.
        // Stateful-пикеры переопределяют, делегируя своему IPicker.Commit().
        protected virtual void CommitSubject(string title) { }

        // Основной цикл с обратной связью от планировщика.
        // Планировщик через accept говорит, OK ли слот и OK ли предмет.
        // Селектор механически реагирует на два бита:
        //   (T,T) — размещение принято: коммитим слот и предмет, выдаём PlannedSlot.
        //   (T,F) — слот подходит, но предмет нет: пробуем другой предмет в тот же слот.
        //   (F,T) — предмет подходит, но слот нет: держим предмет, берём другой слот.
        //   (F,F) — не подходит ни то, ни другое: оба новые.
        public IEnumerable<PlannedSlot> Plan(Func<Slot, string, ProposeResult> accept)
        {
            Slot? slot = PeekSlot();
            if (slot is null) yield break;
            string? title = PeekSubject();
            if (title is null) yield break;

            bool newSlot = false;
            bool newSubject = false;

            while (true)
            {
                if (newSlot)
                {
                    slot = PeekSlot();
                    if (slot is null) yield break;
                    newSlot = false;
                }
                if (newSubject)
                {
                    title = PeekSubject();
                    if (title is null) yield break;
                    newSubject = false;
                }

                ProposeResult result = accept(slot, title);

                if (result.SlotOK && result.SubjectOK)
                {
                    CommitSlot(slot);
                    CommitSubject(title);
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
