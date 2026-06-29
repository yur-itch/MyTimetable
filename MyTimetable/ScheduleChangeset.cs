using MyTimetable.Models;
using MyTimetable.Planning;

namespace MyTimetable
{
    // Набор решений планировщика по кастомным урокам: какие слоты получают новый кастомный урок,
    // а какие — очищаются. Планировщик владеет только кастомными уроками: дефолтные принадлежат API
    // (перезаписываются воркером), скрытия — пользователю (Deactivations), поэтому changeset их не трогает.
    //
    // Ключ — слот, поэтому повторное решение по тому же слоту перезаписывает прежнее: итоговое состояние
    // каждого слота однозначно, а применение к БД идемпотентно.
    public sealed class ScheduleChangeset
    {
        private readonly Dictionary<Slot, CustomLesson?> _customLessons = new();

        public void SetCustomLesson(PlannedSlot planned) => _customLessons[planned.Slot] = planned.Lesson;
        public void RemoveCustomLesson(Slot slot) => _customLessons[slot] = null;

        public IReadOnlyDictionary<Slot, CustomLesson?> CustomLessons => _customLessons;

        public bool IsEmpty => _customLessons.Count == 0;
    }
}
