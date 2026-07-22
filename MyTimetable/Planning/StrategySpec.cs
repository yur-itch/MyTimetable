using System.Text.Json.Serialization;

namespace MyTimetable.Planning;

[JsonDerivedType(typeof(Prebuilt), "prebuilt")]
[JsonDerivedType(typeof(Composed), "composed")]
public abstract record class StrategySpec
{
    public sealed record class Prebuilt(string Name) : StrategySpec;
    public sealed record class Composed(string Picker, string Slotter) : StrategySpec;
}
