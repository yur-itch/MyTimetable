namespace MyTimetable.Planning;

public interface IPicker
{
    string? Pick(Dictionary<string, int> queue);
}
