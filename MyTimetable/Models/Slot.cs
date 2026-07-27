namespace MyTimetable.Models
{
    public readonly record struct Slot(DateOnly Date, int Number);
    //public class AvailableSlot : Slot { public AvailableSlot(DateOnly date, int number) : base(date, number) { } }
}
