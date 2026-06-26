using MyTimetable.Models;

namespace MyTimetable
{
    public sealed class Planner
    {
        private IServiceProvider _serviceProvider;
        public Dictionary<string, int> Queue = new();
        public ScheduleBuilder _scheduleBuilder;
        
        public Planner(IServiceProvider provider, ScheduleBuilder builder)
        {
            _serviceProvider = provider;
            _scheduleBuilder = builder;
        }

        //public List<Slot> GetAllSlots(DateOnly from, DateOnly to)
        //{
        //    return Enumerable
        //        .Range(from.DayNumber, to.DayNumber - from.DayNumber + 1)
        //        .SelectMany(dayNumber => Enumerable
        //            .Range(1, 6)
        //            .Select(number => new Slot(
        //                DateOnly.FromDayNumber(dayNumber),
        //                number
        //            ))
        //        )
        //        .ToList();
        //}

        //public List<Slot> GetAvailableSlots(DateOnly from, DateOnly to)
        //{
        //    using var scope = _serviceProvider.CreateScope();
        //    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        //    var allSlots = GetAllSlots(from, to);
        //    // TODO: finish
        //    return allSlots;
        //}

    }
}
