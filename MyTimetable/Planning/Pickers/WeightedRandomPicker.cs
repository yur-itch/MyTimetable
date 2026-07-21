namespace MyTimetable.Planning;

public sealed class WeightedRandomPicker : IPicker
{
    private readonly Random _random;

    public WeightedRandomPicker(Random? random = null)
        => _random = random ?? Random.Shared;

    public string? Peek(Dictionary<string, int> queue)
    {
        var available = queue.Where(kv => kv.Value > 0).ToList();
        if (available.Count == 0) return null;

        int total = 0;
        foreach (var kv in available) total += kv.Value;

        int roll = _random.Next(total);
        int accumulated = 0;
        foreach (var kv in available)
        {
            accumulated += kv.Value;
            if (roll < accumulated) return kv.Key;
        }
        return available[^1].Key;
    }

    public void Commit(string title) { }
}
