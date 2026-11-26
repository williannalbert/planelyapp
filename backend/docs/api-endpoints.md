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

## Where the files live

`Endpoints/` has the shape of the Application: one folder per module and, inside it, one folder per resource, like
`Application/<Module>/<Resource>`. Whoever knows where a use case is knows where its endpoint is.

```text
Endpoints/
  Account/MeEndpoints.cs
  Catalog/
    CatalogEndpoints.cs               MapCatalog(): the only line of this module that Program.cs knows
    ServiceCategories/ServiceCategoryEndpoints.cs, ServiceCategoryBody.cs
    ServiceOfferings/ServiceOfferingEndpoints.cs, ServiceOfferingDetailsBody.cs
  Organization/
    OrganizationEndpoints.cs          MapOrganization(): the same, for this module
    Companies/CompanyEndpoints.cs
    Branches/BranchEndpoints.cs, BranchDetailsBody.cs
    Customers/CustomerEndpoints.cs, CustomerContactBody.cs, CustomerProfileBody.cs, MarketingConsentBody.cs
    Professionals/ProfessionalEndpoints.cs, ProfessionalDetailsBody.cs, TimeOffBody.cs, OfferingBody.cs, LinkUserBody.cs
    Users/UserEndpoints.cs, CreateUserBody.cs, UserDetailsBody.cs, UserRoleBody.cs, BranchAccessBody.cs
    Shared/DailyScheduleBody.cs, WeekdayRoute.cs, FieldProblems.cs
```

- **A resource is a folder.** `<Resource>Endpoints.cs` has one `Map<Resources>()` and the bodies of its routes sit next to it, one type per file
  (`<Thing>Body`). A body is the contract of the Api for one route, so it lives with the route and not in the Application.
- **The namespace is the folder** (`PlanelyApp.Api.Endpoints.Organization.Branches`).
- **`Shared/` is for what two resources of the module use** (the body of a weekday, the check of `{day}`). Something only one resource uses stays beside
  it, even if it might be used again: it moves when the second use appears, not before.
- **A new resource** is a folder and one line in `Map<Module>()`. `Program.cs` does not change.
- **The tests** of a resource are `<Resource>EndpointsTests.cs`. `OrganizationEndpointsTests` and `CatalogEndpointsTests` look at a module as a whole,
  through `EndpointModule`: every endpoint has a role rule and a name of its own, and only the registration of a company is outside
  `/companies/{companyId}`. A new module gets the three rules by naming its `Map<Module>()` in one more test class.

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

### Branch endpoints (5.5b)

All under `/companies/{companyId}/branches`. The branch endpoints are for the company's own staff: the platform administrator has no
branch (RN11), so it is in none of them (on a route with a `branchId` it hears `Branch.AccessDenied`, on the others `User.RoleNotAllowed`).

| Method and route | Use case | Success | Roles |
|---|---|---|---|
| `GET /branches?isActive=true` | `ListBranchesQuery` | 200, array of summaries | CompanyAdmin, Receptionist, Professional |
| `POST /branches` | `CreateBranchCommand` | 201, `Location: /companies/{companyId}/branches/{id}` | CompanyAdmin |
| `GET /branches/{branchId}` | `GetBranchQuery` | 200 `BranchDto` | CompanyAdmin, Receptionist, Professional |
| `PUT /branches/{branchId}` | `UpdateBranchDetailsCommand` | 204 | CompanyAdmin |
| `POST /branches/{branchId}/activate` | `ActivateBranchCommand` | 204 | CompanyAdmin |
| `POST /branches/{branchId}/deactivate` | `DeactivateBranchCommand` | 204 | CompanyAdmin |
| `PUT /branches/{branchId}/business-hours/{day}` | `SetBranchBusinessHoursCommand` | 204 | CompanyAdmin |
| `DELETE /branches/{branchId}/business-hours/{day}` | `RemoveBranchBusinessHoursCommand` | 204 | CompanyAdmin |

- **The id of the route wins.** `branchId` and `day` come from the route. The body of `PUT /branches/{branchId}` has only `name`, `address`,
  `phone` and `timeZoneId` (an `branchId` in the body is ignored), and the body of `PUT .../business-hours/{day}` has `start`, `end` and
  `breaks` (a missing list is no breaks). Where the command has no id (`POST /branches`) the command *is* the body.
- **`{day}` is the name of a weekday** (`Monday`...`Sunday`). A name that does not exist is a 400 `Http.BadRequest`; a number outside 0 to 6
  is a 400 `Validation.Failed` on the field `Day`. Times are `HH:mm:ss` (`"09:00:00"`).
