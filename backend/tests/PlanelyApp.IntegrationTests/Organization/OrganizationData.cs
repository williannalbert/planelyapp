using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Domain.Organization.Branches;
using PlanelyApp.Domain.Organization.Companies;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.IntegrationTests.Organization;

/// <summary>Valid Domain objects for the tests. Values are deliberately not the defaults, to catch a column left unmapped.</summary>
internal static class OrganizationData
{
    private static readonly int[] FirstWeights = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
    private static readonly int[] SecondWeights = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

    public static Company NewCompany(string tradeName = "Barbearia Central", Cnpj? cnpj = null) =>
        Company.Create(
            tradeName,
            "Barbearia Central LTDA",
            cnpj ?? NewCnpj(),
            Email.Create("contato@barbearia.com.br"),
            Phone.Create("+5511999990000"),
            Language.Spanish,
            BookingPolicy.Create(4, 90, 12, 10, 45, true));

    public static Branch NewBranch(Guid companyId, string name = "Centro") =>
        Branch.Create(companyId, name, NewAddress(), Phone.Create("+551133334444"), "America/Sao_Paulo");

    public static Address NewAddress() =>
        Address.Create("Rua das Flores", "123", "Sala 4", "Centro", "São Paulo", "sp", "01310-100");

    public static DailySchedule WorkDay(DayOfWeek day, bool withBreaks = true)
    {
        var range = TimeOfDayRange.Create(new TimeOnly(9, 0), new TimeOnly(18, 0));

        Break[] breaks = withBreaks
            ?
            [
                Break.Create(TimeOfDayRange.Create(new TimeOnly(12, 0), new TimeOnly(13, 0)), "Lunch"),
                Break.Create(TimeOfDayRange.Create(new TimeOnly(15, 30), new TimeOnly(15, 45))),
            ]
            : [];

        return DailySchedule.Create(day, range, breaks);
    }

    /// <summary>A valid CNPJ nobody has used yet. Companies must be unique, and the database is shared by all tests.</summary>
    public static Cnpj NewCnpj()
    {
        var digits = new char[14];

        for (var i = 0; i < 12; i++)
        {
            digits[i] = (char)('0' + Random.Shared.Next(0, 10));
        }

        digits[11] = digits[0] == '1' ? '2' : '1'; // never all the same digit, which is invalid
        digits[12] = (char)('0' + CheckDigit(digits.AsSpan(0, 12), FirstWeights));
        digits[13] = (char)('0' + CheckDigit(digits.AsSpan(0, 13), SecondWeights));

        return Cnpj.Create(new string(digits));
    }

    public static async Task<Company> SeedCompanyAsync(MySqlFixture mysql, CancellationToken cancellationToken)
    {
        await using var scope = mysql.CreateScope();
        var company = NewCompany();

        scope.ServiceProvider.GetRequiredService<ICompanyRepository>().Add(company);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken);

        return company;
    }

    private static int CheckDigit(ReadOnlySpan<char> characters, int[] weights)
    {
        var sum = 0;

        for (var i = 0; i < characters.Length; i++)
        {
            sum += (characters[i] - '0') * weights[i];
        }

        var remainder = sum % 11;

        return remainder < 2 ? 0 : 11 - remainder;
    }
}