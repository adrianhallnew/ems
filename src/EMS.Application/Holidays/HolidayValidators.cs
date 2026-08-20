using EMS.Domain.Enums;
using FluentValidation;

namespace EMS.Application.Holidays;

/// <summary>Field rules for adding a holiday.</summary>
/// <remarks>
/// That the date is free is a unique index, checked by the service on write. Two observances on one
/// date are recorded as a single entry with a combined name (spec section 3.7.1).
/// </remarks>
public sealed class CreateHolidayValidator : AbstractValidator<CreateHolidayCommand>
{
    /// <summary>Initialises the rules.</summary>
    public CreateHolidayValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Rule).IsInEnum();

        RuleFor(x => x.EasterOffset)
            .NotNull()
            .When(x => x.Rule == HolidayRule.EasterRelative)
            .WithMessage("An Easter-relative holiday needs an offset from Easter Sunday.");

        RuleFor(x => x.EasterOffset)
            .Null()
            .When(x => x.Rule == HolidayRule.FixedDate)
            .WithMessage("A fixed-date holiday carries no Easter offset.");
    }
}

/// <summary>Field rules for editing a holiday.</summary>
public sealed class UpdateHolidayValidator : AbstractValidator<UpdateHolidayCommand>
{
    /// <summary>Initialises the rules.</summary>
    public UpdateHolidayValidator()
    {
        RuleFor(x => x.HolidayId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Rule).IsInEnum();

        RuleFor(x => x.EasterOffset)
            .NotNull()
            .When(x => x.Rule == HolidayRule.EasterRelative)
            .WithMessage("An Easter-relative holiday needs an offset from Easter Sunday.");

        RuleFor(x => x.EasterOffset)
            .Null()
            .When(x => x.Rule == HolidayRule.FixedDate)
            .WithMessage("A fixed-date holiday carries no Easter offset.");
    }
}
