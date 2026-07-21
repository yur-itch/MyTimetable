namespace MyTimetable.Planning;

public sealed class SmallestQueueFirstPicker : IPicker
{
    public string? Peek(Dictionary<string, int> queue)
    {
        var available = queue.Where(kv => kv.Value > 0).ToList();
        if (available.Count == 0) return null;
        return available.MinBy(kv => kv.Value).Key;
    }

    public void Commit(string title) { }
}
