using System.Globalization;
using System.Text;
using Bogus;
using EMS.Application.Common.Options;
using EMS.Domain.Entities;
using EMS.Domain.Enums;
using EMS.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EMS.Infrastructure.Data.Seed;

/// <summary>
/// Populates a development database with a deterministic dataset.
/// </summary>
/// <remarks>
/// Phase 2 seeds the account and organisation structure: roles, the default admin, departments,
/// employees, and one manager per department. Attendance history, leave requests with matching
/// balance rows, and the public holiday calendar are deliberately absent — they depend on the
/// holiday service and the leave rules, which arrive in Phase 4, and seeding them by hand here
/// would encode those rules twice.
/// </remarks>
public static class DatabaseSeeder
{
    /// <summary>The role names provisioned in Identity.</summary>
    private static readonly string[] RoleNames = ["Admin", "Manager", "Employee"];

    /// <summary>The departments from spec section 5.2.</summary>
    private static readonly string[] DepartmentNames =
        ["Finance", "Human Resources", "Operations", "IT", "Marketing"];

    /// <summary>
    /// The administrative districts of Mahé.
    /// </summary>
    /// <remarks>
    /// The national total is 26 districts; the three that are not on Mahé — Baie Sainte Anne and
    /// Grand'Anse Praslin on Praslin, and La Digue — are excluded, and the two Ile Perseverance
    /// districts reclaimed off the east coast are included.
    /// </remarks>
    private static readonly string[] MaheDistricts =
    [
        "Anse aux Pins",
        "Anse Boileau",
        "Anse Étoile",
        "Anse Royale",
        "Au Cap",
        "Baie Lazare",
        "Beau Vallon",
        "Bel Air",
        "Bel Ombre",
        "Cascade",
        "Glacis",
        "Grand'Anse Mahé",
        "Ile Perseverance I",
        "Ile Perseverance II",
        "La Rivière Anglaise",
        "Les Mamelles",
        "Mont Buxton",
        "Mont Fleuri",
        "Plaisance",
        "Pointe La Rue",
        "Port Glaud",
        "Roche Caiman",
        "Saint Louis",
        "Takamaka",
    ];

    /// <summary>Street and road names in use on Mahé.</summary>
    private static readonly string[] Streets =
    [
        "Albert Street",
        "Bel Eau Road",
        "Bois de Rose Avenue",
        "Chemin La Misère",
        "Chemin Sans Souci",
        "5th June Avenue",
        "Francis Rachel Street",
        "Huteau Lane",
        "Independence Avenue",
        "La Louise Road",
        "Latanier Road",
        "Liberation Avenue",
        "Manglier Street",
        "Olivier Maradan Street",
        "Palm Street",
        "Providence Highway",
        "Quincy Street",
        "Revolution Avenue",
        "Route Anse Royale",
        "State House Avenue",
    ];

    /// <summary>Seychellois Creole given names.</summary>
    private static readonly string[] FirstNames =
    [
        "Alain", "Bernadette", "Christelle", "Danny", "Elvina", "Emmanuel", "Fabien", "Gilbert",
        "Jean-Claude", "Josianne", "Kelly", "Lindy", "Marcel", "Marie-Ange", "Marie-Claire",
        "Mervyn", "Nadège", "Nathalie", "Patrick", "Raymonde", "Rita", "Roy", "Sabrina", "Steve",
        "Sylvie", "Terence", "Vanessa", "Wilna",
    ];

    /// <summary>Seychellois family names.</summary>
    private static readonly string[] LastNames =
    [
        "Adrienne", "Albert", "Bastienne", "Belle", "Bristol", "Camille", "Choppy", "Confait",
        "Dogley", "Esparon", "Fanchette", "Faure", "Figaro", "Gappy", "Gonthier", "Grandcourt",
        "Hoareau", "Labiche", "Laporte", "Lesperance", "Mancienne", "Morel", "Nourrice", "Payet",
        "Pool", "Quatre", "Radegonde", "René", "Rose", "Servina", "Sinon", "Souffe", "Talma",
        "Valentin", "Vidot", "Zialor",
    ];

    /// <summary>
    /// Seeds the database if it is empty, and does nothing otherwise.
    /// </summary>
    /// <param name="db">The context. Identity writes and domain writes share it.</param>
    /// <param name="users">The Identity user manager.</param>
    /// <param name="roles">The Identity role manager.</param>
    /// <param name="settings">Seeding settings, including the randomizer seed.</param>
    /// <param name="password">
    /// The temporary password given to every seeded account. Supplied by the caller from
    /// configuration, never from a literal in source.
    /// </param>
    /// <param name="logger">Receives a line per seeded stage.</param>
    /// <param name="ct">Cancels the seed.</param>
    /// <returns>A task that completes when seeding finishes.</returns>
    public static async Task SeedAsync(
        ApplicationDbContext db,
        UserManager<ApplicationUser> users,
        RoleManager<IdentityRole> roles,
        SeedDataSettings settings,
        string password,
        ILogger logger,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(users);
        ArgumentNullException.ThrowIfNull(roles);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(logger);

        // Ignore the soft-delete filter: an inactive employee still means "already seeded".
        if (await db.Employees.IgnoreQueryFilters().AnyAsync(ct).ConfigureAwait(false))
        {
            logger.LogInformation("Seed skipped: the database already holds employee data.");
            return;
        }

        await SeedRolesAsync(roles, ct).ConfigureAwait(false);

        var departments = await SeedDepartmentsAsync(db, ct).ConfigureAwait(false);

        // Fixed seed: integration tests assert against this dataset, so it must be identical on
        // every run and every machine.
        Randomizer.Seed = new Random(settings.RandomizerSeed);
        var faker = new Faker("en");

        await SeedAdminAsync(db, users, departments[0], password, ct).ConfigureAwait(false);
        await SeedEmployeesAsync(db, users, faker, departments, settings.EmployeeCount, password, ct)
            .ConfigureAwait(false);

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        logger.LogInformation(
            "Seed complete: {Departments} departments, {Employees} employees plus the default admin.",
            departments.Count,
            settings.EmployeeCount);
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole> roles, CancellationToken ct)
    {
        foreach (var role in RoleNames)
        {
            ct.ThrowIfCancellationRequested();

            if (!await roles.RoleExistsAsync(role).ConfigureAwait(false))
            {
                await roles.CreateAsync(new IdentityRole(role)).ConfigureAwait(false);
            }
        }
    }

