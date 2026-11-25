# Authentication of the staff

The staff (users of a company and the platform administrator) signs in at Keycloak and sends the access token to the Api in
`Authorization: Bearer <token>`. The Api never sees a password. The customer has another way in (a code sent to the customer,
phase 5.8) and another token; this document is only about the staff.

## What happens to a request

1. **The token is validated** by the JwtBearer scheme `Keycloak`: the signature (the public keys come from the realm), the issuer, the
   audience, the expiry and the not-before. Anything else is a 401. A token signed with a shared secret
   instead of one of the keys the realm publishes is refused (there is a test for it).
2. **The fallback policy** asks for an authenticated user on every endpoint that does not say otherwise. A new endpoint is
   therefore closed until someone opens it with `AllowAnonymous()` (the liveness check, and the documentation in Development, do).
3. **The user is looked up** by the `sub` of the token (the Keycloak subject, stored as `User.IdentityId`) through the use case
   `ResolveStaffActorQuery`. The answer fills `CurrentUser`, which is the `ICurrentUser` of every use case in that request.
4. The endpoint runs. Use cases read who is acting from `ICurrentUser` and never from the token.

Steps 3 and 4 do not run for an endpoint that allows anonymous access, even when a token comes with the request.

## What comes from the token and what does not

| Information | Source |
|---|---|
| Who the person is (`sub`) | The token |
| The id of the `User`, the role, the company | The database, on every request |
| The branches the user may see (RN11) | The database, by the use cases that need them |

The role and the company are not in the token on purpose. A token lives for minutes and cannot be taken back; if the role were
in it, changing a role or deactivating a user would only work after the token expired. Reading the user on each request costs one
indexed query and makes both changes work on the next request.

## Answers

| Situation | Status and code |
|---|---|
| No `Authorization` header | 401 `Http.Unauthorized`, `WWW-Authenticate: Bearer` |
| Token expired, from another realm, for another audience, signed by another key, or not a token | 401 `Http.Unauthorized` |
| Valid token with no `sub` | 401 `Http.Unauthorized` |
| A route that does not exist, with no token | 401 `Http.Unauthorized` (the fallback policy also covers it, so an outsider cannot tell which routes exist) |
| A route that does not exist, with a valid token | 404 `Http.NotFound` (the user is not looked up) |
| Valid token, but no user has this subject | 403 `User.NotRegistered` |
| Valid token of a deactivated user | 403 `User.Inactive` |

The code of the Api never logs the token, the `Authorization` header or the claims; the request log has the status and the correlation id.

## Configuration

The Api reads the section `Keycloak` it already needs for the account of the users:

| Key | Use in authentication |
|---|---|
| `BaseUrl`, `Realm` | The issuer and the authority are `BaseUrl/realms/Realm`. The token must carry exactly this `iss`, so the URL the people use to reach Keycloak must be the one in `BaseUrl` |
| `ClientId` | The audience (`aud`) the token must carry: `planelyapp-api` |

In Development the metadata is read over HTTP. Everywhere else HTTPS is required.

## The realm of development

`infra/keycloak/planelyapp-realm.json` has, besides the service client of the Api:

- The client `planelyapp-panel`: public, for the Angular panel (authorization code with PKCE) and, **in development only**, for the
  direct login with a password, which makes a token easy to get without a browser. The login with a password is not used in production.
- A mapper on that client that puts `planelyapp-api` in the audience of the access token. Without it Keycloak issues the token for
  `account` and the Api refuses it.
- A user `admin@planelyapp.local` (password `admin123`, development only), with a fixed id so that the first platform administrator
  can be registered from it (see "The first platform administrator" below).

Keycloak imports the realm only when it does not exist yet. After changing the file, remove the volume of Keycloak and start it again:

```powershell
cd infra
docker compose rm -sf keycloak
docker volume rm infra_keycloak-data
docker compose up -d keycloak
```

(The name of the volume is the folder of the compose file, `infra`, and `keycloak-data`; `docker volume ls` shows it.)

## Getting a token in development

```powershell
$form = @{
  grant_type = "password"
  client_id  = "planelyapp-panel"
  username   = "admin@planelyapp.local"
  password   = "admin123"
}
$token = (Invoke-RestMethod -Method Post -Uri "http://localhost:8080/realms/planelyapp/protocol/openid-connect/token" -Body $form).access_token
Invoke-RestMethod -Uri "http://localhost:5062/me" -Headers @{ Authorization = "Bearer $token" }
```

Until a user exists in the database with `IdentityId` equal to the `sub` of this token, `/me` answers 403 `User.NotRegistered`. That is
the proof that the token was accepted and the lookup ran.

## The first platform administrator

Keycloak knows the person, but the Api only lets in who is also a `User` of the platform. In Development the Api creates the
platform administrator when it starts: the section `DevelopmentSeed` of `appsettings.Development.json` has the id of the realm user
(the `sub` of the token), a name and an e-mail, and the Api makes a `User` with the role `PlatformAdmin` and no company if none has
that id yet. Running it again changes nothing.

- It runs **only in Development**; the code is not even registered elsewhere. In production the first administrator is a deliberate act.
- It needs the schema (step 4 of the README). If the database is not ready the Api logs a warning (the type of the exception, nothing
  else) and goes on; migrate and start it again.
- From then on `GET /me` with the token of `admin@planelyapp.local` answers 200 with the role `PlatformAdmin`.

## `GET /me`

Who is acting, as the Api sees it: `{ "id", "kind", "role", "companyId" }`. `kind` and `role` are written as names (`"Staff"`,
`"PlatformAdmin"`), like every enum of the Api (see `api-endpoints.md`). `companyId` is null for the platform administrator. The panel
calls it right after the login to know what to show. Any signed-in staff member may call it.

## Not here yet

- The company and the branch of the route against the user: see `api-tenancy.md`.
- The customer token and the second scheme: 5.8.
- Roles on the endpoints (who may call what): `api-endpoints.md`.
