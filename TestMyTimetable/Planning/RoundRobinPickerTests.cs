using MyTimetable.Planning;

namespace TestMyTimetable.Planning;

public class TestRoundRobinPicker
{
    // RoundRobinPicker — stateless reader: queue values only change via caller.
    // Tests decrement the queue to simulate PlanningSelectorBase.Plan().

    [Fact]
    public void Pick_FromEmptyQueue_ReturnsNull()
    {
        var picker = new RoundRobinPicker(new Dictionary<string, int>());
        picker.Pick().Should().BeNull();
    }

    [Fact]
    public void Pick_SingleItem_ReturnsItEveryTime()
    {
        var q = new Dictionary<string, int> { ["A"] = 5 };
        var picker = new RoundRobinPicker(q);
        for (int i = 0; i < 5; i++)
        {
            picker.Pick().Should().Be("A");
            q["A"]--; // симулируем декремент из Plan()
        }
    }

    [Fact]
    public void Pick_CyclesThroughItems()
    {
        var q = new Dictionary<string, int> { ["A"] = 3, ["B"] = 3 };
        var picker = new RoundRobinPicker(q);

        picker.Pick().Should().Be("A"); q["A"]--;
        picker.Pick().Should().Be("B"); q["B"]--;
        picker.Pick().Should().Be("A"); q["A"]--;
        picker.Pick().Should().Be("B"); q["B"]--;
        picker.Pick().Should().Be("A"); q["A"]--;
        picker.Pick().Should().Be("B"); q["B"]--;
    }

    [Fact]
    public void Pick_CyclesWithThreeItems()
    {
        var q = new Dictionary<string, int> { ["X"] = 2, ["Y"] = 2, ["Z"] = 2 };
        var picker = new RoundRobinPicker(q);

        picker.Pick().Should().Be("X"); q["X"]--;
        picker.Pick().Should().Be("Y"); q["Y"]--;
        picker.Pick().Should().Be("Z"); q["Z"]--;
        picker.Pick().Should().Be("X"); q["X"]--;
        picker.Pick().Should().Be("Y"); q["Y"]--;
        picker.Pick().Should().Be("Z"); q["Z"]--;
    }

    [Fact]
    public void Pick_WhenItemExhausted_SkipsIt()
    {
        // Симулируем: A израсходован (q["A"]=0)
        var q = new Dictionary<string, int> { ["A"] = 0, ["B"] = 3 };
        var picker = new RoundRobinPicker(q);

        picker.Pick().Should().Be("B"); q["B"]--;
        picker.Pick().Should().Be("B"); q["B"]--;
        picker.Pick().Should().Be("B"); q["B"]--;
        picker.Pick().Should().BeNull();
    }

    [Fact]
    public void Pick_WhenAllExhausted_ReturnsNull()
    {
        var q = new Dictionary<string, int> { ["A"] = 0, ["B"] = 0 };
        var picker = new RoundRobinPicker(q);
        picker.Pick().Should().BeNull();
    }

    [Fact]
    public void Pick_PositionPreservedAcrossCalls()
    {
        var q = new Dictionary<string, int> { ["A"] = 10, ["B"] = 10 };
        var picker = new RoundRobinPicker(q);

        picker.Pick(); q["A"]--;                      // A
        picker.Pick(); q["B"]--;                      // B
        picker.Pick(); q["A"]--;                      // A
        picker.Pick(); q["B"]--;                      // B
        picker.Pick(); q["A"]--;                      // A
        picker.Pick().Should().Be("B");               // B — следующая в цикле
    }

    [Fact]
    public void Pick_RespectsKeyOrderFromDictionary()
    {
        var q = new Dictionary<string, int> { ["First"] = 2, ["Second"] = 2, ["Third"] = 2 };
        var picker = new RoundRobinPicker(q);

        picker.Pick().Should().Be("First");  q["First"]--;
        picker.Pick().Should().Be("Second"); q["Second"]--;
        picker.Pick().Should().Be("Third");  q["Third"]--;
        picker.Pick().Should().Be("First");  q["First"]--;
    }

    [Fact]
    public void Pick_SingleItemExhausted_ReturnsNull()
    {
        var q = new Dictionary<string, int> { ["A"] = 0 };
        var picker = new RoundRobinPicker(q);
        picker.Pick().Should().BeNull();
    }
}
