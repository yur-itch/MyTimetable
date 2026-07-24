using MyTimetable.Entities;
using MyTimetable.Planning;

namespace MyTimetable
{
    // Запись-сторона перевода: ScheduleChangeset -> сущности БД. Зеркало ScheduleBuilder, который читает
    // сущности в доменную модель; здесь обратное направление. Любой производитель changeset (не только
    // Planner) кладёт изменения через него.
    //
    // Только СТАВИТ изменения в контекст — SaveChanges не зовёт: единицей работы владеет вызывающий
    // (запрос в контроллере, scope воркера), он и коммитит, возможно вместе с другими изменениями.
    // Касается лишь CustomLessons — единственного, что описывает changeset.
    public sealed class ChangesetApplier
    {
        public async Task Apply(AppDbContext db, ScheduleChangeset changeset)
        {
            foreach ((Slot slot, CustomLesson? lesson) in changeset.CustomLessons)
            {
                var key = new object[] { slot.Date, slot.Number };
                CustomLessonEntry? existing = await db.CustomLessons.FindAsync(key);
                if (lesson == null)
                {
                    if (existing != null) db.CustomLessons.Remove(existing);
                }
                else if (existing == null)
                {
                    await db.CustomLessons.AddAsync(new CustomLessonEntry
                    {
                        Date = slot.Date,
                        LessonNumber = slot.Number,
                        Title = lesson.Title,
                        LessonType = lesson.LessonType
                    });
                }
                else
                {
                    existing.Title = lesson.Title;
                    existing.LessonType = lesson.LessonType;
                }
            }
        }
    }
}
