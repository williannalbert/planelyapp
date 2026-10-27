# Domain error codes

The Domain never writes messages. It throws `DomainException(code, arguments)` with a stable code; the Api turns the code into a text in pt-BR, en or es (`.resx` key = code, arguments fill `{0}`, `{1}`...).

**Convention:** `<Type>.<Problem>` in PascalCase. The prefix is the type that owns the rule (or the module concept, for `Availability` and `PriceCalculator`). Instants that are not UTC always end in `TimeMustBeUtc`, a missing id usually in `Required` and a bad value usually in `Invalid`.

**Total:** 137 codes. Only the ones with a value in the Arguments column have a placeholder in the message.

## AccessCode

| Code | Arguments |
|---|---|
| `AccessCode.ChannelInvalid` |  |
| `AccessCode.CompanyRequired` |  |
| `AccessCode.ResendLimitReached` |  |
| `AccessCode.ResendTooSoon` |  |
| `AccessCode.TimeMustBeUtc` |  |

## Address

| Code | Arguments |
|---|---|
| `Address.CityInvalid` |  |
| `Address.ComplementInvalid` |  |
| `Address.DistrictInvalid` |  |
| `Address.NumberInvalid` |  |
| `Address.PostalCodeInvalid` |  |
| `Address.StateInvalid` |  |
| `Address.StreetInvalid` |  |

## Appointment

| Code | Arguments |
|---|---|
| `Appointment.ActorRequired` |  |
| `Appointment.BeyondBookingWindow` |  |
| `Appointment.BranchRequired` |  |
| `Appointment.BundleIncomplete` |  |
| `Appointment.BundleItemCannotBeAdded` |  |
| `Appointment.CancellationDeadlinePassed` |  |
| `Appointment.CompanyRequired` |  |
| `Appointment.CreatedByRequired` |  |
| `Appointment.CustomerRequired` |  |
| `Appointment.InvalidStatusTransition` | `Status, to` (current status, target status) |
| `Appointment.ItemDurationInvalid` |  |
| `Appointment.ItemHasFixedPrice` |  |
| `Appointment.ItemNotFound` |  |
| `Appointment.ItemsRequired` |  |
| `Appointment.MinimumNoticeNotMet` |  |
| `Appointment.NoShowBeforeStart` |  |
| `Appointment.NotEditable` | `Status` (current status) |
| `Appointment.NotTheCustomer` |  |
| `Appointment.NotesInvalid` |  |
| `Appointment.OnRequestItemRequiresPending` | `Status` (current status) |
| `Appointment.PriceCanOnlyBeSetWhilePending` | `Status` (current status) |
| `Appointment.PriceDoesNotMatchService` |  |
| `Appointment.PriceStillPending` |  |
| `Appointment.ProfessionalRequired` |  |
| `Appointment.ServiceFromAnotherCompany` |  |
| `Appointment.TimeMustBeUtc` |  |

## Availability

| Code | Arguments |
|---|---|
| `Availability.CompanyMismatch` |  |
| `Availability.LengthInvalid` |  |
| `Availability.PeriodNotAvailable` |  |
| `Availability.StepInvalid` |  |
| `Availability.TimeMustBeUtc` |  |

## BookingPolicy

| Code | Arguments |
|---|---|
| `BookingPolicy.CancellationDeadlineInvalid` |  |
| `BookingPolicy.IntervalInvalid` |  |
| `BookingPolicy.MaximumWindowInvalid` |  |
| `BookingPolicy.MinimumNoticeInvalid` |  |
| `BookingPolicy.TravelTimeInvalid` |  |
| `BookingPolicy.WindowSmallerThanNotice` |  |

## Branch

| Code | Arguments |
|---|---|
| `Branch.CompanyRequired` |  |
| `Branch.CustomPriceNotAllowed` |  |
| `Branch.NameInvalid` |  |
| `Branch.ServiceFromAnotherCompany` |  |
| `Branch.ServiceRequired` |  |
| `Branch.TimeMustBeUtc` |  |
| `Branch.TimeZoneInvalid` |  |

## Break

| Code | Arguments |
|---|---|
| `Break.DescriptionInvalid` |  |

## Cnpj

| Code | Arguments |
|---|---|
| `Cnpj.Invalid` |  |

## Company

| Code | Arguments |
|---|---|
| `Company.LegalNameInvalid` |  |
| `Company.TradeNameInvalid` |  |

## Cpf

| Code | Arguments |
|---|---|
| `Cpf.Invalid` |  |

## Customer

| Code | Arguments |
|---|---|
| `Customer.AccessTimeInvalid` |  |
| `Customer.BirthDateInvalid` |  |
| `Customer.CompanyRequired` |  |
| `Customer.ContactRequired` |  |
| `Customer.NameInvalid` |  |

## CustomerSession

| Code | Arguments |
|---|---|
| `CustomerSession.CompanyRequired` |  |
| `CustomerSession.CustomerRequired` |  |
| `CustomerSession.Expired` |  |
| `CustomerSession.LifetimeInvalid` |  |
| `CustomerSession.RefreshTokenRequired` |  |
| `CustomerSession.Revoked` |  |
| `CustomerSession.TimeMustBeUtc` |  |

