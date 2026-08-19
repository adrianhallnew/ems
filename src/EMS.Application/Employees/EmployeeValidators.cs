using EMS.Application.Common.Time;
using FluentValidation;

namespace EMS.Application.Employees;

/// <summary>Field rules for provisioning an employee.</summary>
/// <remarks>
/// Field-level only. Anything needing database state — email uniqueness, the last-admin guard —
/// belongs in the service, inside the transaction that commits the change. A validator that queries
/// the database creates a check-then-act gap between validation and commit.
/// </remarks>
public sealed class CreateEmployeeValidator : AbstractValidator<CreateEmployeeCommand>
{
    /// <summary>Initialises the rules.</summary>
    /// <param name="clock">Supplies today's SCT date.</param>
    public CreateEmployeeValidator(SctClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().MaximumLength(256).EmailAddress();
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Address).NotEmpty().MaximumLength(500);
        RuleFor(x => x.EmergencyContactName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.EmergencyContactPhone).NotEmpty().MaximumLength(30);
        RuleFor(x => x.JobTitle).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Salary).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.DepartmentId).NotEmpty();
        RuleFor(x => x.ContractType).IsInEnum();
        RuleFor(x => x.Role).IsInEnum();

        RuleFor(x => x.DateOfBirth)
            .LessThan(_ => clock.Today)
            .WithMessage("Date of birth must be in the past.");

        RuleFor(x => x.DateOfBirth)
            .Must((command, dateOfBirth) => dateOfBirth.AddYears(16) <= command.HireDate)
            .WithMessage("An employee must be at least 16 years old on their hire date.");

        // Null asks the service to generate one; a supplied value must meet the length policy.
        RuleFor(x => x.TemporaryPassword)
            .MinimumLength(12)
            .MaximumLength(128)
            .When(x => x.TemporaryPassword is not null);
    }
}

/// <summary>Field rules for an Admin's edit of an employee.</summary>
public sealed class UpdateEmployeeValidator : AbstractValidator<UpdateEmployeeCommand>
{
    /// <summary>Initialises the rules.</summary>
    /// <param name="clock">Supplies today's SCT date.</param>
    public UpdateEmployeeValidator(SctClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Address).NotEmpty().MaximumLength(500);
        RuleFor(x => x.EmergencyContactName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.EmergencyContactPhone).NotEmpty().MaximumLength(30);
        RuleFor(x => x.JobTitle).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Salary).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.DepartmentId).NotEmpty();
        RuleFor(x => x.ContractType).IsInEnum();

        RuleFor(x => x.DateOfBirth)
            .LessThan(_ => clock.Today)
            .WithMessage("Date of birth must be in the past.");

        RuleFor(x => x.DateOfBirth)
            .Must((command, dateOfBirth) => dateOfBirth.AddYears(16) <= command.HireDate)
            .WithMessage("An employee must be at least 16 years old on their hire date.");
    }
}

/// <summary>Field rules for an employee editing their own contact details.</summary>
public sealed class UpdateOwnProfileValidator : AbstractValidator<UpdateOwnProfileCommand>
{
    /// <summary>Initialises the rules.</summary>
    public UpdateOwnProfileValidator()
    {
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Address).NotEmpty().MaximumLength(500);
        RuleFor(x => x.EmergencyContactName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.EmergencyContactPhone).NotEmpty().MaximumLength(30);
    }
}

/// <summary>Field rules for an Admin changing an employee's login email.</summary>
public sealed class ChangeEmployeeEmailValidator : AbstractValidator<ChangeEmployeeEmailCommand>
{
    /// <summary>Initialises the rules.</summary>
    /// <remarks>
    /// Uniqueness is Identity's to enforce; this only checks the address parses and fits.
    /// </remarks>
    public ChangeEmployeeEmailValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.NewEmail).NotEmpty().MaximumLength(256).EmailAddress();
    }
}

/// <summary>Field rules for an employee changing their own login email.</summary>
public sealed class ChangeOwnEmailValidator : AbstractValidator<ChangeOwnEmailCommand>
{
    /// <summary>Initialises the rules.</summary>
    public ChangeOwnEmailValidator()
    {
        RuleFor(x => x.NewEmail).NotEmpty().MaximumLength(256).EmailAddress();
    }
}

/// <summary>Field rules for deactivating an employee.</summary>
public sealed class DeactivateEmployeeValidator : AbstractValidator<DeactivateEmployeeCommand>
{
    /// <summary>Initialises the rules.</summary>
    public DeactivateEmployeeValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

/// <summary>Field rules for a role change.</summary>
public sealed class ChangeEmployeeRoleValidator : AbstractValidator<ChangeEmployeeRoleCommand>
{
    /// <summary>Initialises the rules.</summary>
    public ChangeEmployeeRoleValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.Role).IsInEnum();
    }
}
