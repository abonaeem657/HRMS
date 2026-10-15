using System.Net.Http.Json;
using System.Text.Json.Serialization;
using HRMS.Application.External;

namespace HRMS.Infrastructure.External;

public sealed class PublicHolidaysService(HttpClient httpClient) : IPublicHolidaysService
{
    public async Task<IReadOnlyList<PublicHolidayDto>> GetHolidaysAsync(
        int year,
        string countryCode = "JO",
        CancellationToken cancellationToken = default)
    {
        if (year is < 2000 or > 2100)
        {
            throw new ArgumentOutOfRangeException(nameof(year), "Holiday year must be between 2000 and 2100.");
        }
        if (countryCode.Length != 2 || !countryCode.All(char.IsAsciiLetter))
        {
            throw new ArgumentException("Country code must contain exactly two ASCII letters.", nameof(countryCode));
        }

        var response = await httpClient.GetFromJsonAsync<List<NagerHolidayResponse>>(
            $"PublicHolidays/{year}/{Uri.EscapeDataString(countryCode.ToUpperInvariant())}",
            cancellationToken);
        if (response is null)
        {
            throw new InvalidDataException("The public holidays API returned an empty response.");
        }

        return response.Select(item =>
        {
            if (!DateOnly.TryParseExact(
                    item.Date,
                    "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out var date) ||
                string.IsNullOrWhiteSpace(item.LocalName) ||
                string.IsNullOrWhiteSpace(item.Name) ||
                string.IsNullOrWhiteSpace(item.CountryCode))
            {
                throw new InvalidDataException("The public holidays API returned an invalid holiday item.");
            }
            return new PublicHolidayDto(date, item.LocalName, item.Name, item.CountryCode);
        }).ToList();
    }

    private sealed record NagerHolidayResponse(
        [property: JsonPropertyName("date")] string Date,
        [property: JsonPropertyName("localName")] string LocalName,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("countryCode")] string CountryCode);
}
