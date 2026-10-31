# Application error codes

A use case does not write messages. It returns `Result<T>` with an `Error` that carries a stable code, a type and, when the message needs them, arguments. The Api turns the type into an HTTP status and the code into a text in pt-BR, en or es (`.resx` key = code, arguments fill `{0}`, `{1}`...).

The errors that come from a rule of the business are not in this file: the Domain throws `DomainException`, the Api answers 422 and the codes are in `error-codes.md`.

**Convention:** `<Type>.<Problem>` in PascalCase, like the Domain codes. The prefix is the aggregate or the concept the use case is about.

**Types and status:**

| Type | HTTP | When |
|---|---|---|
| `Validation` | 400 | The request is malformed. It lists the fields (`Validation.Failed`). |
| `NotFound` | 404 | What the request points to does not exist, or is not visible to this company. |
| `Conflict` | 409 | The request clashes with the current state. |
| `Forbidden` | 403 | The user is known but may not do this. |

## Validation

A request with a malformed field answers `Validation.Failed` (400) with one entry per problem, each with the name of the field (`Name`, or `Address.Street` for a field inside another object) and one of the codes below. The Api translates each code once, not once per field.

| Code | Meaning |
|---|---|
| `Validation.Failed` | The request has one or more malformed fields (the list says which). |
| `Validation.Required` | The field is missing or empty. |
| `Validation.Invalid` | The field has a value the Domain value object refuses (an e-mail, a CNPJ, a phone, a language...). |
| `Validation.TooLong` | The field has more characters than allowed. |
| `Validation.OutOfRange` | The number is outside the allowed range. |

A validator can also set its own code with `WithErrorCode`; it is kept as it is and listed in the section of its use case.

## Company

| Code | Type (HTTP) | Meaning |
|---|---|---|
| `Company.CnpjAlreadyRegistered` | Conflict (409) | A company with this CNPJ is already registered. |
| `Company.NotFound` | NotFound (404) | The company of the request does not exist. |

## Branch

| Code | Type (HTTP) | Meaning |
|---|---|---|
| `Branch.NameAlreadyExists` | Conflict (409) | The company already has a branch with this name (the comparison ignores case). |
| `Branch.NotFound` | NotFound (404) | The branch does not exist or belongs to another company. |

## Professional

| Code | Type (HTTP) | Meaning |
|---|---|---|
| `Professional.NotFound` | NotFound (404) | The professional does not exist or belongs to another company. |
