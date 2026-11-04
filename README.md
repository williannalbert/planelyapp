# PlanelyApp

Multi-company scheduling platform, built as a study project: .NET 10 (modular monolith, Clean Architecture + DDD),
Angular, MySQL, RabbitMQ and Keycloak.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://www.docker.com/) (the database for development and for the integration tests)
- Git

## First-time setup

Some steps are command-line only and are required for the project to build, run and evolve. Run them in this order.

### 1. Start the infrastructure

    cd infra
    docker compose up -d

This starts MySQL 8.4 (port 3306), RabbitMQ (5672, management UI on 15672), Keycloak (8080) and Mailpit (SMTP 1025, UI on 8025).
Port 3306 must be free: stop any MySQL installed on the machine first.

On its first start Keycloak imports the realm `planelyapp` from `infra/keycloak/planelyapp-realm.json`: people log in with their
e-mail, the languages are pt-BR, en and es, e-mails go to Mailpit, and a service client (`planelyapp-api`) lets the Api manage
the accounts. Its secret in that file is a development-only value; any other environment sets its own. The administration
console is at http://localhost:8080 (user `admin`, password `admin`, also development-only).

### 2. Restore the .NET tools

    cd backend
    dotnet tool restore

The solution uses a **local tool** (`dotnet-ef`, pinned in `backend/dotnet-tools.json`) to create and apply database
migrations. Without this step the `dotnet ef` commands below do not exist.

### 3. Build and test

    cd backend
    dotnet build
    dotnet test

There are four kinds of tests, and none of them needs the containers from `infra/docker-compose.yml`:

- `PlanelyApp.Domain.Tests`: pure unit tests, in memory, no Docker.
- `PlanelyApp.Application.Tests`: the use cases with fake dependencies, in memory, no Docker.
- `PlanelyApp.ArchitectureTests`: read the compiled assemblies and check the dependencies between layers; they run nothing.
- `PlanelyApp.IntegrationTests`: talk to a **real** MySQL 8.4, RabbitMQ and Keycloak 26.4 (with Mailpit), but to disposable
  containers that Testcontainers starts on random ports and removes at the end (the migrations are applied to them, and the
  Keycloak realm is imported from `infra/keycloak/planelyapp-realm.json`, the same file the compose uses). **Docker must be
  running**; the development database is never touched. The fakes are the message publisher in the tests of the outbox
  processor, and an identity provider in memory in the tests of the use cases of the users.

### 4. Create the development database schema

The Api does not create tables by itself. Apply the migrations to the MySQL from step 1:

    cd backend/src/PlanelyApp.Infrastructure
    dotnet ef database update --connection "Server=localhost;Port=3306;Database=planelyapp;User ID=planelyapp;Password=planelyapp123;"

(These are the development-only credentials from `infra/docker-compose.yml`.)

### 5. Run the Api

    cd backend
    dotnet run --project src/PlanelyApp.Api

In Development the connection string comes from `appsettings.Development.json` (`ConnectionStrings:Default`).
The RabbitMQ connection string is `ConnectionStrings:RabbitMq` (an `amqp://` URI; outside Development use the environment
variable `ConnectionStrings__RabbitMq`). The Api also runs the outbox publisher in the background: it reads `outbox_messages`
and publishes to RabbitMQ. It needs the schema from step 4; RabbitMQ is only contacted when there is something to publish,
so the Api starts without it.
In any other environment set it with the environment variable `ConnectionStrings__Default`; never commit real credentials.

## Working with database migrations

Always run `dotnet ef` from the Infrastructure project folder, where the `DbContext` and the migrations live:

    cd backend/src/PlanelyApp.Infrastructure

| Goal | Command |
|---|---|
| Create a migration after changing a mapping | `dotnet ef migrations add <MigrationName> --output-dir Persistence/Migrations` |
| Undo the last migration (only if it was never applied anywhere) | `dotnet ef migrations remove` |
| Apply pending migrations to the development database | `dotnet ef database update --connection "<connection string>"` |
| List migrations | `dotnet ef migrations list` |

Rules:

