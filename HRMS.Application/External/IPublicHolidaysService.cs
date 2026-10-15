namespace HRMS.Application.External;

public sealed record PublicHolidayDto(
    DateOnly Date,
    string LocalName,
    string Name,
    string CountryCode);

public interface IPublicHolidaysService
{
    Task<IReadOnlyList<PublicHolidayDto>> GetHolidaysAsync(
        int year,
        string countryCode = "JO",
        CancellationToken cancellationToken = default);
}

