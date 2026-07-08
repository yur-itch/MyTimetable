namespace MyTimetable.Planning
{
    // Выбор предмета по кругу: ходит по фиксированному порядку, пропуская исчерпанные. Слот-стратегии
    // (которые сами решают, КУДА ставить) композируют его, чтобы не дублировать логику выбора ПРЕДМЕТА.
    // Держит ссылку на живую очередь селектора — ту же, что декрементит база после каждого размещения.
    internal sealed class RoundRobinPicker
    {
        private readonly Dictionary<string, int> _queue;
        private readonly List<string> _order;
        private int _position;

        public RoundRobinPicker(Dictionary<string, int> queue)
        {
            _queue = queue;
            _order = queue.Keys.ToList();
        }

        // null — ни одного предмета с остатком (очередь исчерпана).
        public string? Pick()
        {
            for (int i = 0; i < _order.Count; i++)
            {
                int index = (_position + i) % _order.Count;
                string key = _order[index];
                if (_queue.TryGetValue(key, out int remaining) && remaining > 0)
                {
                    _position = (index + 1) % _order.Count;
                    return key;
                }
            }
            return null;
        }
    }
}
