namespace MyTimetable.Planning;

public interface IPickerFactory
{
    IPicker Create(Dictionary<string, int> queue);
}
