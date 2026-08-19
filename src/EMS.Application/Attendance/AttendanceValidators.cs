using FluentValidation;

namespace EMS.Application.Attendance;

/// <summary>Field rules for an Admin correction.</summary>
/// <remarks>
/// The note is mandatory because a correction without a stated reason is indistinguishable from
/// tampering when the audit trail is read later (spec section 3.3.6).
/// </remarks>
public sealed class CorrectAttendanceValidator : AbstractValidator<CorrectAttendanceCommand>
{
    /// <summary>Initialises the rules.</summary>
    public CorrectAttendanceValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.CorrectionNote).NotEmpty().MaximumLength(500);

        RuleFor(x => x.ClockIn)
            .NotNull()
            .When(x => x.ClockOut is not null)
            .WithMessage("A clock-out cannot be recorded without a clock-in.");

        RuleFor(x => x.ClockOut)
            .Must((command, clockOut) => clockOut is null || command.ClockIn is null || clockOut > command.ClockIn)
            .WithMessage("Clock-out must be after clock-in.");

        RuleFor(x => x)
            .Must(command => command.ClockIn is not null || command.ClockOut is not null)
            .WithMessage("A correction must set at least one of clock-in and clock-out.");
    }
}
