# Endpoints of the Api

How an endpoint is written, who may call it and what it answers. The company endpoints (5.5a) are the first; the others follow the
same shape.

## The shape of an endpoint

An endpoint reads the request, sends **one** command or query through `IDispatcher`, and turns the `Result` into HTTP. It has no
rule of the business and no `try/catch`: the rules are in the Domain, the outcomes the caller must handle are `Error`s of the use
case, and the exceptions are turned into problems by the handlers in `Errors/`.

```csharp
company.MapPut("/", async (UpdateCompanyDetailsCommand command, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        (await dispatcher.SendAsync(command, cancellationToken)).ToNoContent())
    .RequireRoles(UserRole.CompanyAdmin);
```

| The use case returns | The helper | Success | Failure |
|---|---|---|---|
| A value | `ToHttp()` | 200 with the value | The status of the error type, see `api-errors.md` |
| Nothing (`Unit`) | `ToNoContent()` | 204 | the same |
| The id of what was created | `ToCreated(id => "/things/" + id)` | 201, `Location`, body `{ "id": ... }` | the same |

The commands and queries of the Application are the body of the request: the Api does not copy them into other types. They are
public records without Domain types (an architecture test checks it), so what the client sends is exactly what the use case
validates. The company and the branch of the request are **not** in the body: they come from the route (see `api-tenancy.md`).

Routes use the `{companyId:guid}` constraint, so an id that is not a Guid is a 404 before anything else. The tenancy check is the
second guard (it also refuses the empty Guid).

## Roles

Every endpoint declares which roles may call it, and an endpoint that declares nothing is **closed**:

| Declaration | Meaning |
|---|---|
| `.RequireRoles(UserRole.CompanyAdmin, UserRole.Receptionist)` | Only these roles. Naming none throws when the Api starts |
| `.RequireAnyStaff()` | Any signed-in staff member (all four roles) |
| `.AllowAnonymous()` | Open: no token, no user, no role (health, documentation) |
| Nothing | 403 `User.RoleNotAllowed` and an **error** in the log naming the endpoint |

A declaration on a group (`MapGroup(...).RequireRoles(...)`) applies to every endpoint of the group, and a declaration on an
endpoint replaces the one of its group. The roles are not hierarchical: the platform administrator is not "above" the company
administrator, it simply has its own list of endpoints. If it also has to call an endpoint, the endpoint names it.

The order of the checks of a request is: token (401), user in the database (403 `User.NotRegistered` / `User.Inactive`), company and branch
of the route (403 `Company.AccessDenied` / `Branch.AccessDenied`), role (403 `User.RoleNotAllowed`), validation of the body, the use case.
A refused request never reaches a use case, and the log of a refusal has ids and the code only.

### Company endpoints (5.5a)

| Method and route | Use case | Success | Roles |
|---|---|---|---|
| `POST /companies` | `RegisterCompanyCommand` | 201, `Location: /companies/{id}` | PlatformAdmin |
| `GET /companies/{companyId}` | `GetCompanyQuery` | 200 `CompanyDto` | any staff |
| `PUT /companies/{companyId}` | `UpdateCompanyDetailsCommand` | 204 | CompanyAdmin |
| `PUT /companies/{companyId}/booking-policy` | `UpdateBookingPolicyCommand` (the body is the policy itself) | 204 | CompanyAdmin |
| `POST /companies/{companyId}/activate` | `ActivateCompanyCommand` | 204 | PlatformAdmin |
| `POST /companies/{companyId}/deactivate` | `DeactivateCompanyCommand` | 204 | PlatformAdmin |

The company administrator can only enter its own company (RN23); the platform administrator can enter any company's routes. There
is no "list the companies" use case yet, so the platform administrator learns the id of a company from `POST /companies`.

## JSON

- Property names are camelCase.
- An enum is written and read as its **name** (`"CompanyAdmin"`), never as a number: the number of a member is not part of the
  contract, and a client that sends `1` gets a 400. An unknown name is a 400 too.
- Ids are Guids in text; dates and times follow the ISO 8601 of `System.Text.Json`.

## A body the framework cannot read

| What the client sent | Answer |
|---|---|
| Invalid JSON, a field of the wrong type, an enum that does not exist, an empty body | 400 `Http.BadRequest` |
| A content type that is not `application/json` | 415 `Http.UnsupportedMediaType` |

In Development the framework throws an exception for these cases; in Production it only sets the status. `BadRequestExceptionHandler`
turns the first into the same problem the second gets, so a client sees one behaviour in every environment, and the text of the
framework (which names types and properties) never reaches the body.

A body that is valid JSON but breaks a rule of the format (a missing name, an invalid CNPJ) is a 400 `Validation.Failed` with the
list of fields, from the validator of the use case. A property that is missing from the JSON arrives as `null`/`0` in the command,
and the validators of the commands refuse a null where it matters.

## Trying the Api

The collection of Bruno in `backend/requests/` has every endpoint of the Api. It uses only the development values, no real credential.

1. Start MySQL, RabbitMQ and Keycloak (`infra`), apply the migrations and run the Api (see the README).
2. Open the folder `backend/requests` as a collection in Bruno and choose the environment **Development**.
3. Run **Get token** (login of `admin@planelyapp.local` in the realm of development). It saves the token in the variable `token`,
   which every other request sends as a Bearer.
4. Run **Me**: 200 with the role `PlatformAdmin` (the Api created this user at startup, see `api-authentication.md`).
5. Run **Register company**. It saves the id of the new company in `companyId`; **Get company**, **Update company**, and the rest use it.

The token lives for minutes: when a request answers 401, run **Get token** again.
