using System.Reflection;
using System.Text.Json;
using EMS.Application.Common.Options;
using Shouldly;

namespace EMS.UnitTests.Architecture;

/// <summary>
/// Enforces the dependency rule from architecture.md section 1.1 and ADR-0003: EMS.Application may
/// reference EF Core abstractions, and never a database provider.
/// </summary>
/// <remarks>
/// The document claims this boundary "is testable: an architecture test asserts that EMS.Application
/// has no transitive reference to Microsoft.EntityFrameworkCore.SqlServer or Microsoft.Data.SqlClient".
/// This is that test. Without it the claim was aspiration, and the first careless
/// <c>dotnet add package</c> would have broken the boundary silently.
/// </remarks>
public class LayerBoundaryTests
{
    /// <summary>Package names that must never reach the application layer.</summary>
    private static readonly string[] ForbiddenPackages =
    [
        "Microsoft.EntityFrameworkCore.SqlServer",
        "Microsoft.Data.SqlClient",
    ];

    [Fact]
    public void ApplicationAssembly_ReferencesNoProviderAssembly()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<Assembly>();
        queue.Enqueue(typeof(AppSettings).Assembly);

        while (queue.Count > 0)
        {
            foreach (var reference in queue.Dequeue().GetReferencedAssemblies())
            {
                var name = reference.Name ?? string.Empty;

                ForbiddenPackages.ShouldNotContain(
                    name,
                    $"EMS.Application must not reference {name}. See ADR-0003.");

                if (!name.StartsWith("EMS.", StringComparison.Ordinal) || !seen.Add(name))
                {
                    continue;
                }

                queue.Enqueue(Assembly.Load(reference));
            }
        }
    }

    [Fact]
    public void ApplicationLockFile_ResolvesNoProviderPackage()
    {
        // The assembly check above only sees references the compiler kept. The lock file is the
        // authority on what restore actually pulls in, transitive packages included.
        var lockFile = Path.Combine(RepositoryRoot(), "src", "EMS.Application", "packages.lock.json");

        File.Exists(lockFile).ShouldBeTrue($"Expected a lock file at {lockFile}.");

        using var document = JsonDocument.Parse(File.ReadAllText(lockFile));

        var resolved = document.RootElement
            .GetProperty("dependencies")
            .EnumerateObject()
            .SelectMany(framework => framework.Value.EnumerateObject())
            .Select(package => package.Name)
            .ToList();

        foreach (var forbidden in ForbiddenPackages)
        {
            resolved.ShouldNotContain(
                forbidden,
                StringComparer.OrdinalIgnoreCase,
                $"EMS.Application resolves {forbidden}. The provider belongs to Infrastructure alone.");
        }
    }

    [Fact]
    public void DomainAssembly_ReferencesNothingBeyondTheBaseClassLibrary()
    {
        // Phase 1's rule: EMS.Domain carries no dependency of its own. The assertion is on the
        // compiled assembly rather than the lock file, because analyzer packages are legitimate
        // -- they are build-time only, ship no runtime asset, and appear in every lock file.
        var references = typeof(Domain.Entities.Employee).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => !name.StartsWith("System", StringComparison.Ordinal)
                           && !name.Equals("netstandard", StringComparison.Ordinal)
                           && !name.Equals("mscorlib", StringComparison.Ordinal))
            .ToList();

        references.ShouldBeEmpty(
            $"EMS.Domain must depend on nothing but the base class library, but references {string.Join(", ", references)}.");
    }

    /// <summary>Walks up from the test binaries until the solution file appears.</summary>
    /// <returns>The repository root.</returns>
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EMS.sln")))
        {
            directory = directory.Parent;
        }

        directory.ShouldNotBeNull("Could not locate EMS.sln above the test output directory.");

        return directory.FullName;
    }
}
