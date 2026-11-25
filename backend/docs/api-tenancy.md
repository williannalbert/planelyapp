# Tenant and branch of a request

Every route that works inside a company carries the id of the company: `/companies/{companyId}/...`. The Api takes the tenant
from there and from nowhere else (RN23): not from the token, not from the body, not from a header. A route that also has
`{branchId}` is checked against the branches of the staff member (RN11).

## What the middleware does

It runs after the authentication and after the user is known (`CurrentUser`), and before the endpoint:

1. A route with no `companyId` and no `branchId` goes through untouched. The request has no company; a use case that needs one
   fails loudly (see below).
2. A `companyId` or a `branchId` that is not an id (text, or the empty id) is a **404**. The routes already use the `:guid`
   constraint; this is the second lock.
3. If the endpoint asked for a signed-in user:
   - The company of the route must be the company of the user, or the user must be the platform administrator: otherwise **403
     `Company.AccessDenied`**.
   - For staff, a route with a `branchId` needs that branch in the list of the user: otherwise **403 `Branch.AccessDenied`**. The
     company is always checked first.
4. The company of the route becomes the tenant of the request (`TenantContext`), and from then on the data filter of the
   `DbContext` and the writes of every use case work inside that company.

An endpoint that allows anonymous access (the code for the customer to log in, in 5.8) skips step 3, because there is no user to
compare, but still gets the tenant of the route in step 4: the use case behind it needs to know which company the customer is in.

## Who may do what

| Who | Company routes | Branch routes |
|---|---|---|
| User of a company with every branch | Only its own company | Every branch |
| User of a company with a list of branches | Only its own company | Only the branches of the list |
| User of a company with an empty list | Only its own company | None |
| Platform administrator | Any company | None: it has no company, so no branch (the same answer as `User.CanAccess`) |

The list comes from the database with the user, once per request (`StaffActor.BranchIds`: no list means every branch, an empty
list means none; the empty list is never the same as "no list"). The platform administrator reaching a company is a
decision of the platform: it lets the company be looked after (see it, deactivate it). What it may do there is each endpoint's rule.

## Where the rule lives, and where it does not

- **Here (Api):** the route has the ids. The check costs no query and refuses before any use case.
- **In the use cases:** the appointment use cases still check the branch (`Branch.AccessDenied`) themselves, because the branch is
  not in their route: it comes in the body (create an appointment) or is found from the appointment. The same code, the same
  answer, two doors: never one without the other.
- **Not here:** who may call what by role (receptionist, professional...). Each endpoint says it, from 5.5.

## If a use case runs with no company

A use case that works inside a company reads it with `RequireCompanyId`. On a route with no `companyId` that call throws, and the
Api answers 500: it is a mistake in how the route was written (it should be under `/companies/{companyId}`), not something a
caller can fix. The tests of the routes catch it.

## The log

A refusal is written at Information level with the code, the id of the user and the ids of the route. No name, e-mail or token.
