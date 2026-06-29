using MyTimetable.Models;

namespace MyTimetable.Planning
{
    // Одно решение планировщика: в какой слот какой кастомный урок поставить.
    public record class PlannedSlot
    {
        public required Slot Slot;
        public required CustomLesson Lesson;
    }
}
