using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Abstractions.Tenancy;
using PlanelyApp.Domain.Catalog.Promotions;
using PlanelyApp.Domain.Catalog.ServiceOfferings;
using PlanelyApp.Domain.Organization.Branches;
using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Application.Catalog.Promotions.Create;

internal sealed class CreatePromotionHandler(
    IPromotionRepository promotions,
    IServiceOfferingRepository services,
    IBranchRepository branches,
    ITenantProvider tenantProvider)
    : ICommandHandler<CreatePromotionCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(CreatePromotionCommand command, CancellationToken cancellationToken)
    {
        var companyId = tenantProvider.RequireCompanyId();

        // The Domain keeps ids, so that they exist in this company is checked here. The repositories already hide
        // what belongs to other companies, so a missing id and an id of another company look the same: not found.
        var serviceIds = command.ServiceIds.Distinct().ToList();
        var found = (await services.ListByIdsAsync(serviceIds, cancellationToken)).ToDictionary(service => service.Id);

        if (found.Count != serviceIds.Count)
        {
            return Error.NotFound("ServiceOffering.NotFound");
        }

        if (!command.AppliesToAllBranches)
        {
            // The validator guarantees a list here.
            var branchIds = command.BranchIds!.Distinct().ToList();

            if (await branches.CountByIdsAsync(branchIds, cancellationToken) != branchIds.Count)
            {
                return Error.NotFound("Branch.NotFound");
            }
        }

        var discount = command.Discount.ToDomain();
        var validity = ValidityPeriod.Create(command.ValidFrom, command.ValidUntil);

        // The store answers in its own order; the promotion keeps the order of the request.
        var requested = serviceIds.Select(id => found[id]).ToList();

        var promotion = (command.Kind, command.AppliesToAllBranches) switch
        {
            (PromotionKind.PerService, true) => Promotion.CreateForAllBranches(
                companyId, command.Name, discount, validity, requested, command.Description),
            (PromotionKind.PerService, false) => Promotion.CreateForBranches(
                companyId, command.Name, discount, validity, requested, command.BranchIds!, command.Description),
            (_, true) => Promotion.CreateBundleForAllBranches(
                companyId, command.Name, discount, validity, requested, command.Description),
            _ => Promotion.CreateBundleForBranches(
                companyId, command.Name, discount, validity, requested, command.BranchIds!, command.Description),
        };

        promotion.SetWeekdays(command.Weekdays ?? []);
        promotions.Add(promotion);

        return promotion.Id;
    }
}