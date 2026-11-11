using System.Text.Json;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Organization.Customers.ChangeMarketingConsent;
using PlanelyApp.Application.Organization.Customers.Register;
using PlanelyApp.Application.Organization.Customers.UpdateContact;
using PlanelyApp.Application.Organization.Customers.UpdateProfile;
using PlanelyApp.IntegrationTests.Messaging;
using Shouldly;

namespace PlanelyApp.IntegrationTests.Organization;

/// <summary>What the customer use cases leave in the outbox: the id and nothing personal.</summary>
public class CustomerMessagesUseCaseTests(MySqlFixture mysql)
{
    [Fact]
    public async Task Registering_should_leave_a_message_with_only_the_id()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);

        var id = (await mysql.SendAsync(
            company.Id,
            new RegisterCustomerCommand("Ana Souza", "ana@example.com", "+5511911110001", true),
            cancellationToken)).Value;

        var message = (await OutboxData.ForCompanyAsync(mysql, company.Id, cancellationToken)).Single();
        message.Type.ShouldBe("customer.registered");
        message.Version.ShouldBe(1);
        message.ProcessedAtUtc.ShouldBeNull();
        using var data = JsonDocument.Parse(message.Payload);
        data.RootElement.EnumerateObject().Select(property => property.Name).ToList().ShouldBe(["customerId"], ignoreOrder: true);
        data.RootElement.GetProperty("customerId").GetGuid().ShouldBe(id);
        message.Payload.ShouldNotContain("ana@example.com");
        message.Payload.ShouldNotContain("Ana Souza");
        message.Payload.ShouldNotContain("911110001");
    }

    [Fact]
    public async Task Each_real_change_should_leave_an_updated_message()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var id = (await mysql.SendAsync(
            company.Id, new RegisterCustomerCommand("Ana Souza", "ana@example.com", "+5511911110001", true), cancellationToken)).Value;

        await mysql.SendAsync(
            company.Id, new UpdateCustomerContactCommand(id, "ana.new@example.com", "+5511911110001"), cancellationToken);
        await mysql.SendAsync(
            company.Id, new UpdateCustomerProfileCommand(id, "Ana S.", null, null, null), cancellationToken);
        await mysql.SendAsync(
            company.Id, new ChangeCustomerMarketingConsentCommand(id, false), cancellationToken);

        var messages = await OutboxData.ForCompanyAsync(mysql, company.Id, cancellationToken);
        messages.Select(message => message.Type).ToList().ShouldBe(
            ["customer.registered", "customer.updated", "customer.updated", "customer.updated"], ignoreOrder: false);
        foreach (var message in messages)
        {
            using var data = JsonDocument.Parse(message.Payload);
            data.RootElement.GetProperty("customerId").GetGuid().ShouldBe(id);
            message.Payload.ShouldNotContain("ana.new@example.com");
        }
    }

    [Fact]
    public async Task A_change_that_changes_nothing_should_leave_no_message()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        var id = (await mysql.SendAsync(
            company.Id, new RegisterCustomerCommand("Ana Souza", "ana@example.com", "+5511911110001", true), cancellationToken)).Value;

        await mysql.SendAsync(
            company.Id, new UpdateCustomerContactCommand(id, "ANA@example.com", "+5511911110001"), cancellationToken);
        await mysql.SendAsync(company.Id, new ChangeCustomerMarketingConsentCommand(id, true), cancellationToken);

        (await OutboxData.ForCompanyAsync(mysql, company.Id, cancellationToken)).Select(message => message.Type).ToList()
            .ShouldBe(["customer.registered"], ignoreOrder: false);
    }

    [Fact]
    public async Task A_refused_registration_should_leave_no_message()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var company = await OrganizationData.SeedCompanyAsync(mysql, cancellationToken);
        await mysql.SendAsync(
            company.Id, new RegisterCustomerCommand("Ana Souza", "ana@example.com", "+5511911110001"), cancellationToken);

        var duplicate = await mysql.SendAsync(
            company.Id, new RegisterCustomerCommand("Other", "ana@example.com", "+5511911110002"), cancellationToken);

        duplicate.Error.Code.ShouldBe("Customer.EmailAlreadyRegistered");
        (await OutboxData.ForCompanyAsync(mysql, company.Id, cancellationToken)).Count.ShouldBe(1);
    }
}