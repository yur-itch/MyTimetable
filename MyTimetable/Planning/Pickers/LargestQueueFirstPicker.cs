namespace MyTimetable.Planning;

public sealed class LargestQueueFirstPicker : IPicker
{
    public string? Peek(Dictionary<string, int> queue)
    {
        var available = queue.Where(kv => kv.Value > 0).ToList();
        if (available.Count == 0) return null;
        return available.MaxBy(kv => kv.Value).Key;
    }

    public void Commit(string title) { }
}
