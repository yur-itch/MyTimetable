namespace MyTimetable.Planning;

public sealed class PickerFactory : IPickerFactory
{
    private readonly Func<Dictionary<string, int>, IPicker> _create;

    public PickerFactory(Func<Dictionary<string, int>, IPicker> create)
        => _create = create;

    public IPicker Create(Dictionary<string, int> queue) => _create(queue);
}
