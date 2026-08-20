using EMS.Application.Common.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace EMS.Infrastructure.Data;

/// <summary>
/// Recognises SQL Server uniqueness failures. See ADR-0015.
/// </summary>
public sealed class SqlServerErrorClassifier : IDatabaseErrorClassifier
{
    /// <summary>Duplicate key row in an object with a unique index.</summary>
    private const int DuplicateKeyInUniqueIndex = 2601;

    /// <summary>Violation of a UNIQUE KEY or PRIMARY KEY constraint.</summary>
    private const int UniqueConstraintViolation = 2627;

    /// <inheritdoc/>
    public bool IsUniqueViolation(DbUpdateException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        // EF Core wraps the provider failure, so the number lives on the inner exception. A batch
        // can report several errors; any one of them being a uniqueness failure is enough.
        return exception.InnerException is SqlException sql
               && sql.Errors.Cast<SqlError>().Any(error =>
                   error.Number is DuplicateKeyInUniqueIndex or UniqueConstraintViolation);
    }
}
