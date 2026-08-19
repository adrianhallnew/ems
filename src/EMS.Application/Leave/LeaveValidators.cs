using EMS.Application.Common.Time;
using FluentValidation;

namespace EMS.Application.Leave;

/// <summary>Field rules for a leave request.</summary>
/// <remarks>
/// Balance sufficiency, overlap, probation and the reset-boundary check are deliberately absent.
/// Each needs database state and must be re-checked inside the transaction that commits the
/// request; checking here as well would give a false sense of atomicity.
/// </remarks>
public sealed class SubmitLeaveValidator : AbstractValidator<SubmitLeaveCommand>
{
    /// <summary>Initialises the rules.</summary>
    /// <param name="clock">Supplies today's SCT date.</param>
    public SubmitLeaveValidator(SctClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        RuleFor(x => x.LeaveType).IsInEnum();

        RuleFor(x => x.StartDate)
            .GreaterThanOrEqualTo(_ => clock.Today)
            .WithMessage("Leave cannot be backdated.");

        RuleFor(x => x.EndDate)
            .GreaterThanOrEqualTo(x => x.StartDate)
            .WithMessage("The end date cannot precede the start date.");

        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

/// <summary>Field rules for approving a request.</summary>
public sealed class ApproveLeaveValidator : AbstractValidator<ApproveLeaveCommand>
{
    /// <summary>Initialises the rules.</summary>
    public ApproveLeaveValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty();
        RuleFor(x => x.Note).MaximumLength(500);
    }
}

/// <summary>Field rules for rejecting a request.</summary>
public sealed class RejectLeaveValidator : AbstractValidator<RejectLeaveCommand>
{
    /// <summary>Initialises the rules.</summary>
    public RejectLeaveValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty();
        RuleFor(x => x.Note).MaximumLength(500);
    }
}

/// <summary>Field rules for cancelling a request.</summary>
public sealed class CancelLeaveValidator : AbstractValidator<CancelLeaveCommand>
{
    /// <summary>Initialises the rules.</summary>
    public CancelLeaveValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty();
        RuleFor(x => x.Note).MaximumLength(500);
    }
}

/// <summary>Field rules for an Admin balance adjustment.</summary>
/// <remarks>The note is mandatory: an unexplained balance change cannot be reconciled later.</remarks>
public sealed class AdjustLeaveBalanceValidator : AbstractValidator<AdjustLeaveBalanceCommand>
{
    /// <summary>Initialises the rules.</summary>
    public AdjustLeaveBalanceValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.LeaveType).IsInEnum();
        RuleFor(x => x.Entitlement).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Note).NotEmpty().MaximumLength(500);
    }
}

/// <summary>Field rules for granting maternity leave.</summary>
public sealed class GrantMaternityLeaveValidator : AbstractValidator<GrantMaternityLeaveCommand>
{
    /// <summary>Initialises the rules.</summary>
    public GrantMaternityLeaveValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.Entitlement).GreaterThan(0);
        RuleFor(x => x.Note).NotEmpty().MaximumLength(500);

        RuleFor(x => x.PeriodEnd)
            .GreaterThanOrEqualTo(x => x.PeriodStart)
            .WithMessage("The period end cannot precede the period start.");
    }
}
