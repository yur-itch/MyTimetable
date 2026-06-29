namespace MyTimetable
{
    // Машина времени для отладки: сдвигает «сейчас» на фиксированный интервал, чтобы посмотреть приложение
    // как будто сегодня другая дата (например, откатиться к началу учебного года и погонять планировщик).
    // Сдвиг задаётся конфигом Schedule:DateOffsetDays; 0 — обычное системное время (см. Program.cs).
    public sealed class OffsetTimeProvider : TimeProvider
    {
        private readonly TimeSpan _offset;

        public OffsetTimeProvider(TimeSpan offset) => _offset = offset;

        public override DateTimeOffset GetUtcNow() => TimeProvider.System.GetUtcNow() + _offset;
    }
}