- **The list respects the branches of the user (RN11).** A staff member sees only the branches of its list (a new user has none until
  someone gives it access); a customer, who books in any branch, sees them all. The branch of a route (`/branches/{branchId}`) is
  checked before the endpoint, see `api-tenancy.md`.
- A company administrator whose list is restricted cannot work in a branch it just created until its list is changed.

### Professional endpoints (5.5c)

All under `/companies/{companyId}/professionals`. Like the branches, they are for the company's own staff: the platform administrator is in none.

| Method and route | Use case | Success | Roles |
|---|---|---|---|
| `GET /professionals?isActive=true` | `ListProfessionalsQuery` | 200, array of summaries | company roles (administrator, receptionist, professional) |
| `POST /professionals` | `RegisterProfessionalCommand` | 201, `Location: /companies/{companyId}/professionals/{id}` | CompanyAdmin |
| `GET /professionals/{professionalId}` | `GetProfessionalQuery` | 200 `ProfessionalDto` (hours, time offs, services) | company roles |
| `PUT /professionals/{professionalId}` | `UpdateProfessionalDetailsCommand` | 204 | CompanyAdmin |
| `POST /professionals/{professionalId}/activate` and `/deactivate` | `Activate/DeactivateProfessionalCommand` | 204 | CompanyAdmin |
| `PUT /professionals/{professionalId}/branches/{branchId}/working-hours/{day}` | `SetProfessionalWorkingHoursCommand` | 204 | CompanyAdmin, Receptionist |
| `DELETE` the same route | `RemoveProfessionalWorkingHoursCommand` | 204 | CompanyAdmin, Receptionist |
| `POST /professionals/{professionalId}/time-offs` | `AddProfessionalTimeOffCommand` | 201, body `{ "id" }`, `Location` of the professional | CompanyAdmin, Receptionist |
| `DELETE /professionals/{professionalId}/time-offs/{timeOffId}` | `RemoveProfessionalTimeOffCommand` | 204 | CompanyAdmin, Receptionist |
| `PUT /professionals/{professionalId}/offerings/{serviceOfferingId}` | `EnableProfessionalOfferingCommand` | 204 | CompanyAdmin |
| `DELETE` the same route | `DisableProfessionalOfferingCommand` | 204 | CompanyAdmin |
| `PUT /professionals/{professionalId}/user` | `LinkProfessionalUserCommand` (body `{ "userId" }`) | 204 | CompanyAdmin |
| `DELETE /professionals/{professionalId}/user` | `UnlinkProfessionalUserCommand` | 204 | CompanyAdmin |

- **Every id comes from the route.** The bodies have no id of the professional, the branch, the day, the time off or the service (one sent anyway is ignored). The
  only id in a body is the `userId` of the link, because the user is the thing being linked.
- **The working hours carry `branchId` in the route on purpose.** A route with a `branchId` is checked against the branches of the user (RN11) before the endpoint,
  so a receptionist restricted to branch A cannot set the hours of a professional at branch B, with no check inside the use case. The same holds for removing them.
- **Who may do what.** The agenda (working hours and time off) is also for the receptionist; what the professional *is* (registration, services, link to a user,
  activation) is for the administrator. A user with the role `Professional` only reads: there is no rule yet that tells "its own" professional, so it is not allowed
  to change anyone's time off.
- **Time off** takes UTC instants (`"2026-12-01T09:00:00Z"`); an instant that is not UTC is refused by the Domain (422). There is no address for a single time off, so the
  `Location` of the 201 is the professional, where it is read (`timeOffs`).
- **A service is enabled with a body**, `{}` for the standard duration or `{ "customDurationMinutes": 45 }`; enabling it again changes the duration.
- `{day}` works as in the branches: the name of a weekday, 400 for anything else.

### Customer endpoints (5.5d)

All under `/companies/{companyId}/customers`. A customer is personal data (CPF, birth date, contacts), so these are for the counter and the
administration: **CompanyAdmin and Receptionist**. A professional sees the customer inside an appointment, not in the register, and the platform
administrator has no company to work in.

