
namespace MyTimetable.Planning;

public interface ISlotter
{
    Slot? Peek();
    void Commit(Slot slot);
}