## DailySchedule

| Code | Arguments |
|---|---|
| `DailySchedule.BreakOutsideRange` |  |
| `DailySchedule.BreaksOverlap` |  |
| `DailySchedule.DayInvalid` |  |

## DataImport

| Code | Arguments |
|---|---|
| `DataImport.CompanyRequired` |  |
| `DataImport.FailureReasonRequired` |  |
| `DataImport.FileNameInvalid` |  |
| `DataImport.InvalidStatusTransition` | `Status, to` (current status, target status) |
| `DataImport.NotAllRowsProcessed` | `ProcessedRows, TotalRows` (rows processed, total rows) |
| `DataImport.NotProcessing` | `Status` (current status) |
| `DataImport.RequestedByRequired` |  |
| `DataImport.StoragePathInvalid` |  |
| `DataImport.TimeMustBeUtc` |  |
| `DataImport.TooManyRowsProcessed` | `TotalRows` (total rows) |
| `DataImport.TotalRowsInvalid` |  |
| `DataImport.TypeInvalid` |  |

## DateTimeRange

| Code | Arguments |
|---|---|
| `DateTimeRange.EndMustBeAfterStart` |  |
| `DateTimeRange.TimeMustBeUtc` |  |

## Discount

| Code | Arguments |
|---|---|
| `Discount.FixedAmountInvalid` |  |
| `Discount.PercentageInvalid` | `percent` (the invalid percentage) |

## Email

| Code | Arguments |
|---|---|
| `Email.Invalid` |  |

## ImportError

| Code | Arguments |
|---|---|
| `ImportError.ReasonRequired` |  |
| `ImportError.RowInvalid` |  |

## Language

| Code | Arguments |
|---|---|
| `Language.NotSupported` | `code ?? string.Empty` (the unsupported language code) |

## Money

| Code | Arguments |
|---|---|
| `Money.NegativeAmount` |  |
| `Money.PercentageInvalid` | `percent` (the invalid percentage) |

## Phone

| Code | Arguments |
|---|---|
| `Phone.Invalid` |  |

## PriceCalculator

| Code | Arguments |
|---|---|
| `PriceCalculator.CompanyMismatch` |  |

## PriceQuote

| Code | Arguments |
|---|---|
| `PriceQuote.PromotionRequired` |  |

## Professional

| Code | Arguments |
|---|---|
| `Professional.CompanyRequired` |  |
| `Professional.NameInvalid` |  |
| `Professional.SpecialtyInvalid` |  |
| `Professional.TimeOffNotFound` |  |
| `Professional.UserInvalid` |  |
| `Professional.WorkingHoursConflict` |  |

## ProfessionalOffering

| Code | Arguments |
|---|---|
| `ProfessionalOffering.DurationInvalid` |  |
| `ProfessionalOffering.ServiceRequired` |  |

## Promotion

| Code | Arguments |
|---|---|
| `Promotion.BranchInvalid` |  |
| `Promotion.BranchesRequired` |  |
| `Promotion.BundleRequiresAtLeastTwoServices` |  |
| `Promotion.CompanyRequired` |  |
| `Promotion.DescriptionInvalid` |  |
| `Promotion.NameInvalid` |  |
| `Promotion.OnRequestServiceNotAllowed` |  |
| `Promotion.ServiceFromAnotherCompany` |  |
| `Promotion.ServicesRequired` |  |
| `Promotion.WeekdayInvalid` |  |

## ServiceCategory

| Code | Arguments |
|---|---|
| `ServiceCategory.CompanyRequired` |  |
| `ServiceCategory.NameInvalid` |  |

## ServiceOffering

| Code | Arguments |
|---|---|
| `ServiceOffering.CategoryInvalid` |  |
| `ServiceOffering.CompanyRequired` |  |
| `ServiceOffering.DescriptionInvalid` |  |
| `ServiceOffering.DurationInvalid` |  |
| `ServiceOffering.NameInvalid` |  |

## StatusChange

| Code | Arguments |
|---|---|
| `StatusChange.ReasonInvalid` |  |

## TimeOfDayRange

| Code | Arguments |
|---|---|
| `TimeOfDayRange.EndMustBeAfterStart` |  |

## TimeOff

| Code | Arguments |
|---|---|
| `TimeOff.ReasonInvalid` |  |

## User

| Code | Arguments |
|---|---|
| `User.BranchAccessNotApplicable` |  |
| `User.BranchInvalid` |  |
| `User.CompanyRequired` |  |
| `User.IdentityIdInvalid` |  |
| `User.NameInvalid` |  |
| `User.RoleInvalid` |  |

## ValidityPeriod

| Code | Arguments |
|---|---|
| `ValidityPeriod.EndBeforeStart` |  |

## WorkingHours

| Code | Arguments |
|---|---|
| `WorkingHours.BranchRequired` |  |
