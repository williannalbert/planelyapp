# Api error responses

Every failure of the Api is `application/problem+json` (RFC 9457). The Api never writes the text of an error in code: `code` is the stable key, and from 5.2 on `detail` carries the text in the language of the request (`Accept-Language`).

```json
{
  "status": 422,
  "title": "Unprocessable Entity",
  "code": "Customer.ContactRequired",
  "arguments": ["5", "x"],
  "correlationId": "3f2a9c...",
  "traceId": "00-4bf92f..."
}
```

| Field | When | Meaning |
|---|---|---|
| `status`, `title` | always | The HTTP status and its standard phrase. |
| `code` | always | The key to translate. `<Type>.<Problem>` for a rule or an error of a use case, `Http.<Phrase>` for a status with no use case behind it, `Server.UnexpectedError` for a failure of the Api, `Concurrency.Conflict` and `Identity.Unavailable` for a failure outside the Api that the caller can try again. |
| `arguments` | when the code has placeholders | The values for `{0}`, `{1}`... of the message, as text. |
| `errors` | `Validation.Failed` only | One `{ "field", "code" }` for each field refused. |
| `correlationId` | always | The id of the request, the same as the `X-Correlation-Id` header. |
| `traceId` | always | The trace of the request (W3C), for the logs. |

## Which status

| Source | Status |
|---|---|
| `Error` of type Validation (`ValidationError`) | 400 |
| `Error` of type Unauthorized | 401 |
| `Error` of type Forbidden | 403 |
| `Error` of type NotFound | 404 |
| `Error` of type Conflict | 409 |
| `DomainException` (a rule of the Domain said no) | 422 |
| `ConcurrencyConflictException` (the data changed while the request ran, and nothing was saved) | 409, `Concurrency.Conflict`, no detail of the exception in the body. The caller may send the same request again |
| `IdentityProviderException` (Keycloak could not be reached, was too slow, or refused in a way nobody can act on) | 503, `Identity.Unavailable`, no detail of the exception in the body. The caller may try again later |
| Any other exception | 500, `Server.UnexpectedError`, no detail of the exception in the body (it is logged) |
| A status with no body (an unknown route, a wrong method) | The same status, as a problem with `Http.NotFound`, `Http.MethodNotAllowed`... |

## Correlation id

The Api reads `X-Correlation-Id` from the request and uses it when it is at most 64 characters of letters, digits, `-`, `_` and `.` (it goes to logs and headers, so nothing else is trusted). Otherwise it makes one. The id is in every log line of the request and in the `X-Correlation-Id` header of the response, also when the response is an error.

The two answers that ask the caller to try again are described, with the retries that already happened before them, in `resilience.md`.
