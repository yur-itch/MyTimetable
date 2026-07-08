namespace MyTimetable
{
    // Модель страницы планирования: фиксированный на старте список стратегий + текущая очередь предметов
    // (остатки после планирования и предметы, возвращённые снятием конфликтов). Очередь рендерится в строки
    // формы, чтобы открытая страница сразу показывала, что ещё предстоит расставить.
    public sealed class PlanView
    {
        public required IReadOnlyCollection<string> Strategies { get; init; }
        public required IReadOnlyDictionary<string, int> Queue { get; init; }
    }
}
