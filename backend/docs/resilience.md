# Resilience

What the backend does when something outside the process is slow, down or busy. Two places can fail like that: Keycloak (an HTTP call) and MySQL (a lost race or a dropped connection). In both, the rule is the same: **repeat only what is safe to repeat, and give up fast when it is not going to work**.

## Keycloak

The adapter (`KeycloakIdentityProvider`) uses the named client `keycloak` of `IHttpClientFactory` with the standard resilience handler of `Microsoft.Extensions.Http.Resilience`, adjusted like this:

| Strategy | Value | Why |
|---|---|---|
| Total timeout | `KeycloakOptions.RequestTimeout` (15 s) | The longest a caller waits for one call of the adapter, retries included. |
| Attempt timeout | `KeycloakOptions.AttemptTimeout` (5 s) | One slow attempt does not eat the whole budget. Must be greater than zero and less than the total timeout. |
| Retry | 2 more attempts, exponential from 300 ms, with jitter, **only for GET** | A read can be repeated. A POST, PUT or DELETE cannot, see below. |
| Circuit breaker | opens at 50% of failures with at least 5 calls in 30 s, stays open for 30 s | While Keycloak is down, calls fail at once instead of each one waiting for its timeout. |

What counts as a failure for the retry and for the breaker: no answer (network error), timeout, 408, 429 and 5xx. A 409 (e-mail already in use), a 404 and the other 4xx are **answers** of a healthy Keycloak and never count.

The sampling window of the breaker is never shorter than twice the attempt timeout (Polly refuses otherwise), so it grows when `AttemptTimeout` is raised above 15 s.

### Why only GET is repeated

Creating an account is a POST. If the answer is lost, the account may exist, and repeating would create a second one or fail with a confusing 409. The same goes for the e-mail of the first access (it would be sent twice). The update and the enable/disable are a read (GET, repeated) followed by a PUT (not repeated). DELETE is idempotent, but it is only used to undo a creation, and a second attempt there brings little; it stays out of the rule so the rule has no exception.

The login of the service client (the token) is a POST too, so it is not repeated either. The token is kept until it is about to expire, so this is one call every few minutes, not one per request.

### What the caller sees

Everything that ends in a refusal of the pipeline (timeout, open circuit) or in a network error is wrapped by the adapter in `IdentityProviderException`, and the Api answers **503 `Identity.Unavailable`**. The exception message may carry a piece of the answer of Keycloak, so it goes to the log and never to the body.

## Database

### 1. The driver repeats transient failures

`UseMySql(..., EnableRetryOnFailure(3 attempts, up to 2 s))`. EF repeats by itself what failed for a reason that passes (a dropped connection, a server that restarted), for reads and for `SaveChanges`. The code never opens a transaction by hand (the only transaction is the one of `SaveChanges`), which is what this strategy needs.

### 2. Losing a race is one error

A save can fail because someone else changed the same data first:

- `DbUpdateConcurrencyException`: the concurrency token (the status of an appointment, the `ScheduleVersion` of a professional...) is not the one that was read.
- MySQL error 1213 (deadlock): two bookings for the same professional lock each other. MySQL picks one and undoes it. This is what RN27 looks like in practice.
- MySQL error 1205 (lock wait timeout): a lock was held for too long.

`PlanelyDbContext.SaveChangesAsync` looks for these (also inside the inner exceptions, because EF wraps the last failure of a retried operation in `RetryLimitExceededException`) and throws `ConcurrencyConflictException`, a type of the Domain. Nobody above the Infrastructure needs to know MySQL error numbers. A violation of a unique key is **not** translated: it is a different problem.

### 3. A use case that is safe to repeat runs again

The decorator `RetryOnConflictCommandHandler` sits between validation and the transaction:

```
logging → validation → retry on conflict → transaction → domain events → handler
```

When a command that implements `IRetryOnConflict` fails with `ConcurrencyConflictException`, the decorator discards what the unit of work tracks (`IUnitOfWork.DiscardChanges()`), waits a short random time and runs the whole use case again, from the reads: the second run sees what the winner saved, so it either succeeds on the new state or fails with a business error ("the slot is taken"). At most 2 repeats (3 runs). If the last one also loses, the exception goes up.

**It is opt-in, and the rule to opt in is: the command has no effect outside the database.** A run that is repeated does everything again, so a call to Keycloak, an e-mail or a file written in the middle would be done twice. Today only the commands that raise the version of the agenda of a professional (RN27) are marked: `CreateAppointmentCommand`, `RescheduleAppointmentCommand` and `SetAppointmentItemPriceCommand`. The commands of users (which call Keycloak) are not.

### What the caller sees

A lost race that survives the repeats (or happens in a command that is not marked) is **409 `Concurrency.Conflict`**. The request changed nothing, and the caller may send it again. The log has one warning, with no stack trace: it is not a failure of the Api.

The reminder job catches `ConcurrencyConflictException` too: if two instances pick the same reminder, the one that loses just skips it.

## Where each piece is

| Piece | Project |
|---|---|
| `ConcurrencyConflictException`, `IUnitOfWork.DiscardChanges()` | Domain (`SeedWork`) |
| `IRetryOnConflict`, `RetryOnConflictCommandHandler` | Application (`Abstractions`) |
| `ConcurrencyFailures`, the override of `SaveChangesAsync`, `EnableRetryOnFailure` | Infrastructure (`Persistence`) |
| The `keycloak` client and its pipeline | Infrastructure (`Identity`) |
| The 409 and 503 answers | Api (`Errors`) |

## Tests

- `Application.Tests/Pipeline/RetryOnConflictTests`: the repeat, its limit, and everything that must not be repeated.
- `IntegrationTests/Identity/KeycloakResilienceTests`: retry only for reads, a conflict that does not open the circuit, the circuit opening, the attempt timeout. No Docker.
- `IntegrationTests/Persistence/UnitOfWorkTests`: `DiscardChanges` and a real lock wait timeout becoming `ConcurrencyConflictException`.
- `Api.Tests/ExternalFailureTests`: the 409 and the 503, without leaking the message.
