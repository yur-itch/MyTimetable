using MyTimetable.Planning;

namespace TestMyTimetable.Planning;

public class TestRoundRobinPicker
{
    [Fact]
    public void Peek_FromEmptyQueue_ReturnsNull()
    {
        var picker = new RoundRobinPicker(new Dictionary<string, int>());
        picker.Peek(new Dictionary<string, int>()).Should().BeNull();
    }

    [Fact]
    public void Peek_SingleItem_ReturnsItEveryTime()
    {
        var q = new Dictionary<string, int> { ["A"] = 5 };
        var picker = new RoundRobinPicker(q);
        for (int i = 0; i < 5; i++)
        {
            var t = picker.Peek(q); t.Should().Be("A"); picker.Commit(t!);
            q["A"]--;
        }
    }

    [Fact]
    public void Peek_CyclesThroughItems()
    {
        var q = new Dictionary<string, int> { ["A"] = 3, ["B"] = 3 };
        var picker = new RoundRobinPicker(q);

        var t = picker.Peek(q); t.Should().Be("A"); picker.Commit(t!); q["A"]--;
        t = picker.Peek(q); t.Should().Be("B"); picker.Commit(t!); q["B"]--;
        t = picker.Peek(q); t.Should().Be("A"); picker.Commit(t!); q["A"]--;
        t = picker.Peek(q); t.Should().Be("B"); picker.Commit(t!); q["B"]--;
        t = picker.Peek(q); t.Should().Be("A"); picker.Commit(t!); q["A"]--;
        t = picker.Peek(q); t.Should().Be("B"); picker.Commit(t!); q["B"]--;
    }

    [Fact]
    public void Peek_CyclesWithThreeItems()
    {
        var q = new Dictionary<string, int> { ["X"] = 2, ["Y"] = 2, ["Z"] = 2 };
        var picker = new RoundRobinPicker(q);

        var t = picker.Peek(q); t.Should().Be("X"); picker.Commit(t!); q["X"]--;
        t = picker.Peek(q); t.Should().Be("Y"); picker.Commit(t!); q["Y"]--;
        t = picker.Peek(q); t.Should().Be("Z"); picker.Commit(t!); q["Z"]--;
        t = picker.Peek(q); t.Should().Be("X"); picker.Commit(t!); q["X"]--;
        t = picker.Peek(q); t.Should().Be("Y"); picker.Commit(t!); q["Y"]--;
        t = picker.Peek(q); t.Should().Be("Z"); picker.Commit(t!); q["Z"]--;
    }

    [Fact]
    public void Peek_WhenItemExhausted_SkipsIt()
    {
        var q = new Dictionary<string, int> { ["A"] = 0, ["B"] = 3 };
        var picker = new RoundRobinPicker(q);

        var t = picker.Peek(q); t.Should().Be("B"); picker.Commit(t!); q["B"]--;
        t = picker.Peek(q); t.Should().Be("B"); picker.Commit(t!); q["B"]--;
        t = picker.Peek(q); t.Should().Be("B"); picker.Commit(t!); q["B"]--;
        picker.Peek(q).Should().BeNull();
    }

    [Fact]
    public void Peek_WhenAllExhausted_ReturnsNull()
    {
        var q = new Dictionary<string, int> { ["A"] = 0, ["B"] = 0 };
        var picker = new RoundRobinPicker(q);
        picker.Peek(q).Should().BeNull();
    }

    [Fact]
    public void Peek_PositionPreservedAcrossCalls()
    {
        var q = new Dictionary<string, int> { ["A"] = 10, ["B"] = 10 };
        var picker = new RoundRobinPicker(q);

        var t = picker.Peek(q); picker.Commit(t!); q[t!]--;
        t = picker.Peek(q); picker.Commit(t!); q[t!]--;
        t = picker.Peek(q); picker.Commit(t!); q[t!]--;
        t = picker.Peek(q); picker.Commit(t!); q[t!]--;
        t = picker.Peek(q); picker.Commit(t!); q[t!]--;
        t = picker.Peek(q); t.Should().Be("B");
    }

    [Fact]
    public void Peek_RespectsKeyOrderFromDictionary()
    {
        var q = new Dictionary<string, int> { ["First"] = 2, ["Second"] = 2, ["Third"] = 2 };
        var picker = new RoundRobinPicker(q);

        var t = picker.Peek(q); t.Should().Be("First"); picker.Commit(t!); q["First"]--;
        t = picker.Peek(q); t.Should().Be("Second"); picker.Commit(t!); q["Second"]--;
        t = picker.Peek(q); t.Should().Be("Third"); picker.Commit(t!); q["Third"]--;
        t = picker.Peek(q); t.Should().Be("First"); picker.Commit(t!); q["First"]--;
    }

    [Fact]
    public void Peek_SingleItemExhausted_ReturnsNull()
    {
        var q = new Dictionary<string, int> { ["A"] = 0 };
        var picker = new RoundRobinPicker(q);
        picker.Peek(q).Should().BeNull();
    }
}
