using Microsoft.EntityFrameworkCore;
using PlanelyApp.Application.Abstractions.Paging;
using PlanelyApp.Application.Organization.Customers;
using PlanelyApp.Domain.Organization.Customers;
using PlanelyApp.Domain.SeedWork;
using PlanelyApp.Domain.SharedKernel;

namespace PlanelyApp.Infrastructure.Persistence.Queries;

/// <summary>Reads for lists: straight to a projection, nothing tracked. The tenant filter of the context still applies.</summary>
internal sealed class CustomerReadModel(PlanelyDbContext context) : ICustomerReadModel
{
    public async Task<PagedResult<CustomerSummaryDto>> ListAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var customers = context.Set<Customer>().AsNoTracking();

        if (search is not null)
        {
            // E-mail and phone are value objects with a converter, which the EF cannot search by a piece: they
            // match only as a whole. The cheap checks keep the Domain from throwing for every name typed.
            var email = search.Contains('@') ? TryCreate(Email.Create, search) : null;
            var phone = search.StartsWith('+') ? TryCreate(Phone.Create, search) : null;
            var hasEmail = email is not null;
            var hasPhone = phone is not null;

            customers = customers.Where(customer =>
                (customer.Name != null && customer.Name.Contains(search))
                || (hasEmail && customer.Email == email)
                || (hasPhone && customer.Phone == phone));
        }

        var totalCount = await customers.CountAsync(cancellationToken);

        var rows = await customers
            .OrderBy(customer => customer.Name == null)
            .ThenBy(customer => customer.Name)
            .ThenBy(customer => customer.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(customer => new
            {
                customer.Id,
                customer.Name,
                customer.Email,
                customer.Phone,
                customer.AcceptsMarketing,
                customer.LastAccessAt,
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(row => new CustomerSummaryDto(
                row.Id, row.Name, row.Email?.Value, row.Phone?.Value, row.AcceptsMarketing, row.LastAccessAt))
            .ToList();

        return new PagedResult<CustomerSummaryDto>(items, page, pageSize, totalCount);
    }

    private static T? TryCreate<T>(Func<string?, T> factory, string text)
        where T : class
    {
        try
        {
            return factory(text);
        }
        catch (DomainException)
        {
            return null;
        }
    }
}