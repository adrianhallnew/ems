using Microsoft.EntityFrameworkCore;

namespace EMS.Application.Common.Interfaces;

/// <summary>
/// Classifies provider-specific database failures that the application treats as outcomes rather
/// than as bugs.
/// </summary>
/// <remarks>
/// A unique index is the authoritative guard against a duplicate clock-in: a prior read narrows the
/// window but never closes it (spec §3.3.4). Recognising the violation means reading a SQL Server
/// error number, and <c>Microsoft.Data.SqlClient</c> is not referenceable from this layer (ADR-0003).
/// The port is declared here and implemented in Infrastructure, as ADR-0013 does for the context
/// factory. See ADR-0015.
/// </remarks>
public interface IDatabaseErrorClassifier
{
    /// <summary>
    /// Determines whether a save failure was caused by a unique index or unique constraint violation.
    /// </summary>
    /// <param name="exception">The failure raised by <c>SaveChangesAsync</c>.</param>
    /// <returns><see langword="true"/> if the write collided with a uniqueness guarantee.</returns>
    bool IsUniqueViolation(DbUpdateException exception);
}
