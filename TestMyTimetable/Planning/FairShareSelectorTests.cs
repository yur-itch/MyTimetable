using MyTimetable.Models;
using MyTimetable.Planning;

namespace TestMyTimetable.Planning;

public class TestFairShareSelector
{
    private static List<Slot> Slots(int count = 10, DateOnly? date = null)
        => Enumerable.Range(1, count)
            .Select(n => new Slot { Date = date ?? new DateOnly(2024, 1, 1), Number = n })
            .ToList();

    [Fact]
    public void Plan_EmptyQueue_ReturnsNothing()
    {
        var sel = new FairShareSelector(new Dictionary<string, int>(), Slots(5));
        sel.Plan().Should().BeEmpty();
    }

    [Fact]
    public void Plan_EmptyFillable_ReturnsNothing()
    {
        var sel = new FairShareSelector(new Dictionary<string, int> { ["A"] = 3 }, []);
        sel.Plan().Should().BeEmpty();
    }

    [Fact]
    public void Plan_SingleSubject_FillsAll()
    {
        var queue = new Dictionary<string, int> { ["Math"] = 4 };
        var sel = new FairShareSelector(queue, Slots(4));
        sel.Plan().Select(p => p.Lesson.Title).Should().AllBe("Math");
    }

    [Fact]
    public void Plan_StopsWhenQueueExhausted()
    {
        var queue = new Dictionary<string, int> { ["A"] = 2 };
        var sel = new FairShareSelector(queue, Slots(10));
        sel.Plan().Should().HaveCount(2);
    }

    [Fact]
    public void Plan_StopsWhenSlotsExhausted()
    {
        var queue = new Dictionary<string, int> { ["A"] = 10 };
        var sel = new FairShareSelector(queue, Slots(3));
        sel.Plan().Should().HaveCount(3);
    }

    [Fact]
    public void Plan_EqualProportions_AlternatesFairly()
    {
        // A=50%, B=50% → должно чередоваться A,B,A,B...
        var queue = new Dictionary<string, int> { ["A"] = 3, ["B"] = 3 };
        var sel = new FairShareSelector(queue, Slots(6));
        var titles = sel.Plan().Select(p => p.Lesson.Title).ToList();

        // После каждого шага пропорции должны быть как можно ближе к 50/50
        // Логика: с пустым _placed первый шаг одинаков для обоих → берём A (первый в Available)
        // После A: A=1/1=100%, B=0% → нужен B
        // После B: A=1/2=50%, B=1/2=50% → оба одинаково близки → первый A
        // После A: A=2/3=67%, B=1/3=33% → нужен B
        // После B: A=2/4=50%, B=2/4=50% → A
        // После A: A=3/5=60%, B=2/5=40% → B
        // После B: A=3/6=50%, B=3/6=50% → конец
        titles.Should().Equal(["A", "B", "A", "B", "A", "B"]);
    }

    [Fact]
    public void Plan_UnequalProportions_DistributesFairly()
    {
        // A=75%, B=25% (A=3, B=1)
        // Первый шаг: A(3/4=75%) или B(1/4=25%) → оба на старте 0/0, выбираем первый A
        // placed: A=1/1=100%, желание A=75% → отклонение (100-75)²=625, B(0-25)²=625
        // B: A=1/2=50%, B=1/2=50% → A=50-75=-25²=625, B=50-25=25²=625 — одинаково → первый A
        // A: A=2/3=67%, B=1/3=33% → A=(67-75)²=64, B=(33-25)²=64 → одинаково → A
        // A: A=3/4=75%, B=1/4=25% → всё, оба ровно
        var queue = new Dictionary<string, int> { ["A"] = 3, ["B"] = 1 };
        var sel = new FairShareSelector(queue, Slots(4));
        var titles = sel.Plan().Select(p => p.Lesson.Title).ToList();

        titles.Should().Equal(["A", "A", "A", "B"]);
    }

