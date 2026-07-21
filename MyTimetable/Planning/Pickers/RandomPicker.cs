namespace MyTimetable.Planning;

public sealed class RandomPicker : IPicker
{
    private readonly Random _random;

    public RandomPicker(Random? random = null)
        => _random = random ?? Random.Shared;

    public string? Peek(Dictionary<string, int> queue)
    {
        var available = queue.Where(kv => kv.Value > 0).Select(kv => kv.Key).ToList();
        if (available.Count == 0) return null;
        return available[_random.Next(available.Count)];
    }

    public void Commit(string title) { }
}
