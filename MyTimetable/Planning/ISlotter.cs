using MyTimetable.Models;

namespace MyTimetable.Planning;

public interface ISlotter
{
    Slot? NextSlot();
}
