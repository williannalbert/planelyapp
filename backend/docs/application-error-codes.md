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
| `Unauthorized` | 401 | The caller could not be identified: a refresh token that is unknown, expired or revoked. |

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
| `Company.Inactive` | Conflict (409) | The company is inactive, so nothing can be booked. |
| `Company.NotFound` | NotFound (404) | The company of the request does not exist. |

## Branch

| Code | Type (HTTP) | Meaning |
|---|---|---|
| `Branch.AccessDenied` | Forbidden (403) | The user has no access to the branch (RN11). |
| `Branch.Inactive` | Conflict (409) | The branch is inactive, so nothing can be booked there. |
| `Branch.NameAlreadyExists` | Conflict (409) | The company already has a branch with this name (the comparison ignores case). |
| `Branch.NotFound` | NotFound (404) | The branch does not exist or belongs to another company. |

## Professional

| Code | Type (HTTP) | Meaning |
|---|---|---|
| `Professional.CannotPerformService` | Conflict (409) | The professional does not perform one of the services asked (RN09). |
| `Professional.Inactive` | Conflict (409) | The professional is inactive, so cannot be booked. |
| `Professional.NotAtBranch` | Conflict (409) | The professional has no working hours at this branch (RN04). |
| `Professional.NotFound` | NotFound (404) | The professional does not exist or belongs to another company. |
| `Professional.TimeOffNotFound` | NotFound (404) | The professional has no time off with this id. (The Domain throws the same code as a 422 if asked directly.) |
| `Professional.UserAlreadyLinked` | Conflict (409) | Another professional of the company is already linked to this user. |

## ServiceCategory

| Code | Type (HTTP) | Meaning |
|---|---|---|
| `ServiceCategory.NameAlreadyExists` | Conflict (409) | The company already has a category with this name (the comparison ignores case). |
| `ServiceCategory.NotFound` | NotFound (404) | The category does not exist or belongs to another company. |

## ServiceOffering

| Code | Type (HTTP) | Meaning |
|---|---|---|
| `ServiceOffering.NameAlreadyExists` | Conflict (409) | The company already has a service with this name (the comparison ignores case). |
| `ServiceOffering.NotAvailableInBranch` | Conflict (409) | The service is inactive or the branch does not offer it, so it cannot be booked there. |
| `ServiceOffering.NotFound` | NotFound (404) | The service does not exist or belongs to another company. |

## Promotion

| Code | Type (HTTP) | Meaning |
|---|---|---|
| `Promotion.NotFound` | NotFound (404) | The promotion does not exist or belongs to another company. |

## Appointment

| Code | Type (HTTP) | Meaning |
|---|---|---|
| `Appointment.NotFound` | NotFound (404) | The appointment does not exist, belongs to another company, or (for a customer) is not theirs. |
| `Appointment.NotTheCustomer` | Forbidden (403) | A customer tried to book for another customer. (The Domain throws the same code as a 422 when asked about an appointment that is not the customer's.) |
| `Appointment.StaffOnly` | Forbidden (403) | A customer tried an action that only the staff of the company can do (confirm, start, complete, no-show, set the price of a service, list the agenda of a branch or of a professional). |
| `Appointment.TimeNotAvailable` | Conflict (409) | The professional is not free for the whole period: working hours, break, time off or another appointment (RN01, RN02, RN12). It is also the answer when moving an appointment to a time that is taken, and when the agreed duration of a service on request makes the period longer than the free time. A booking, a move or a price that loses a race for the same time is also a 409 (RN27): the Api answers it from the concurrency exception. |

## Customer

| Code | Type (HTTP) | Meaning |
|---|---|---|
| `Customer.EmailAlreadyRegistered` | Conflict (409) | Another customer of the company already has this e-mail. |
| `Customer.PhoneAlreadyRegistered` | Conflict (409) | Another customer of the company already has this phone. |
| `Customer.CpfAlreadyRegistered` | Conflict (409) | Another customer of the company already has this CPF. |
| `Customer.NameRequired` | Conflict (409) | The customer has no name yet, and an appointment needs one (RN24). |
| `Customer.NotFound` | NotFound (404) | The customer does not exist or belongs to another company. A customer who asks for the appointments of another customer gets the same answer. |

## User

| Code | Type (HTTP) | Meaning |
|---|---|---|
| `User.NotFound` | NotFound (404) | The user does not exist or belongs to another company. |
| `User.EmailAlreadyRegistered` | Conflict (409) | Another user of the company already has this e-mail. |
| `User.LastAdministrator` | Conflict (409) | The company would be left without an active administrator. |

## CustomerSession

| Code | Type (HTTP) | Meaning |
|---|---|---|
| `CustomerSession.RefreshTokenInvalid` | Unauthorized (401) | The refresh token is unknown, expired or revoked. The three cases look the same on purpose, so nobody learns which tokens exist. A token that was already renewed is unknown too: only the newest one works. |

## AccessCode

A wrong, expired or used access code is **not** an error of the use case: `VerifyAccessCodeCommand` succeeds and says what happened in `Outcome` (`Verified`, `Invalid`, `Expired`, `AlreadyUsed`, `TooManyAttempts`). The attempt has to be saved, and a failed result is never saved. The Api answers `Verified` with 200 and the rest with 401 (`Invalid`, `Expired`, `AlreadyUsed`) or 429 (`TooManyAttempts`). Asking for a code too soon or too often is a Domain rule (`AccessCode.ResendTooSoon`, `AccessCode.ResendLimitReached`, in `error-codes.md`): the Api should answer those two with 429, not the usual 422.

If the code cannot be delivered, the sender throws `AccessCodeDeliveryException`: nothing is saved, so the attempt does not count against the limit of the customer, and the Api answers 502 or 503.

## Identity

Errors returned by the identity provider (`IIdentityProvider`) that the use case passes on as they are.

| Code | Type (HTTP) | Meaning |
|---|---|---|
| `Identity.EmailAlreadyInUse` | Conflict (409) | Another account of the platform already uses this e-mail. |

When the identity provider cannot be reached, refuses a request or answers something unexpected, the adapter throws `IdentityProviderException`. It is not a result of the use case: the Api answers 502 or 503, and the text is not translated.
