namespace MyTimetable.Models
{
    // Вью-модель главной страницы: уже отрендеренные партиалы дней (в порядке дат) плюс то, что
    // шаблону нужно помимо них. Страница собирается склейкой готовых кусочков, а не повторным
    // рендером каждого дня из CalendarSchedule — день рендерится один раз (в PartialViewResult).
    public sealed record PageView
    {
        public required int SlotCount { get; init; }
        public required string ScrollTarget { get; init; }
        public required IReadOnlyList<string> Days { get; init; }
    }

    public sealed record TableView {
        public required int SlotCount { get; init; }
        public required string ScrollTarget { get; init; }
        public required IReadOnlyList<string> Days { get; init; }
    }
}
