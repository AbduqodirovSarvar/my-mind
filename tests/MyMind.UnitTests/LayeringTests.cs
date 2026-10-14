using System.Reflection;

namespace MyMind.UnitTests;

/// <summary>
/// Guards the dependency direction: Api → Infrastructure → Application → Domain.
/// The compiler drops unused references, so each layer is checked against the set it is allowed to use.
/// </summary>
public class LayeringTests
{
    [Fact]
    public void Domain_DoesNotReferenceOtherLayers() =>
        AssertReferencesOnly(typeof(Domain.AssemblyMarker).Assembly);

    [Fact]
    public void Application_ReferencesOnlyDomain() =>
        AssertReferencesOnly(typeof(Application.DependencyInjection).Assembly, "MyMind.Domain");

    [Fact]
    public void Infrastructure_DoesNotReferenceApi() =>
        AssertReferencesOnly(typeof(Infrastructure.DependencyInjection).Assembly, "MyMind.Application", "MyMind.Domain");

    private static void AssertReferencesOnly(Assembly assembly, params string[] allowed)
    {
        var forbidden = assembly.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(name => name.StartsWith("MyMind.", StringComparison.Ordinal))
            .Except(allowed);

        Assert.Empty(forbidden);
    }
}
