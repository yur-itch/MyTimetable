using MyTimetable.Models;

namespace MyTimetable.Planning;

public sealed class SequentialSlotter : ISlotter
{
    private readonly List<Slot> _fillable;

    public SequentialSlotter(List<Slot> fillable)
        => _fillable = fillable;

    public Slot? NextSlot()
    {
        if (_fillable.Count == 0) return null;
        var slot = _fillable[0];
        _fillable.RemoveAt(0);
        return slot;
    }
}