    [Fact]
    public void Plan_FourToOneRatio()
    {
        // A=4, B=1 → A=80%, B=20%
        // Первый: A (первый в словаре)
        // A=1/1=100% vs 80% → откл 400, B=0% vs 20% → откл 400 → одинаково → A
        // A=2/2=100% vs 80% → откл 400, B=0% vs 20% → откл 400 → A
        // A=3/3=100% vs 80% → откл 400, B=0% vs 20% → откл 400 → A
        // A=4/4=100% → откл 400, но B=0% vs 20% → откл 400 → всё ещё A... 
        // На 5-м шаге: A закончился (4/4=100%), остался B=1
        var queue = new Dictionary<string, int> { ["A"] = 4, ["B"] = 1 };
        var sel = new FairShareSelector(queue, Slots(5));
        var titles = sel.Plan().Select(p => p.Lesson.Title).ToList();

        // Пересчёт: с равными расстояниями FairShare берёт первого в Available.
        // Available = [A(4), B(1)] — на каждом шаге сравниваем A vs B по расстоянию.
        // Шаг 1: желание A=0.8, B=0.2. placed: A=0, B=0, total=0.
        //   Если A: A=1/1=1.0, diff_A=0.2, diff_B=-0.2, dist²=0.04+0.04=0.08
        //   Если B: B=1/1=1.0, diff_A=-0.8, diff_B=0.8, dist²=0.64+0.64=1.28
        //   → A (0.08 < 1.28)
        // Шаг 2: placed A=1,B=0,total=1 (всего размещено 1, считаем _placedTotal + 1 = 2, но _placed уже +1)
        //   placed: A=1. Если A: A=2/2=1.0, diff_A=0.2, B=0/2=0, diff_B=-0.2, dist²=0.08
        //   Если B: A=1/2=0.5, B=1/2=0.5, diff_A=-0.3, diff_B=0.3, dist²=0.18
        //   → A (0.08 < 0.18)
        // Шаг 3: placed A=2. Если A: A=3/3=1.0, dist²=0.08
        //   Если B: A=2/3=0.667, B=1/3=0.333, diff_A=-0.133, diff_B=0.133, dist²=0.0356
        //   → B! (0.0356 < 0.08)
        // Шаг 4: placed A=2,B=1. Если A: A=3/4=0.75, B=1/4=0.25, diff_A=-0.05, diff_B=0.05, dist²=0.005
        //   Если B: A=2/4=0.5, B=2/4=0.5, diff_A=-0.3, diff_B=0.3, dist²=0.18
        //   → A (0.005 < 0.18)
        // Шаг 5: placed A=3,B=1. queue осталось A=1. Только A в Available → A.
        titles.Should().Equal(["A", "A", "B", "A", "A"]);
    }

    [Fact]
    public void Plan_ThreeEqualSubjects()
    {
        var queue = new Dictionary<string, int> { ["X"] = 2, ["Y"] = 2, ["Z"] = 2 };
        var sel = new FairShareSelector(queue, Slots(6));
        var titles = sel.Plan().Select(p => p.Lesson.Title).ToList();

        // С равными пропорциями каждый шаг даёт одинаковую дистанцию → берём первого в Available
        // Available всегда [X,Y,Z]
        // Шаг 1: все 0/0, первый X
        // Шаг 2: X=1/1=100%, Y=0,Z=0. Если X: X=2/2=100%, dist²: X(1-0.333)²=0.445, Y(0-0.333)²=0.111, Z(0-0.333)²=0.111, sum=0.667
        //   Если Y: X=1/2=50%, Y=1/2=50%, Z=0/2=0 → dist²: (0.5-0.333)²=0.0278 × 2 + (0-0.333)²=0.111 = 0.167
        //   Если Z: X=1/2=50%, Z=1/2=50%, Y=0/2=0 → dist²: 0.0278+0.111+0.0278=0.167
        //   → Y и Z одинаковы, первый Y
        // ...
        // Фактически, из-за симметрии будет получаться XYZXYZ с равными
        titles.Should().HaveCount(6);
        titles.Should().OnlyContain(t => t == "X" || t == "Y" || t == "Z");
        // Каждый предмет должен появиться ровно 2 раза
        titles.Count(t => t == "X").Should().Be(2);
        titles.Count(t => t == "Y").Should().Be(2);
        titles.Count(t => t == "Z").Should().Be(2);
    }

    [Fact]
    public void Plan_OutputHasCorrectProperties()
    {
        var queue = new Dictionary<string, int> { ["A"] = 1 };
        var slots = new List<Slot>
        {
            new() { Date = new DateOnly(2024, 5, 10), Number = 3 }
        };
        var result = new FairShareSelector(queue, slots).Plan().ToList();

        result.Should().HaveCount(1);
        result[0].Slot.Date.Should().Be(new DateOnly(2024, 5, 10));
        result[0].Slot.Number.Should().Be(3);
        result[0].Lesson.Title.Should().Be("A");
        result[0].Lesson.LessonType.Should().Be("PRACTICE");
    }
}
