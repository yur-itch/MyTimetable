
namespace MyTimetable.Planning;

public interface ISlotterFactory
{
    ISlotter Create(List<Slot> fillable);
}