| Method and route | Use case | Success |
|---|---|---|
| `GET /customers?search=&page=&pageSize=` | `ListCustomersQuery` | 200 `{ items, page, pageSize, totalCount, totalPages }` |
| `POST /customers` | `RegisterCustomerCommand` | 201, `Location: /companies/{companyId}/customers/{id}` |
| `GET /customers/{customerId}` | `GetCustomerQuery` | 200 `CustomerDto` (with CPF and birth date) |
| `PUT /customers/{customerId}/contact` (body `{ email, phone }`) | `UpdateCustomerContactCommand` | 204 |
| `PUT /customers/{customerId}/profile` (body `{ name, cpf, birthDate, preferredLanguage }`) | `UpdateCustomerProfileCommand` | 204 |
| `PUT /customers/{customerId}/marketing-consent` (body `{ acceptsMarketing }`) | `ChangeCustomerMarketingConsentCommand` | 204 |

- **The list has no CPF and no birth date**: a list does not need personal data it does not show. `search` finds the name by a piece of it and the
  e-mail or the phone (E.164, `+5511999990000`, so the `+` is written `%2B` in a query string) by the whole value. `page` starts at 1, `pageSize` is
  at most 100; a value out of range is a 400 `Validation.Failed`, a value that is not a number is a 400 `Http.BadRequest`.
- **`PUT` replaces the whole thing.** The contact: a blank one is removed, but at least one must remain (a Domain rule, 422). The profile: a field left
  out is cleared.
- **The consent must be said.** A missing `acceptsMarketing` is a 400 `Validation.Failed` on the field `AcceptsMarketing`, not a withdrawal (see
  "A field the body must say").
- **There is no delete.** Erasing the data of a customer (LGPD) is its own use case, later.
- The query string of the list carries a name, an e-mail or a phone. The Api never logs a query string, but a proxy in front of it may.

### User endpoints (5.5d)

All under `/companies/{companyId}/users`, **CompanyAdmin only**: the e-mails of the team and who may do what are not for the rest of the staff.

| Method and route | Use case | Success |
|---|---|---|
| `GET /users?role=Receptionist&isActive=true` | `ListUsersQuery` | 200, array of summaries |
| `POST /users` | `CreateUserCommand` (through `CreateUserBody`) | 201, `Location: /companies/{companyId}/users/{id}` |
| `GET /users/{userId}` | `GetUserQuery` | 200 `UserDto` (with the branches it sees) |
| `PUT /users/{userId}` (body `{ name, email, preferredLanguage }`) | `UpdateUserDetailsCommand` | 204 |
| `PUT /users/{userId}/role` (body `{ role }`) | `ChangeUserRoleCommand` | 204 |
| `PUT /users/{userId}/branch-access` (body `{ accessAllBranches, branchIds }`) | `SetUserBranchAccessCommand` | 204 |
| `POST /users/{userId}/activate` and `/deactivate` | `Activate/DeactivateUserCommand` | 204 |

- **Creating a user creates the login account** at the identity provider and then the user, with its branch access, in one request. A new user sees **no** branch until it is given the access, so `accessAllBranches` or `branchIds` are part of the
  creation. A user with no access cannot work in any branch.
- **The role is a name** (`"Receptionist"`), and the body must say it: see below. The role `PlatformAdmin` cannot be given (422 from the Domain).
- **The branch access replaces the whole access**: every branch, or exactly the list (none, if empty).
- **The last administrator of a company** cannot be deactivated or lose its role (409 `User.LastAdministrator`).
- `role` in the query string works as in the body, by name; a number is a 400 `Validation.Failed` on the field `Role`.

## Catalog endpoints (5.6a)

The services of a company and their categories. Everything is under `/companies/{companyId}`, and the whole staff of the company (administrator,
receptionist, professional) **reads** the catalog, because it is what they offer and book. What the catalog is made of (create, change, activate)
is for the **CompanyAdmin**. The platform administrator is in none. The customer reads the catalog with its own login (5.8).

### Service categories

| Method and route | Use case | Success | Roles |
|---|---|---|---|
| `GET /service-categories` | `ListServiceCategoriesQuery` | 200, array ordered by name | company roles |
| `POST /service-categories` | `CreateServiceCategoryCommand` | 201, body `{ "id" }`, `Location` is the list | CompanyAdmin |
| `PUT /service-categories/{serviceCategoryId}` (body `{ name }`) | `RenameServiceCategoryCommand` | 204 | CompanyAdmin |

A category is read inside the list (there is no read of a single one), so the `Location` of the 201 is the list, as with the time offs of a professional.
There is no delete: a category with services would leave them without it.

### Services

