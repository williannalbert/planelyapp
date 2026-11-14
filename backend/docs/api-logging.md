# Api logs and documentation

## Logs

The Api logs through `ILogger`, with Serilog as the provider (`AddApiLogging`). Nothing in the Application or the Domain knows Serilog.

| Environment | Format |
|---|---|
| Development | One readable line: time, level, correlation id, message. |
| Any other | One JSON object per line (`CompactJsonFormatter`), with every property of the event, `CorrelationId` included. |

The levels come from the `Serilog:MinimumLevel` section of the configuration (`Default` and one `Override` per namespace). The format is chosen in code, not in the JSON: lists of the configuration are merged by position between `appsettings.json` and the one of the environment, so a second sink would be added to the first instead of replacing it.

### The correlation id in every line

`CorrelationIdMiddleware` opens a log scope with the id of the request. Serilog turns the keys of a scope into properties of the events, so every line written while the request runs has `CorrelationId`: the line of the request, the lines of the use cases (`ILogger<UseCase>`), the lines of EF Core and the error of an unhandled exception. The same id is in the `X-Correlation-Id` header and in the body of an error (see `api-errors.md`), so a report from the front end finds its lines in the logs.

### One line per request

`RequestLoggingMiddleware` writes `{Method} {Path} responded {StatusCode} in {ElapsedMs} ms` when the response ends.

| Response | Level |
|---|---|
| `/health/*` | Debug (a probe every few seconds would hide the rest) |
| 5xx | Warning (the exception itself is logged as Error by the exception handler, with the stack) |
| Anything else, 4xx included | Information |

### What is never logged

Logs are kept longer than the data they describe and are read by more people, so they follow the rule that is already in the use cases (`RequestLogging`): **names, codes and ids, never content**.

- The query string is not logged (`Path` has no `?...`).
- Bodies and headers are not logged. A request has an e-mail, a phone or a name in its body.
- A new log line never interpolates a value of a customer, an e-mail, a phone, a token or an access code. Log the id of the thing.
- EF Core logs at Warning and above only: at Information it writes every SQL command.

## OpenAPI and Scalar

In Development the Api serves the OpenAPI document at `/openapi/v1.json` and the Scalar page, which reads it, at `/scalar/v1`. In any other environment neither route exists (they answer 404). Opening the documentation outside Development is a decision of 5.10, because it lists every route.
