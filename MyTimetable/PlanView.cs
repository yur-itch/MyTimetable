namespace MyTimetable
{
    // Модель страницы планирования: списки для UI (пребилды, пикеры, слоттеры) + текущая очередь предметов.
    public sealed class PlanView
    {
        public required IReadOnlyCollection<string> PrebuiltNames { get; init; }
        public required IReadOnlyCollection<string> PickerNames { get; init; }
        public required IReadOnlyCollection<string> SlotterNames { get; init; }
        public required IReadOnlyDictionary<string, int> Queue { get; init; }
    }
}
