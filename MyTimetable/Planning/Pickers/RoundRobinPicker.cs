namespace MyTimetable.Planning;

public sealed class RoundRobinPicker : IPicker
{
    private readonly List<string> _order;
    private int _position;

    public RoundRobinPicker(Dictionary<string, int> queue)
        => _order = queue.Keys.ToList();

    public string? Peek(Dictionary<string, int> queue)
    {
        for (int i = 0; i < _order.Count; i++)
        {
            int index = (_position + i) % _order.Count;
            string key = _order[index];
            if (queue.TryGetValue(key, out int remaining) && remaining > 0)
                return key;
        }
        return null;
    }

    public void Commit(string title)
    {
        int index = _order.IndexOf(title);
        _position = (index + 1) % _order.Count;
    }
}
