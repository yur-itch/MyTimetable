using System.Text.Json.Serialization;

namespace MyTimetable.Planning;

[JsonDerivedType(typeof(Prebuilt), "prebuilt")]
[JsonDerivedType(typeof(Composed), "composed")]
public abstract record class SelectorSpec
{
    public sealed record class Prebuilt(string Name) : SelectorSpec;
    public sealed record class Composed(string Picker, string Slotter) : SelectorSpec;
}
