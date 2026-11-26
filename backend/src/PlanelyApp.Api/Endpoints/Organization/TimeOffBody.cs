namespace PlanelyApp.Api.Endpoints.Organization;

/// <summary>The body of <c>POST /professionals/{professionalId}/time-offs</c>. The instants are UTC (<c>2026-12-01T09:00:00Z</c>).</summary>
internal sealed record TimeOffBody(DateTime StartUtc, DateTime EndUtc, string? Reason = null);