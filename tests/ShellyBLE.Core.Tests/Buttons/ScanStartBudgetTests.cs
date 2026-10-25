namespace ShellyBLE.Core.Tests.Buttons;

public class ScanStartBudgetTests
{
    readonly Mock<TimeProvider> time = new();
    readonly ScanStartBudget budget;
    DateTimeOffset now = DateTimeOffset.FromUnixTimeSeconds(1_761_300_000);

    public ScanStartBudgetTests()
    {
        time.Setup(t => t.GetUtcNow()).Returns(() => now);
        budget = new(time.Object);
    }

    [Fact]
    public void Starts_within_the_quota_need_no_wait()
    {
        for (var i = 0; i < ScanStartBudget.MaxStarts - 1; i++)
            budget.Record();

        budget.WaitBeforeNextStart().ShouldBe(TimeSpan.Zero);
    }

    /// <summary>Cas réel : cinq boutons aperçus coup sur coup relanceraient le scan cinq fois.</summary>
    [Fact]
    public void A_full_quota_waits_until_the_oldest_start_leaves_the_window()
    {
        for (var i = 0; i < ScanStartBudget.MaxStarts; i++)
        {
            budget.Record();
            now += TimeSpan.FromSeconds(2);
        }

        budget.WaitBeforeNextStart().ShouldBe(ScanStartBudget.Window - TimeSpan.FromSeconds(2 * ScanStartBudget.MaxStarts));
    }

    [Fact]
    public void Old_starts_stop_counting_once_out_of_the_window()
    {
        for (var i = 0; i < ScanStartBudget.MaxStarts; i++)
            budget.Record();

        now += ScanStartBudget.Window;

        budget.WaitBeforeNextStart().ShouldBe(TimeSpan.Zero);
    }
}
