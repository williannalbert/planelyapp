using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace PlanelyApp.Infrastructure.Persistence;

/// <summary>Knows which failures of a save mean "someone else got there first". The only place that knows MySQL error numbers.</summary>
internal static class ConcurrencyFailures
{
    /// <summary>
    /// A concurrency token that no longer matches, a deadlock (1213) or a lock wait that ran out (1205). It looks at the
    /// inner exceptions too, because EF wraps the last failure of a retried operation (RetryLimitExceededException).
    /// A unique key violation is a different problem and is not here.
    /// </summary>
    public static bool IsLostRace(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbUpdateConcurrencyException
                || current is MySqlException { ErrorCode: MySqlErrorCode.LockDeadlock or MySqlErrorCode.LockWaitTimeout })
            {
                return true;
            }
        }

        return false;
    }
}