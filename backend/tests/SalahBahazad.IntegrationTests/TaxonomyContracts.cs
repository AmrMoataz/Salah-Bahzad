namespace SalahBahazad.IntegrationTests;

/// <summary>Loosely-typed mirrors of the taxonomy/reference API responses (kept separate from the DTOs).</summary>
public sealed record GradeResponse(Guid Id, string Name);

public sealed record SubjectResponse(Guid Id, string Name, int SpecializationCount);

public sealed record SpecializationResponse(Guid Id, string Name, Guid SubjectId, string SubjectName);

public sealed record CityResponse(Guid Id, string NameEn, string NameAr);

public sealed record RegionResponse(Guid Id, Guid CityId, string NameEn, string NameAr);
