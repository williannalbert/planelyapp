namespace PlanelyApp.Domain.Organization.Professionals;

public interface IProfessionalRepository
{
    Task<Professional?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    void Add(Professional professional);
}