using Microsoft.Extensions.DependencyInjection;
using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Domain.Organization.Users;

namespace PlanelyApp.IntegrationTests;

/// <summary>Runs a use case as a request would: one scope, with its own context and the company set.</summary>
internal static class UseCaseExtensions
{
    public static async Task<Result<TResponse>> SendAsync<TResponse>(
        this MySqlFixture mysql,
        Guid companyId,
        ICommand<TResponse> command,
        CancellationToken cancellationToken)
    {
        await using var scope = mysql.CreateScope(companyId);

        return await scope.ServiceProvider.GetRequiredService<IDispatcher>().SendAsync(command, cancellationToken);
    }

    /// <summary>The same, for a use case that looks at who is acting: a member of the staff.</summary>
    public static async Task<Result<TResponse>> SendAsStaffAsync<TResponse>(
        this MySqlFixture mysql,
        Guid companyId,
        Guid userId,
        ICommand<TResponse> command,
        CancellationToken cancellationToken)
    {
        await using var scope = mysql.CreateScope(companyId);
        scope.ServiceProvider.GetRequiredService<FakeCurrentUser>().UseStaff(userId, UserRole.Receptionist);

        return await scope.ServiceProvider.GetRequiredService<IDispatcher>().SendAsync(command, cancellationToken);
    }

    /// <summary>The same, for a customer.</summary>
    public static async Task<Result<TResponse>> SendAsCustomerAsync<TResponse>(
        this MySqlFixture mysql,
        Guid companyId,
        Guid customerId,
        ICommand<TResponse> command,
        CancellationToken cancellationToken)
    {
        await using var scope = mysql.CreateScope(companyId);
        scope.ServiceProvider.GetRequiredService<FakeCurrentUser>().UseCustomer(customerId);

        return await scope.ServiceProvider.GetRequiredService<IDispatcher>().SendAsync(command, cancellationToken);
    }

    public static async Task<Result<TResponse>> QueryAsync<TResponse>(
        this MySqlFixture mysql,
        Guid companyId,
        IQuery<TResponse> query,
        CancellationToken cancellationToken)
    {
        await using var scope = mysql.CreateScope(companyId);

        return await scope.ServiceProvider.GetRequiredService<IDispatcher>().QueryAsync(query, cancellationToken);
    }
    /// <summary>A query that looks at who is acting: a member of the staff.</summary>
    public static async Task<Result<TResponse>> QueryAsStaffAsync<TResponse>(
        this MySqlFixture mysql,
        Guid companyId,
        Guid userId,
        IQuery<TResponse> query,
        CancellationToken cancellationToken)
    {
        await using var scope = mysql.CreateScope(companyId);
        scope.ServiceProvider.GetRequiredService<FakeCurrentUser>().UseStaff(userId, UserRole.Receptionist);

        return await scope.ServiceProvider.GetRequiredService<IDispatcher>().QueryAsync(query, cancellationToken);
    }

    /// <summary>The same, for a customer.</summary>
    public static async Task<Result<TResponse>> QueryAsCustomerAsync<TResponse>(
        this MySqlFixture mysql,
        Guid companyId,
        Guid customerId,
        IQuery<TResponse> query,
        CancellationToken cancellationToken)
    {
        await using var scope = mysql.CreateScope(companyId);
        scope.ServiceProvider.GetRequiredService<FakeCurrentUser>().UseCustomer(customerId);

        return await scope.ServiceProvider.GetRequiredService<IDispatcher>().QueryAsync(query, cancellationToken);
    }
}