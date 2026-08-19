using FluentValidation;

namespace EMS.Application.Departments;

/// <summary>Field rules for creating a department.</summary>
/// <remarks>Name uniqueness is a unique index, checked by the service on write.</remarks>
public sealed class CreateDepartmentValidator : AbstractValidator<CreateDepartmentCommand>
{
    /// <summary>Initialises the rules.</summary>
    public CreateDepartmentValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

/// <summary>Field rules for editing a department.</summary>
public sealed class UpdateDepartmentValidator : AbstractValidator<UpdateDepartmentCommand>
{
    /// <summary>Initialises the rules.</summary>
    public UpdateDepartmentValidator()
    {
        RuleFor(x => x.DepartmentId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

/// <summary>Field rules for assigning a department manager.</summary>
/// <remarks>
/// That the assignee is an active Manager or Admin is a database question, so the service checks
/// it inside the write.
/// </remarks>
public sealed class AssignDepartmentManagerValidator : AbstractValidator<AssignDepartmentManagerCommand>
{
    /// <summary>Initialises the rules.</summary>
    public AssignDepartmentManagerValidator()
    {
        RuleFor(x => x.DepartmentId).NotEmpty();
    }
}
