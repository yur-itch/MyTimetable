namespace MyTimetable.Models
{
    public record class Slot
    {
        public required DateOnly Date;
        public required int Number;
    }
    //public class AvailableSlot : Slot { public AvailableSlot(DateOnly date, int number) : base(date, number) { } }
}