| Method and route | Use case | Success | Roles |
|---|---|---|---|
| `GET /service-offerings?search=&categoryId=&isActive=&page=&pageSize=` | `ListServiceOfferingsQuery` | 200 page of summaries (with the category name) | company roles |
| `POST /service-offerings` | `CreateServiceOfferingCommand` | 201, `Location: /companies/{companyId}/service-offerings/{id}` | CompanyAdmin |
| `GET /service-offerings/{serviceOfferingId}` | `GetServiceOfferingQuery` | 200 `ServiceOfferingDto` | company roles |
| `PUT /service-offerings/{serviceOfferingId}` (body `{ name, durationMinutes, description, categoryId }`) | `UpdateServiceOfferingDetailsCommand` | 204 | CompanyAdmin |
| `PUT /service-offerings/{serviceOfferingId}/pricing` (body `{ type, basePrice }`) | `ChangeServiceOfferingPricingCommand` | 204 | CompanyAdmin |
| `POST /service-offerings/{serviceOfferingId}/activate` and `/deactivate` | `Activate/DeactivateServiceOfferingCommand` | 204 | CompanyAdmin |

- **Pricing.** `type` is `Fixed` (with a `basePrice`) or `OnRequest` (with none: the professional sets the price when confirming, RN18). The price is a JSON
  number with at most two decimals and arrives as a `decimal`, so `45.90` is exactly 45.90. `Fixed` is the first member of the enum: a pricing with no
  `type` and no `basePrice` is refused by the validator (400 on `BasePrice`), so there is no silent default.
- **`PUT` of the details replaces them**: a `categoryId` left out takes the service out of its category. A category that is not in the company is a 404
  `ServiceCategory.NotFound`.
- **Nothing is deleted.** Deactivating is how a service goes away; the professionals and promotions that cite it are not touched.
- The ids of the services are what `PUT /professionals/{professionalId}/offerings/{serviceOfferingId}` takes, so a service must exist before a professional
  can perform it.
- The list works like the one of the customers: `page` from 1, `pageSize` at most 100, `search` by a piece of the name, `categoryId` and `isActive` narrow it.

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

The `field` of an error is the **name of the property of the command**, in PascalCase (`Email`, `Name`, `Day`), as the validators write it. The
endpoints name the fields they refuse by themselves with `nameof` of that property, so a client sees one convention.

## A field the body must say

For most fields, "left out" and "null" mean the same and the use case decides what that is (no specialty, no breaks). For a few, a missing value
**must not** become the default of the type, because the default means something else:

| Field | What a missing value would have been | Answer instead |
|---|---|---|
| `role` (create a user, change the role) | `PlatformAdmin`, the first member of the enum: the one role nobody may be given, discovered after the account was created at the identity provider | 400 `Validation.Failed`, field `Role`, code `Validation.Required` |
| `acceptsMarketing` | `false`: a consent withdrawn that nobody withdrew | the same, field `AcceptsMarketing` |
| `accessAllBranches` | `false` and an empty list: a user locked out of every branch | the same, field `AccessAllBranches` |

For these the body type of the Api has a nullable (`UserRole?`, `bool?`), and the endpoint answers the 400 before sending anything. `FieldProblems`
(in `Shared/`) builds it. The use case still sees a plain value.

## Trying the Api

The collection of Bruno in `backend/requests/` has every endpoint of the Api. It uses only the development values, no real credential.

1. Start MySQL, RabbitMQ and Keycloak (`infra`), apply the migrations and run the Api (see the README).
2. Open the folder `backend/requests` as a collection in Bruno and choose the environment **Development**.
3. Run **Get token (platform admin)** or **Get token (company admin)**: the two users of the realm of development. It saves the token in
   the variable `token`, which every other request sends as a Bearer.
4. Run **Me**. The platform administrator (`admin@planelyapp.local`) answers with no `companyId`; the company administrator
   (`companyadmin@planelyapp.local`) answers with the id of the company of development, and **Me** saves it in `companyId`. The Api created
   both at startup (see `api-authentication.md`).
5. As the platform administrator, **Register company** saves the id of a new company in `companyId`. The endpoints of a company work
   for the administrator of that company, so to try the branches and the rest, sign in as the **company administrator** and run **Me**
   first, so that `companyId` is the company of that user.

The company endpoints are split by who may call them (see the tables above): the platform administrator registers and activates, the company
administrator changes its own company, its branches, its professionals (5.5c), its customers and its users (5.5d), and from 5.6a its categories and services.

As the company administrator, **Register customer** and **Create user** save `customerId` and `userId`, which the other requests of their folder use
(and **Link user**, of the professionals). **Create user** creates a real account in the Keycloak of development, with the e-mail of the body; run it
once, or change the e-mail, or the second call answers 409.

The token lives for minutes: when a request answers 401, run **Get token** again.
