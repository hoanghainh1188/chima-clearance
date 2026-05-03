using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using ArchUnitArch = ArchUnitNET.Domain.Architecture;

namespace B2.Server.Api.Tests.Architecture;

/// <summary>
/// Constitution V — Layered Dependency Rules (NON-NEGOTIABLE).
/// Source: <c>.specify/memory/constitution.md</c> v1.1.0,
/// <c>specs/04-implementation-plan.md § 3.4</c>.
///
/// At Sprint S0 the projects are empty placeholders, so these tests verify the rules
/// against the loaded assembly graph rather than against business types. As real
/// types land in S1+, the rules continue to apply automatically.
/// </summary>
public sealed class LayerRulesTests
{
    private static readonly ArchUnitArch Architecture = new ArchLoader()
        .LoadAssemblies(
            typeof(B2.Domain.AssemblyMarker).Assembly,
            typeof(B2.Application.AssemblyMarker).Assembly,
            typeof(B2.Infrastructure.AssemblyMarker).Assembly,
            typeof(B2.Server.Api.AssemblyMarker).Assembly,
            typeof(B2.Shared.Contracts.AssemblyMarker).Assembly)
        .Build();

    [Fact]
    [Trait("Category", "Architecture")]
    public void Domain_must_not_depend_on_EFCore()
    {
        Types().That().ResideInAssembly("B2.Domain")
            .Should().NotDependOnAny(Types().That().HaveFullNameContaining("Microsoft.EntityFrameworkCore"))
            .Because("Constitution V — EF Core types must not leak into Domain")
            .WithoutRequiringPositiveResults()
            .Check(Architecture);
    }

    [Fact]
    [Trait("Category", "Architecture")]
    public void Domain_must_not_depend_on_AspNetCore()
    {
        Types().That().ResideInAssembly("B2.Domain")
            .Should().NotDependOnAny(Types().That().HaveFullNameContaining("Microsoft.AspNetCore"))
            .Because("Constitution V — HttpContext / AspNetCore must not leak into Domain")
            .WithoutRequiringPositiveResults()
            .Check(Architecture);
    }

    [Fact]
    [Trait("Category", "Architecture")]
    public void Domain_must_not_depend_on_MicrosoftExtensions()
    {
        Types().That().ResideInAssembly("B2.Domain")
            .Should().NotDependOnAny(Types().That().HaveFullNameContaining("Microsoft.Extensions"))
            .Because("Constitution V — Domain has zero outward references, not even Microsoft.Extensions.*")
            .WithoutRequiringPositiveResults()
            .Check(Architecture);
    }

    [Fact]
    [Trait("Category", "Architecture")]
    public void Application_must_not_depend_on_EFCore()
    {
        Types().That().ResideInAssembly("B2.Application")
            .Should().NotDependOnAny(Types().That().HaveFullNameContaining("Microsoft.EntityFrameworkCore"))
            .Because("Constitution V — EF Core types must not leak into Application; Infrastructure owns EF.")
            .WithoutRequiringPositiveResults()
            .Check(Architecture);
    }

    [Fact]
    [Trait("Category", "Architecture")]
    public void Application_must_not_depend_on_HttpContext()
    {
        Types().That().ResideInAssembly("B2.Application")
            .Should().NotDependOnAny(Types().That().HaveFullNameContaining("Microsoft.AspNetCore.Http.HttpContext"))
            .Because("Constitution V — HttpContext must not leak into Application; Server.Api owns the web boundary.")
            .WithoutRequiringPositiveResults()
            .Check(Architecture);
    }

    // Note: rules 2 and 3 (no project references B2.Desktop; Desktop ↔ Server.Api only via Shared.Contracts)
    // are enforced statically by the project-reference graph in `B2.sln`. Editing a .csproj is itself
    // a code-review signal. The ArchUnit assertions above guard the runtime/compile-time leaks the
    // project graph cannot prevent.
}
