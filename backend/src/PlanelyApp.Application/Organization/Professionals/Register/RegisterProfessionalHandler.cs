using PlanelyApp.Application.Abstractions.Cqrs;
using PlanelyApp.Application.Abstractions.Results;
using PlanelyApp.Application.Abstractions.Tenancy;
using PlanelyApp.Domain.Organization.Professionals;
using PlanelyApp.Domain.SeedWork;

namespace PlanelyApp.Application.Organization.Professionals.Register;

internal sealed class RegisterProfessionalHandler(IProfessionalRepository professionals, ITenantProvider tenantProvider)
    : ICommandHandler<RegisterProfessionalCommand, Guid>
{
    public Task<Result<Guid>> HandleAsync(RegisterProfessionalCommand command, CancellationToken cancellationToken)
    {
        var professional = Professional.Create(tenantProvider.RequireCompanyId(), command.Name, command.Specialty);

        professionals.Add(professional);

        return Task.FromResult<Result<Guid>>(professional.Id);
    }
}