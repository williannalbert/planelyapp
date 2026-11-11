using PlanelyApp.Application.Abstractions.Cqrs;

namespace PlanelyApp.Application.Organization.Professionals.Get;

public sealed record GetProfessionalQuery(Guid ProfessionalId) : IQuery<ProfessionalDto>;