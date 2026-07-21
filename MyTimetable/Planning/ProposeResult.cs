namespace MyTimetable.Planning;

/// <summary>
/// Planner's response to a proposed (slot, subject) pair.
/// The selector proposes; the planner evaluates domain rules and replies with two independent bits.
/// The selector reacts mechanically — it does not need to know <em>why</em> something was rejected.
/// </summary>
public readonly record struct ProposeResult(bool SlotOK, bool SubjectOK);
