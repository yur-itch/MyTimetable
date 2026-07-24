using MyTimetable.Planning;

namespace TestMyTimetable.Planning;

public class TestPlanningSelectorFactory
{
    [Fact]
    public void Create_ReturnsCorrectSelectorType()
    {
        var factory = new PlanningSelectorFactory(
            (q, f) => new LargestQueueFirstSelector(q, f));
        var sel = factory.Create(
            new Dictionary<string, int> { ["A"] = 1 },
            [new Slot { Date = new DateOnly(2024, 1, 1), Number = 1 }]);
        sel.Should().BeOfType<LargestQueueFirstSelector>();
    }

    [Fact]
    public void Create_PassesQueueAndFillable()
    {
        var queue = new Dictionary<string, int> { ["X"] = 3 };
        var fillable = new List<Slot>
        {
            new() { Date = new DateOnly(2024, 1, 1), Number = 5 }
        };

        List<Slot>? capturedFillable = null;

        var factory = new PlanningSelectorFactory((q, f) =>
        {
            capturedFillable = f;
            return new LargestQueueFirstSelector(q, f);
        });

        factory.Create(queue, fillable);
        capturedFillable.Should().BeSameAs(fillable);
    }

    [Fact]
    public void Create_WithRoundRobinSelector()
    {
        var factory = new PlanningSelectorFactory(
            (q, f) => new RoundRobinSelector(q, f));
        var sel = factory.Create(
            new Dictionary<string, int> { ["A"] = 1 },
            [new Slot { Date = new DateOnly(2024, 1, 1), Number = 1 }]);
        sel.Should().BeOfType<RoundRobinSelector>();
    }

    [Fact]
    public void Create_WithRandomSelector_UsesDefaultRandom()
    {
        var factory = new PlanningSelectorFactory(
            (q, f) => new RandomSelector(q, f));
        var sel = factory.Create(
            new Dictionary<string, int> { ["A"] = 1 },
            [new Slot { Date = new DateOnly(2024, 1, 1), Number = 1 }]);
        sel.Should().BeOfType<RandomSelector>();
    }

    [Fact]
    public void Create_WithGapClosingSelector()
    {
        var factory = new PlanningSelectorFactory(
            (q, f) => new GapClosingSelector(q, f));
        var sel = factory.Create(
            new Dictionary<string, int> { ["A"] = 1 },
            [new Slot { Date = new DateOnly(2024, 1, 1), Number = 1 }]);
        sel.Should().BeOfType<GapClosingSelector>();
    }

    [Fact]
    public void Create_WithEmptyDaySeedSelector()
    {
        var factory = new PlanningSelectorFactory(
            (q, f) => new EmptyDaySeedSelector(q, f));
        var sel = factory.Create(
            new Dictionary<string, int> { ["A"] = 1 },
            [new Slot { Date = new DateOnly(2024, 1, 1), Number = 1 }]);
        sel.Should().BeOfType<EmptyDaySeedSelector>();
    }

    [Fact]
    public void Create_WithLeadingChunkGrowthSelector()
    {
        var factory = new PlanningSelectorFactory(
            (q, f) => new LeadingChunkGrowthSelector(q, f));
        var sel = factory.Create(
            new Dictionary<string, int> { ["A"] = 1 },
            [new Slot { Date = new DateOnly(2024, 1, 1), Number = 1 }]);
        sel.Should().BeOfType<LeadingChunkGrowthSelector>();
    }

    [Fact]
    public void Create_WithTrailingChunkGrowthSelector()
    {
        var factory = new PlanningSelectorFactory(
            (q, f) => new TrailingChunkGrowthSelector(q, f));
        var sel = factory.Create(
            new Dictionary<string, int> { ["A"] = 1 },
            [new Slot { Date = new DateOnly(2024, 1, 1), Number = 1 }]);
        sel.Should().BeOfType<TrailingChunkGrowthSelector>();
    }

    [Fact]
    public void Create_WithFairShareSelector()
    {
        var factory = new PlanningSelectorFactory(
            (q, f) => new FairShareSelector(q, f));
        var sel = factory.Create(
            new Dictionary<string, int> { ["A"] = 1 },
            [new Slot { Date = new DateOnly(2024, 1, 1), Number = 1 }]);
        sel.Should().BeOfType<FairShareSelector>();
    }

    [Fact]
    public void Create_WithSmallestQueueFirstSelector()
    {
        var factory = new PlanningSelectorFactory(
            (q, f) => new SmallestQueueFirstSelector(q, f));
        var sel = factory.Create(
            new Dictionary<string, int> { ["A"] = 1 },
            [new Slot { Date = new DateOnly(2024, 1, 1), Number = 1 }]);
        sel.Should().BeOfType<SmallestQueueFirstSelector>();
    }

    [Fact]
    public void Create_WithWeightedRandomSelector()
    {
        var factory = new PlanningSelectorFactory(
            (q, f) => new WeightedRandomSelector(q, f));
        var sel = factory.Create(
            new Dictionary<string, int> { ["A"] = 1 },
            [new Slot { Date = new DateOnly(2024, 1, 1), Number = 1 }]);
        sel.Should().BeOfType<WeightedRandomSelector>();
    }

    [Fact]
    public void Create_CapturesBothArguments()
    {
        Dictionary<string, int>? capturedQueue = null;
        List<Slot>? capturedFillable = null;

        var factory = new PlanningSelectorFactory((q, f) =>
        {
            capturedQueue = q;
            capturedFillable = f;
            return new LargestQueueFirstSelector(q, f);
        });

        var queue = new Dictionary<string, int> { ["Test"] = 5 };
        var fillable = new List<Slot>
        {
            new() { Date = new DateOnly(2024, 1, 1), Number = 1 }
        };

        factory.Create(queue, fillable);

        capturedQueue.Should().BeSameAs(queue);
        capturedFillable.Should().BeSameAs(fillable);
    }
}