    private static async Task<List<Department>> SeedDepartmentsAsync(
        ApplicationDbContext db,
        CancellationToken ct)
    {
        var existing = await db.Departments.ToListAsync(ct).ConfigureAwait(false);

        if (existing.Count > 0)
        {
            return existing;
        }

        var departments = DepartmentNames
            .Select(name => new Department { Name = name, Description = $"{name} department" })
            .ToList();

        db.Departments.AddRange(departments);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return departments;
    }

    private static async Task SeedAdminAsync(
        ApplicationDbContext db,
        UserManager<ApplicationUser> users,
        Department department,
        string password,
        CancellationToken ct)
    {
        var user = await CreateUserAsync(users, "admin@ems.local", password, "Admin")
            .ConfigureAwait(false);

        db.Employees.Add(new Employee
        {
            UserId = user.Id,
            FirstName = "System",
            LastName = "Administrator",
            Email = "admin@ems.local",
            Phone = "+248 4 000 000",
            DateOfBirth = new DateOnly(1985, 1, 1),
            Address = "1 Independence Avenue, La Rivière Anglaise, Mahé",
            EmergencyContactName = "Not supplied",
            EmergencyContactPhone = "+248 4 000 001",
            Salary = 0m,
            JobTitle = "System Administrator",
            ContractType = ContractType.FullTime,
            DepartmentId = department.Id,
            Role = EmployeeRole.Admin,
            HireDate = new DateOnly(2020, 1, 1),
            Status = EmployeeStatus.Active,
            MustChangePassword = true,
        });

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private static async Task SeedEmployeesAsync(
        ApplicationDbContext db,
        UserManager<ApplicationUser> users,
        Faker faker,
        List<Department> departments,
        int count,
        string password,
        CancellationToken ct)
    {
        var employees = new List<Employee>(count);

        for (var i = 0; i < count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var department = departments[i % departments.Count];

            // The first employee placed in each department manages it.
            var isManager = i < departments.Count;
            var role = isManager ? EmployeeRole.Manager : EmployeeRole.Employee;

            var firstName = faker.PickRandom(FirstNames);
            var lastName = faker.PickRandom(LastNames);

            // Deterministic and collision-free, which a generated address is not. The slug also
            // strips the accents and hyphens that Creole names carry, which an address cannot hold.
            var email = $"{Slug(firstName)}.{Slug(lastName)}{i + 1}@ems.local";

            var user = await CreateUserAsync(users, email, password, role.ToString())
                .ConfigureAwait(false);

            var employee = new Employee
            {
                UserId = user.Id,
                FirstName = firstName,
                LastName = lastName,
                Email = email,
                Phone = $"+248 2 {faker.Random.Number(100, 999)} {faker.Random.Number(100, 999)}",
                DateOfBirth = faker.Date.BetweenDateOnly(
                    new DateOnly(1970, 1, 1),
                    new DateOnly(2002, 12, 31)),
                Address = $"{faker.Random.Number(1, 240)} {faker.PickRandom(Streets)}, " +
                          $"{faker.PickRandom(MaheDistricts)}, Mahé",
                EmergencyContactName = $"{faker.PickRandom(FirstNames)} {faker.PickRandom(LastNames)}",
                EmergencyContactPhone = $"+248 2 {faker.Random.Number(100, 999)} {faker.Random.Number(100, 999)}",
                Salary = Math.Round(faker.Random.Decimal(15_000m, 80_000m), 2),
                JobTitle = faker.Name.JobTitle(),
                ContractType = faker.PickRandom<ContractType>(),
                DepartmentId = department.Id,
                Role = role,
                HireDate = faker.Date.BetweenDateOnly(
                    new DateOnly(2019, 1, 1),
                    new DateOnly(2025, 6, 30)),
                Status = EmployeeStatus.Active,
                MustChangePassword = true,
            };

            employees.Add(employee);
            db.Employees.Add(employee);

            if (isManager)
            {
                department.ManagerId = employee.Id;
            }
        }
    }

    /// <summary>
    /// Reduces a name to the ASCII letters and digits an email local-part can hold.
    /// </summary>
    /// <param name="value">The name, which may carry accents or hyphens.</param>
    /// <returns>The lowercase ASCII form.</returns>
    private static string Slug(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);

        var ascii = decomposed.Where(c =>
            CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark
            && char.IsAsciiLetterOrDigit(c));

        return string.Concat(ascii).ToLowerInvariant();
    }

    private static async Task<ApplicationUser> CreateUserAsync(
        UserManager<ApplicationUser> users,
        string email,
        string password,
        string role)
    {
        var existing = await users.FindByEmailAsync(email).ConfigureAwait(false);

        if (existing is not null)
        {
            return existing;
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
        };

        var created = await users.CreateAsync(user, password).ConfigureAwait(false);

        if (!created.Succeeded)
        {
            var errors = string.Join("; ", created.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Could not create the seeded account {email}: {errors}");
        }

        await users.AddToRoleAsync(user, role).ConfigureAwait(false);

        return user;
    }
}
