namespace B2.Performance.Tests;

/// <summary>
/// F-13 / T-S5-06 — Sổ B2 export performance budget.
/// </summary>
public sealed class ExportScenarios
{
    // SC-012: a 30-day Sổ B2 export with ~3,500 declarations must complete in < 10 s
    // on the spec VM. The NBomber load harness for this performance project is not yet
    // bootstrapped (the project has no scenarios), so this is a spec-VM placeholder
    // matching the F-07 search-perf convention. Un-skip and implement the NBomber
    // scenario (seed ~3,500 declarations → drive GET /api/reports/excel → assert p95
    // < 10 s) when the performance harness lands. See spec.md SC-012 + tasks.md T-S5-06.
    [Fact(Skip = "30-day export perf budget (SC-012, < 10 s, ~3,500 declarations): execute on spec VM via NBomber once the perf harness is bootstrapped.")]
    [Trait("Feature", "F-13")]
    public void Should_export_30_days_within_budget_on_spec_vm() => Assert.True(true);
}
