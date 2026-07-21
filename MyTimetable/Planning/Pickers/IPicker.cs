namespace MyTimetable.Planning;

public interface IPicker
{
    string? Peek(Dictionary<string, int> queue);
    void Commit(string title);
}
