using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Organization.Shared;
using PlanelyApp.Domain.Organization.Branches;

namespace PlanelyApp.Application.Organization.Branches.Get;

internal sealed class GetBranchHandler(IBranchRepository branches) : IQueryHandler<GetBranchQuery, BranchDto>
{
    public async Task<Result<BranchDto>> HandleAsync(GetBranchQuery query, CancellationToken cancellationToken)
    {
        var branch = await branches.GetByIdAsync(query.BranchId, cancellationToken);

        if (branch is null)
        {
            return Error.NotFound("Branch.NotFound");
        }

        return new BranchDto(
            branch.Id,
            branch.Name,
            AddressData.From(branch.Address),
            branch.Phone.Value,
            branch.TimeZoneId,
            branch.IsActive,
            // The order of a list read from the database is not guaranteed, so the weekdays are ordered here.
            branch.BusinessHours.OrderBy(schedule => schedule.Day).Select(DailyScheduleData.From).ToList());
    }
}