using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Domain.Organization.Branches;
using PlanelyApp.Domain.Organization.Customers;
using PlanelyApp.Domain.Organization.Users;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.IntegrationTests.Organization;

/// <summary>Valid users, customers and contacts, unique on every call because the database is shared by all tests.</summary>
internal static class StaffData
{
    public static string NewEmail() => $"person{Guid.NewGuid():N}@example.com";

    public static Phone NewPhone() => Phone.Create($"+55119{Random.Shared.Next(10_000_000, 100_000_000)}");

    public static Cpf NewCpf()
    {
        var digits = new char[11];

        for (var i = 0; i < 9; i++)
        {
            digits[i] = (char)('0' + Random.Shared.Next(0, 10));
        }

        digits[8] = digits[0] == '1' ? '2' : '1'; // never all the same digit, which is invalid
        digits[9] = (char)('0' + CheckDigit(digits.AsSpan(0, 9), 10));
        digits[10] = (char)('0' + CheckDigit(digits.AsSpan(0, 10), 11));

        return Cpf.Create(new string(digits));
    }

    public static User NewUser(Guid companyId, string? email = null, UserRole role = UserRole.Receptionist) =>
        User.CreateForCompany(
            companyId,
            $"sub-{Guid.NewGuid():N}",
            "Maria Atendente",
            Email.Create(email ?? NewEmail()),
            role,
            Language.English);

    public static Customer NewCustomer(Guid companyId, Email? email = null, Phone? phone = null) =>
        Customer.Register(
            companyId,
            email ?? Email.Create(NewEmail()),
            phone ?? NewPhone(),
            "João Cliente",
            acceptsMarketing: true,
            preferredLanguage: Language.Spanish);

    public static async Task<Branch> SeedBranchAsync(MySqlFixture mysql, Guid companyId, CancellationToken cancellationToken)
    {
        var branch = OrganizationData.NewBranch(companyId, $"Filial {Guid.NewGuid():N}");

        await SaveAsync(mysql, companyId, cancellationToken,
            services => services.GetRequiredService<IBranchRepository>().Add(branch));

        return branch;
    }

    /// <summary>Runs <paramref name="add"/> in a new scope of the given company (or none) and saves it.</summary>
    public static async Task SaveAsync(
        MySqlFixture mysql,
        Guid? companyId,
        CancellationToken cancellationToken,
        Action<IServiceProvider> add)
    {
        await using var scope = mysql.CreateScope(companyId);

        add(scope.ServiceProvider);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken);
    }

    private static int CheckDigit(ReadOnlySpan<char> digits, int firstWeight)
    {
        var sum = 0;

        for (var i = 0; i < digits.Length; i++)
        {
            sum += (digits[i] - '0') * (firstWeight - i);
        }

        var remainder = sum % 11;

        return remainder < 2 ? 0 : 11 - remainder;
    }
}