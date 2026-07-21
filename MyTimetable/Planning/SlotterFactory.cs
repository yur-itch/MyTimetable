using MyTimetable.Models;

namespace MyTimetable.Planning;

public sealed class SlotterFactory : ISlotterFactory
{
    private readonly Func<List<Slot>, ISlotter> _create;

    public SlotterFactory(Func<List<Slot>, ISlotter> create)
        => _create = create;

    public ISlotter Create(List<Slot> fillable) => _create(fillable);
}
