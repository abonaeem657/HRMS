using System.Net;
using HRMS.Infrastructure.External;
namespace HRMS.UnitTests;
public class ExternalApiTests
{
    private sealed class Handler(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>send(request,token); }
    private static PublicHolidaysService Service(string json,HttpStatusCode status=HttpStatusCode.OK)=>new(new HttpClient(new Handler((_,_)=>Task.FromResult(new HttpResponseMessage(status){Content=new StringContent(json)}))){BaseAddress=new Uri("https://example.test/")});
    [Fact] public async Task ValidResponseMapped() { var rows=await Service("[{\"date\":\"2026-01-01\",\"localName\":\"عطلة\",\"name\":\"Holiday\",\"countryCode\":\"JO\"}]").GetHolidaysAsync(2026);Assert.Single(rows); }
    [Fact] public async Task InvalidResponseRejected()=>await Assert.ThrowsAsync<InvalidDataException>(()=>Service("[{}]").GetHolidaysAsync(2026));
    [Fact] public async Task UnsuccessfulHttpRejected()=>await Assert.ThrowsAsync<HttpRequestException>(()=>Service("{}",HttpStatusCode.ServiceUnavailable).GetHolidaysAsync(2026));
    [Fact] public async Task InvalidJsonRejected()=>await Assert.ThrowsAsync<System.Text.Json.JsonException>(()=>Service("not json").GetHolidaysAsync(2026));
    [Fact] public async Task InvalidCountryRejected()=>await Assert.ThrowsAsync<ArgumentException>(()=>Service("[]").GetHolidaysAsync(2026,"../"));
    [Fact] public async Task CancellationPropagates()
    { using var source=new CancellationTokenSource(); source.Cancel(); var service=new PublicHolidaysService(new HttpClient(new Handler((_,t)=>Task.FromCanceled<HttpResponseMessage>(t))){BaseAddress=new Uri("https://example.test/")});await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>service.GetHolidaysAsync(2026,"JO",source.Token)); }
}
