# Message contracts

What the monolith publishes through the outbox. A consumer (a notification service, an integration, a report) depends on this file, so a change here is a change of contract.

## Envelope

Every message is published as JSON with the same envelope. The `type` is also the RabbitMQ routing key; the exchange comes from the subject (the part before the dot).

```json
{
  "messageId": "0199...",
  "type": "appointment.confirmed",
  "version": 1,
  "occurredAt": "2027-11-22T13:00:00Z",
  "companyId": "0199...",
  "data": { }
}
```

| Field | Meaning |
|---|---|
| `messageId` | Id of the outbox row. Stays the same when the message is published again, so a consumer can ignore a duplicate. Delivery is at least once. |
| `type` | `subject.what_happened`, in snake_case. |
| `version` | Version of `data`. A field is only added inside the same version; anything else (remove, rename, change meaning) is a new version. |
| `occurredAt` | When it happened (UTC), not when it was published. |
| `companyId` | The tenant. It is not repeated inside `data`. |

## Rules of `data`

- **Thin:** ids, instants, status, total and who acted. A consumer that needs a name, a phone or an e-mail asks the Api by the id. No personal data and no free text travel in a message (LGPD).
- Property names in camelCase; instants in UTC (`...Z`); money as a number with two decimals; enums as strings (the name is part of the contract).
- A missing value is `null`, never an omitted property.

## exchange `scheduling.events`

| Type | Raised when | `data` |
|---|---|---|
| `appointment.created` | An appointment is created. `status` tells if it was born `Confirmed` or is `Pending`. | `appointmentId`, `branchId`, `customerId`, `professionalId`, `startUtc`, `status` |
| `appointment.confirmed` | A pending appointment is confirmed (every price is known). | `appointmentId`, `branchId`, `customerId`, `professionalId`, `startUtc`, `totalPrice` |
| `appointment.cancelled` | The customer or the company cancels. The reason is not sent. | `appointmentId`, `branchId`, `customerId`, `professionalId`, `startUtc`, `cancelledByCustomer` |
| `appointment.rescheduled` | The appointment moves to another time. The status does not change. | `appointmentId`, `branchId`, `customerId`, `professionalId`, `previousStartUtc`, `newStartUtc`, `rescheduledBy`, `rescheduledByCustomer` |
| `appointment.services_changed` | A service is added or removed. Carries the resulting period and total. `totalPrice` is `null` while a price is pending. | `appointmentId`, `branchId`, `customerId`, `professionalId`, `startUtc`, `endUtc`, `totalPrice`, `changedBy`, `changedByCustomer` |

`rescheduledBy` and `changedBy` are the id of the user (staff) or of the customer who acted; `...ByCustomer` tells which.

`appointment.services_changed` is defined, but no use case raises it yet.

## exchange `organization.events`

| Type | Raised when | `data` |
|---|---|---|
| `customer.registered` | A customer is registered. | `customerId` |
| `customer.updated` | The contacts, the profile or the marketing consent changed (nothing is sent when the new value is equal to the old one). The message does not say which one. | `customerId` |

A customer message carries only the id: no name, e-mail, phone, CPF, birth date or consent. A consumer reads the current customer from the Api by the id, and never keeps the marketing consent from an earlier read (LGPD: a withdrawal must take effect at once).

## exchange `catalog.events`

| Type | Raised when | `data` |
|---|---|---|
| `promotion.created` | A promotion is created. | `promotionId`, `appliesToAllBranches`, `branchIds` (empty when it applies to all branches: the consumer must target every branch of the company) |

Other changes of a promotion (discount, validity, services, branches, end) do not raise a message yet.

## Next

`appointment.reminder_due` (4.12c) is added here when it exists.
