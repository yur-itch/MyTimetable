namespace MyTimetable.Planning;

public sealed class FairSharePicker : IPicker
{
    private readonly Dictionary<string, double> _desired;
    private readonly Dictionary<string, int> _placed = new();
    private int _placedTotal;

    public FairSharePicker(Dictionary<string, int> queue)
    {
        int total = queue.Values.Sum();
        _desired = total == 0
            ? queue.ToDictionary(kv => kv.Key, _ => 0.0)
            : queue.ToDictionary(kv => kv.Key, kv => (double)kv.Value / total);
    }

    public string? Peek(Dictionary<string, int> queue)
    {
        var available = queue.Where(kv => kv.Value > 0).ToList();
        if (available.Count == 0) return null;

        string best = available[0].Key;
        double bestDistance = double.PositiveInfinity;
        foreach (var kv in available)
        {
            double distance = DistanceAfterPlacing(kv.Key);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = kv.Key;
            }
        }

        return best;
    }

    public void Commit(string title)
    {
        _placed[title] = _placed.GetValueOrDefault(title) + 1;
        _placedTotal++;
    }

    private double DistanceAfterPlacing(string candidate)
    {
        int total = _placedTotal + 1;
        double distance = 0;
        foreach (var (key, desired) in _desired)
        {
            int placed = _placed.GetValueOrDefault(key);
            if (key == candidate) placed++;
            double proportion = (double)placed / total;
            double diff = proportion - desired;
            distance += diff * diff;
        }
        return distance;
    }
}
