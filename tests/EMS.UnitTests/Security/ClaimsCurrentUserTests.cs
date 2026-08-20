using System.Security.Claims;
using EMS.Application.Common.Interfaces;
using EMS.Infrastructure.Identity;
using EMS.Web.Security;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Shouldly;

namespace EMS.UnitTests.Security;

/// <summary>
/// Covers the acting user read from the principal. The actor label matters most: spec §3.8.1 wants
/// a background write attributed to the job that made it, not to a bare "System" shared by every
/// job, the seeder, and a startup migration.
/// </summary>
public class ClaimsCurrentUserTests
{
    private static readonly Guid EmployeeId = Guid.Parse("4b1f3a5e-77c2-4d8b-9c0a-0d2f5b6e7a81");
    private static readonly Guid FinanceId = Guid.Parse("9a2c4d6e-1b3f-4a5c-8d7e-2f4a6b8c0d1e");

    [Fact]
    public void ActorDescription_WithNoPrincipalAndNoJob_IsBareSystem()
    {
        Build(principal: null, jobName: null).ActorDescription.ShouldBe("System");
    }

    [Fact]
    public void ActorDescription_InsideAJobPass_NamesTheJob()
    {
        Build(principal: null, jobName: "MissedClockOut")
            .ActorDescription
            .ShouldBe("System: MissedClockOut");
    }

    [Fact]
    public void ActorDescription_WithASignedInUser_IsTheEmail()
    {
        BuildForRequest(Principal("marie@ems.local"))
            .ActorDescription
            .ShouldBe("marie@ems.local");
    }

    [Fact]
    public void Principal_IsNotSoughtInsideAJobPass()
    {
        // A background scope has no user, and asking the authentication state provider for one
        // there throws. The job label is the answer, not a fallback after a failed lookup.
        var user = Build(Principal("marie@ems.local", employeeId: EmployeeId), "MissedClockOut");

        user.ActorDescription.ShouldBe("System: MissedClockOut");
        user.EmployeeId.ShouldBeNull();
    }

    [Fact]
    public void EmployeeId_ReadsTheClaim()
    {
        Build(Principal("marie@ems.local", employeeId: EmployeeId), null)
            .EmployeeId
            .ShouldBe(EmployeeId);
    }

    [Fact]
    public void EmployeeId_WithNoClaim_IsNull()
    {
        Build(Principal("marie@ems.local"), null).EmployeeId.ShouldBeNull();
    }

    [Fact]
    public void EmployeeId_WithAMalformedClaim_IsNullRatherThanThrowing()
    {
        var principal = Principal("marie@ems.local");
        ((ClaimsIdentity)principal.Identity!).AddClaim(new Claim(EmployeeClaims.EmployeeId, "not-a-guid"));

        Build(principal, null).EmployeeId.ShouldBeNull();
    }

    [Fact]
    public void Roles_AreReadFromThePrincipal()
    {
        var user = Build(Principal("marie@ems.local", roles: ["Admin"]), null);

        user.IsAdmin.ShouldBeTrue();
        user.IsManager.ShouldBeFalse();
    }

    [Fact]
    public void ManagedDepartmentIds_CollectsEveryClaim()
    {
        var principal = Principal("marie@ems.local", roles: ["Manager"]);
        var identity = (ClaimsIdentity)principal.Identity!;
        identity.AddClaim(new Claim(EmployeeClaims.ManagedDepartment, FinanceId.ToString()));
        identity.AddClaim(new Claim(EmployeeClaims.ManagedDepartment, EmployeeId.ToString()));

        Build(principal, null).ManagedDepartmentIds.ShouldBe([FinanceId, EmployeeId], ignoreOrder: true);
    }

    [Fact]
    public void ManagedDepartmentIds_WithNoPrincipal_IsEmpty()
    {
        Build(principal: null, jobName: null).ManagedDepartmentIds.ShouldBeEmpty();
    }

    /// <remarks>
    /// The circuit path is the one under test, so the accessor reports no HttpContext — which is
    /// what an interactive render, a background job, and the seeder all see.
    /// </remarks>
    private static ClaimsCurrentUser Build(ClaimsPrincipal? principal, string? jobName) =>
        new(
            new HttpContextAccessor(),
            new StubAuthenticationStateProvider(principal ?? new ClaimsPrincipal(new ClaimsIdentity())),
            new SystemActorContext { JobName = jobName });

    /// <summary>The static SSR path: a real request, with a principal on its context.</summary>
    private static ClaimsCurrentUser BuildForRequest(ClaimsPrincipal principal)
    {
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = principal },
        };

        return new ClaimsCurrentUser(
            accessor,
            new StubAuthenticationStateProvider(new ClaimsPrincipal(new ClaimsIdentity())),
            new SystemActorContext());
    }

    private static ClaimsPrincipal Principal(
        string email,
        Guid? employeeId = null,
        string[]? roles = null)
    {
        var identity = new ClaimsIdentity(authenticationType: "Test");
        identity.AddClaim(new Claim(ClaimTypes.Email, email));

        if (employeeId is { } id)
        {
            identity.AddClaim(new Claim(EmployeeClaims.EmployeeId, id.ToString()));
        }

        foreach (var role in roles ?? [])
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        }

        return new ClaimsPrincipal(identity);
    }

    /// <summary>Returns a fixed state, the way the framework does once a circuit is open.</summary>
    private sealed class StubAuthenticationStateProvider(ClaimsPrincipal principal)
        : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(principal));
    }
}
