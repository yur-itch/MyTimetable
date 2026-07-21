namespace MyTimetable.Planning;

public sealed class RoundRobinPicker : IPicker
{
    private readonly List<string> _order;
    private int _position;

    public RoundRobinPicker(Dictionary<string, int> queue)
        => _order = queue.Keys.ToList();

    public string? Pick(Dictionary<string, int> queue)
    {
        for (int i = 0; i < _order.Count; i++)
        {
            int index = (_position + i) % _order.Count;
            string key = _order[index];
            if (queue.TryGetValue(key, out int remaining) && remaining > 0)
            {
                _position = (index + 1) % _order.Count;
                return key;
            }
        }
        return null;
    }
}
