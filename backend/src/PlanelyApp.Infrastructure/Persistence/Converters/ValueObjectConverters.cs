using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Infrastructure.Persistence.Converters;

// Each value object is stored as the text it holds. Reading goes through Create, so the Domain
// rules also apply to what comes back from the database.
internal sealed class EmailConverter() : ValueConverter<Email, string>(
    email => email.Value,
    value => Email.Create(value));

internal sealed class PhoneConverter() : ValueConverter<Phone, string>(
    phone => phone.Value,
    value => Phone.Create(value));

internal sealed class CnpjConverter() : ValueConverter<Cnpj, string>(
    cnpj => cnpj.Value,
    value => Cnpj.Create(value));

internal sealed class LanguageConverter() : ValueConverter<Language, string>(
    language => language.Code,
    value => Language.Create(value));

internal sealed class CpfConverter() : ValueConverter<Cpf, string>(
    cpf => cpf.Value,
    value => Cpf.Create(value));