using MyTimetable.Models;

namespace MyTimetable.Planning;

public sealed class SequentialSlotter : ISlotter
{
    private readonly List<Slot> _fillable;

    public SequentialSlotter(List<Slot> fillable)
        => _fillable = fillable;

    public Slot? Peek()
    {
        if (_fillable.Count == 0) return null;
        return _fillable[0];
    }

    public void Commit(Slot slot)
    {
        _fillable.RemoveAt(0);
    }
}