- Build the solution first; `migrations add` compiles the project.
- Read the generated migration before committing it. It is the contract with the database.
- **Never edit a migration that has already been applied** to any database (including a teammate's). Create a new one.
- Commit the migration files together with the mapping change that required them.
- If the model changes without a new migration, the integration tests fail when they apply the migrations.
- Some indexes cannot be written in the model (EF Core 10 cannot index an owned type together with its owner, e.g. the period of an appointment). They are added by hand inside the migration that creates the table, with a comment saying so, and a schema test checks them. If you remove and recreate that migration, add them again.
- Rules of the Domain are repeated as `CHECK` constraints (`ToTable(..., t => t.HasCheckConstraint(...))`), as strong as the rule
  itself, with a name that starts with the table name (MySQL requires constraint names to be unique in the schema).
- Two things the schema tests enforce: names are lowercase `snake_case` (also in indexes written by hand) and every `datetime` is `datetime(6)`.

## Conventions

- Code (classes, methods, variables) in English. Error and success messages are translated to pt-BR, en and es.
- Database names are English `snake_case`, generated by convention (`Address.PostalCode` becomes `address_postal_code`).
  See `backend/src/PlanelyApp.Infrastructure/Persistence/Conventions`.
- Commits follow [Conventional Commits](https://www.conventionalcommits.org/).
- The catalog of error codes the Api must translate is in `backend/docs/error-codes.md`.
- A message for another system is added with `IOutbox` in the same unit of work as the change that caused it. Its type is
  `subject.what_happened` (`appointment.confirmed`); a new subject must be mapped to an exchange in `MessageTopology`.
- EF Core, MySqlConnector and RabbitMQ are used only in `PlanelyApp.Infrastructure`. Repositories and entity configurations are
  `internal sealed` and live in `Persistence/Repositories` and `Persistence/Configurations` (an architecture test checks it).
- A use case is a command (`ICommand<T>`, changes something) or a query (`IQuery<T>`, only reads) with its handler, called through
  `IDispatcher`. A handler returns `Result<T>` (`Unit` when there is nothing to return): an expected outcome the caller must handle
  (not found, forbidden, conflict) is an `Error` with a stable code and no text. A rule of the business that the Domain refuses is
  still a `DomainException`, which the Api turns into a 422. Time comes from `TimeProvider`, never from `DateTime.UtcNow`.
- Every handler is wrapped by decorators, from the outside in: log, validation, transaction, domain events, handler (a query has only
  the first two). So a **handler never calls `SaveChanges`**: the transaction decorator saves once, after a successful result, and the
  domain events of the aggregates are dispatched just before, so what their handlers leave in the outbox is saved in the same transaction.
- The format of a request is checked by a FluentValidation validator next to its handler (a failure is a 400 with a list of field and
  code). A default FluentValidation code becomes `Validation.Required`, `Validation.TooLong`, `Validation.OutOfRange` or `Validation.Invalid`;
  a code set on the rule is kept. The catalog is in `backend/docs/application-error-codes.md`.
- Logs carry the name of the request, the result and the time, never the content of the request (it may hold personal data).
- A use case is a folder with its command (or query), handler and validator: `Application/<Module>/<Aggregate>/<UseCase>/`.
  The company of a use case is the one of `ITenantProvider` (set by the Api from the route), not a field of the command; only the
  registration of a company has none. Who may call a use case is a role policy in the Api; handlers check only what depends on data.
- A validator checks the **shape** of the request (a missing field, a string a value object refuses, using `AcceptedBy(Email.Create)`).
  Limits and ranges stay in the Domain, which answers with the code of the field (`Company.TradeNameInvalid`, a 422).
- A query that lists reads through a port of the Application (`IBranchReadModel`), implemented in the Infrastructure with
  `AsNoTracking` and a projection to a summary DTO, so it never loads whole aggregates. A query for one item loads the aggregate with
  its repository and maps it to a DTO by hand. Read models are `internal sealed`, live in `Persistence/Queries` (an architecture test
  checks it) and still go through the tenant filter. A list that can grow large (customers) is paged: it takes `Page` (from 1) and
  `PageSize` (1 to 100) and answers a `PagedResult` with the items, the page and the total, in a stable order (the id breaks ties).
- A name that must be unique inside a company (a branch name, a CNPJ) is checked by the handler through the repository
  (`ExistsByNameAsync`) and answered as a `Conflict`; the unique index in the database is the last line of defense.
- Data several use cases share (`AddressData`, `DailyScheduleData`) lives in `Application/Organization/Shared`, with the conversion
  to and from the Domain next to it. The `Id` of the thing a command changes (`BranchId`) is a field of the command; the company is not.
- Whatever changes a professional's agenda (working hours, time off, enabled services) raises `ScheduleVersion` inside the Domain, so a
  use case never touches it. Two simultaneous edits of the same professional make the second save fail with
  `DbUpdateConcurrencyException` (RN27), which the Api answers as a 409.
- When a use case can tell that something inside an aggregate does not exist (a time off to remove), it answers `NotFound` (404)
  before calling the Domain, which would throw the same code as a 422. The Domain still protects itself.
- An optional text field left blank counts as "not informed" (`OrNull(Email.Create)`); a text that is filled in must be accepted by
  the Domain value object (`AcceptedBy`, a 400 when it is not). Whether at least one contact is needed is a Domain rule (a 422).
- A rule that needs to see the other customers of the company (e-mail, phone and CPF are unique inside it, RN24) is checked by the
  handler through the repository and answered as a `Conflict`. Keeping what the customer already has (its own e-mail, its own CPF)
  is not a conflict. A command that edits a profile replaces all of it: a field left out is cleared.
- The `User` has no tenant filter (it is found by the Keycloak subject before the company is known). So its use cases never call the
  generic `GetByIdAsync`: they use the methods that take the company (`GetByIdInCompanyAsync`, `FindByEmailAsync`), and the user list
  receives the company as a parameter. A user of another company is simply `NotFound`.
- The login account lives in Keycloak behind the `IIdentityProvider` port (the Application does not know Keycloak). A use case that
  changes it does the Domain part first, so a refusal happens before anything outside the database changes, and calls the provider
  last. Creating an account that the Domain then refuses is undone by deleting it. The role and the branches stay in the database.
- A company always keeps at least one active administrator: changing the role of, or deactivating, the last one is a `Conflict`.
- `KeycloakIdentityProvider` (Infrastructure) is the only code that knows the Keycloak API. It logs in as the service client, keeps
  the token until it is about to expire, reads an account before changing it (Keycloak's update replaces what it is not sent), and
  turns "e-mail already used" into a `Conflict` result and everything else that goes wrong into an `IdentityProviderException`.
  A new account gets an e-mail to set its password; if that e-mail cannot be sent, the account is deleted again.
- The catalog (`Application/Catalog`) has categories and services. The price of a service comes as a `PricingData` block (type and
  base price): a fixed price service needs the price, an on-request one cannot have it, and the value must fit the column (`decimal(12,2)`:
  two decimals, ten digits), so it is refused as a 400 instead of being rounded without notice. The name of a category and of a service
  is unique inside the company (a `Conflict`). A service is never deleted, only deactivated; a category can only be created, renamed
  and listed. The list of services is paged, searchable by a piece of the name, filtered by category and by status, and shows the
  name of the category; the list of categories is not paged (a company has few).
- The services of a branch (`Application/Catalog/BranchOfferings`) change through the `Branch` aggregate: a price of its own (only for
  fixed price services, RN20) and whether the branch offers the service (RN14); clearing the price goes back to the base price. The
  list shows every active service of the company with the price of the branch, the price that applies and whether it is offered,
  paged and filtered. A service that is on request shows no price, even if the branch priced it back when it was fixed price.
- A promotion (`Application/Catalog/Promotions`) is created from the ids of its services and branches, which the handler checks in the
  company (a 404 when one is missing), and a `DiscountData` block (a percentage above 0 up to 100, or an amount). A per-service promotion
  and a bundle go through the same command (`Kind`); the Domain refuses a service on request (RN20), a bundle with fewer than two
  services and an end date before the start. "All branches" and a list of branches cannot come together. A promotion is only ended:
  there is no reactivating, a new one is created.
- The conditions of a promotion change one at a time: name and description, discount, validity period and weekdays (an empty list
  means every day). The kind of the promotion never changes. What a change does not touch stays as it was, and appointments already
  made keep their price.
- The scope of a promotion changes in three ways: all branches or a list of branches (checked in the company, with the same
  "all or a list, never both" shape of the creation), services included (the ones it already has are ignored) and a service removed.
  The Domain keeps at least one service in a promotion and two in a bundle.
- The price query (`Application/Catalog/Pricing`) answers, for a branch, a start time in UTC and the services of an appointment, the
  price of each service in the order asked, and the totals. It loads the active promotions whose period contains the day AT THE BRANCH
  (not the UTC day), and the Domain (`PriceCalculator`) applies the branch price, the best promotion per service and the bundles. A
  service on request has no price and stays out of the totals; each line also says whether the service is active and offered by the
  branch, so a screen can show the price and still block the booking.
- The free-slot query (`Application/Scheduling/Availability`) gathers what `AvailabilityCalculator` needs and returns, for a branch, a local
  day and the services asked, the start times (UTC) of each professional who works at the branch, is active and performs all the
  services (or of the one professional asked for). The length of a slot is the duration of the services, with the custom duration of
  each professional, plus the interval of the company policy. A service that is inactive or not offered by the branch is a 409.
  Appointments are loaded for the professionals in a window around the day (any branch, for the travel time), never the whole agenda.
- The create-appointment use case (`Application/Scheduling/Appointments`) is the one that brings it all together. It checks who is acting (a
  customer can only book for themselves; staff need access to the branch, RN11) and that the company, branch and professional are active,
  that the professional works at the branch and performs every service, and that the services are active and offered by the branch. It
  prices all the services together with the promotions in force on the local day of the branch, asks the Domain to create the
  appointment (notice, window, status) and checks that the whole period is free, with the travel time of other branches. The
  appointment and the new version of the professional agenda (RN27) go in the same transaction, so two bookings for the same time
  cannot both be saved: the second one is a 409.
- The appointment status use cases (`Application/Scheduling/Appointments`) are confirm, start, complete, mark as no-show and cancel.
  They share `AppointmentAccess`, which loads the appointment and checks who is acting: staff need access to the branch of the
  appointment (RN11), and a customer only sees their own (for anyone else it does not exist). The Domain decides whether the
  change is allowed (state machine, price still pending, deadline of the customer, no-show only after the start). Cancelling as
  a customer or as staff are two different Domain methods, chosen by who is acting; neither raises the schedule version, because
  freeing time cannot cause a double booking.
- Moving an appointment and setting the price of a service on request are the two changes that can need more room in the agenda.
  Both go through `AppointmentAgenda`, which checks the new period against the other appointments of the professional (leaving the
  appointment itself out, so that a move of a few minutes does not clash with its own old time) and raises the schedule version in
  the same transaction (RN27). A customer can move their own appointment until the deadline of the policy; the staff can move any
  appointment of the branches they can access. The price is not calculated again when the appointment moves. Only the staff set a
  price, and the agenda is checked only when the new duration makes the period longer.
- The agenda queries (`Application/Scheduling/Appointments`) list the appointments of a branch, of a professional or of a customer,
  one page at a time, through a single read model (`IAppointmentReadModel`) that takes a filter. The days of a branch are days at the
  branch, turned into UTC limits by the handler; the agenda of a professional, who can work in several branches, takes UTC instants.
  Staff only see the branches they can access (RN11), and a customer only sees their own appointments (for anyone else the customer
  does not exist). A range is limited to 31 days, so a request cannot ask for the whole history of a branch.
- A domain event becomes a message for other systems in an event handler of the Application (`<Event>OutboxHandler`), which adds it to
  `IOutbox` with the time of the event. A message is thin: ids, UTC instants, status, total and who acted, never a name, phone or
  e-mail, nor free text. Types, fields and versions are in `backend/docs/message-contracts.md`. The tests of the outbox run alone
  (no parallelism with the other collections), because every appointment now writes to the same table.
- Handlers, event handlers and validators are found by assembly scanning (Scrutor); they can be `internal sealed`.
- An entity that belongs to a company implements `ITenantEntity` and gets the query filter. The only exceptions are listed, with the
  reason, in `TenantModelTests`.

## Project layout

    backend/
      src/
        PlanelyApp.Domain/          entities, value objects and rules (no dependencies)
        PlanelyApp.Application/     use cases
        PlanelyApp.Infrastructure/  EF Core, MySQL, repositories, migrations
        PlanelyApp.Api/             HTTP entry point
      tests/
        PlanelyApp.Domain.Tests/
        PlanelyApp.Application.Tests/
        PlanelyApp.ArchitectureTests/
        PlanelyApp.IntegrationTests/
      docs/
    infra/
      docker-compose.yml
