namespace B2.Performance.Tests;

/// <summary>
/// F-17 / T-S6-04 — Smart Suggestions latency budget.
/// </summary>
public sealed class SuggestScenarios
{
    // SC from research.md § 7: GET /api/vehicles/suggest must return < 100 ms p95
    // against a DB primed with 90 days of pairing data.
    // Un-skip and implement the NBomber scenario (seed 90-day pairings →
    // drive GET /api/vehicles/suggest?plate=…&field=trailer → assert p95 < 100 ms)
    // when the performance harness is bootstrapped. See spec.md F-17 + tasks.md T-S6-04.
    [Fact(Skip = "Suggest latency budget (< 100 ms p95, 90-day primed DB): execute on spec VM via NBomber once the perf harness is bootstrapped.")]
    [Trait("Feature", "F-17")]
    public void Should_suggest_within_latency_budget_on_spec_vm() => Assert.True(true);
}
