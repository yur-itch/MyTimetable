using MyTimetable.Planning;

namespace TestMyTimetable.Planning;

public class TestRoundRobinPicker
{
    [Fact]
    public void Pick_FromEmptyQueue_ReturnsNull()
    {
        var picker = new RoundRobinPicker(new Dictionary<string, int>());
        picker.Pick().Should().BeNull();
    }

    [Fact]
    public void Pick_SingleItem_ReturnsItEveryTime()
    {
        var picker = new RoundRobinPicker(new Dictionary<string, int> { ["A"] = 5 });
        for (int i = 0; i < 5; i++)
            picker.Pick().Should().Be("A");
    }

    [Fact]
    public void Pick_CyclesThroughItems()
    {
        var q = new Dictionary<string, int> { ["A"] = 3, ["B"] = 3 };
        var picker = new RoundRobinPicker(q);

        // Ожидаемый порядок: A, B, A, B, A, B
        picker.Pick().Should().Be("A");
        picker.Pick().Should().Be("B");
        picker.Pick().Should().Be("A");
        picker.Pick().Should().Be("B");
        picker.Pick().Should().Be("A");
        picker.Pick().Should().Be("B");
    }

    [Fact]
    public void Pick_CyclesWithThreeItems()
    {
        var q = new Dictionary<string, int> { ["X"] = 2, ["Y"] = 2, ["Z"] = 2 };
        var picker = new RoundRobinPicker(q);

        picker.Pick().Should().Be("X");
        picker.Pick().Should().Be("Y");
        picker.Pick().Should().Be("Z");
        picker.Pick().Should().Be("X");
        picker.Pick().Should().Be("Y");
        picker.Pick().Should().Be("Z");
    }

    [Fact]
    public void Pick_WhenItemExhausted_SkipsIt()
    {
        var q = new Dictionary<string, int> { ["A"] = 1, ["B"] = 3 };
        var picker = new RoundRobinPicker(q);

        picker.Pick().Should().Be("A"); // A=1 → A=0
        picker.Pick().Should().Be("B"); // B=3 → B=2
        // A exhausted — теперь только B
        picker.Pick().Should().Be("B");
        picker.Pick().Should().Be("B");
        picker.Pick().Should().BeNull(); // все исчерпаны
    }

    [Fact]
    public void Pick_WhenAllExhausted_ReturnsNull()
    {
        var q = new Dictionary<string, int> { ["A"] = 1, ["B"] = 1 };
        var picker = new RoundRobinPicker(q);

        picker.Pick().Should().Be("A");
        picker.Pick().Should().Be("B");
        picker.Pick().Should().BeNull();
    }

    [Fact]
    public void Pick_PositionPreservedAcrossCalls()
    {
        var q = new Dictionary<string, int> { ["A"] = 10, ["B"] = 10 };
        var picker = new RoundRobinPicker(q);

        // Потратим 3 цикла
        for (int i = 0; i < 3; i++)
        {
            picker.Pick(); // A
            picker.Pick(); // B
        }
        // Текущая позиция после 3 циклов: позиция на A (если 6 вызовов, то position=0 → A)
        picker.Pick().Should().Be("A");
    }

    [Fact]
    public void Pick_RespectsKeyOrderFromDictionary()
    {
        // Порядок должен соответствовать порядку ключей в словаре
        var q = new Dictionary<string, int> { ["First"] = 2, ["Second"] = 2, ["Third"] = 2 };
        var picker = new RoundRobinPicker(q);

        picker.Pick().Should().Be("First");
        picker.Pick().Should().Be("Second");
        picker.Pick().Should().Be("Third");
        picker.Pick().Should().Be("First");
    }

    [Fact]
    public void Pick_SingleItemExhausted_ReturnsNull()
    {
        var q = new Dictionary<string, int> { ["A"] = 1 };
        var picker = new RoundRobinPicker(q);

        picker.Pick().Should().Be("A");
        picker.Pick().Should().BeNull();
    }
}
