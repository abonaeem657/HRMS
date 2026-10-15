using HRMS.Infrastructure;
using HRMS.Authorization;
using HRMS.Resources;
using Microsoft.AspNetCore.Authorization;
using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.RateLimiting; 
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// ResourcesPath must stay empty: SharedResource is in the HRMS.Resources namespace and the
// .resx files are under Resources/, so the resource base name is already HRMS.Resources.SharedResource.
// A non-empty ResourcesPath would double the "Resources" segment and break resource lookup.
builder.Services.AddLocalization(options => options.ResourcesPath = "");
builder.Services.AddControllersWithViews(options => { options.Filters.Add<HRMS.Infrastructure.PersistenceExceptionFilter>(); options.Filters.Add<AuditForbidFilter>(); })
    .AddViewLocalization()
    .AddDataAnnotationsLocalization(options =>
    {
        options.DataAnnotationLocalizerProvider = (_, factory) => factory.Create(typeof(SharedResource));
    });
builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context => new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(
        new Microsoft.AspNetCore.Mvc.ValidationProblemDetails(context.ModelState)
        {
            Status = 400,
            Title = HRMS.Application.Common.Messages.Get("ValidationFailedTitle")
        });
});
builder.Services.Configure<Microsoft.AspNetCore.Mvc.MvcOptions>(options =>
{
    options.ModelBindingMessageProvider.SetAttemptedValueIsInvalidAccessor((_, name) =>
        string.Format(CultureInfo.CurrentCulture, HRMS.Application.Common.Messages.Get("InvalidFieldValue"), name));
    options.ModelBindingMessageProvider.SetValueMustNotBeNullAccessor(_ => HRMS.Application.Common.Messages.Get("FieldRequired"));
});
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        // API and login endpoints keep their dedicated policies.
        if (context.Request.Path.StartsWithSegments("/api") ||
            (HttpMethods.IsPost(context.Request.Method) &&
             (string.Equals(context.Request.Path.Value, "/Account/Login", StringComparison.OrdinalIgnoreCase) ||
              string.Equals(context.Request.Path.Value, "/Account/Setup", StringComparison.OrdinalIgnoreCase))))
            return RateLimitPartition.GetNoLimiter("dedicated");
        var read = HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method);
        var actor = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter((read ? "web-read:" : "web-write:") + actor,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = read ? 120 : 30,
                Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true
            });
    });
    options.OnRejected = async (rejected, cancellationToken) =>
    {
        if (rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            rejected.HttpContext.Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        await Results.Json(new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Status = 429, Title = HRMS.Application.Common.Messages.Get("RateLimitTitle"),
                Detail = HRMS.Application.Common.Messages.Get("RateLimitDetail")
            }, statusCode: 429, contentType: "application/problem+json")
            .ExecuteAsync(rejected.HttpContext);
    };
    options.AddPolicy("login", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    options.AddPolicy("api", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.User.Identity?.IsAuthenticated == true
                ? "user:" + (context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                  ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown")
                : "ip:" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});
builder.Services.AddInfrastructure(builder.Configuration);
if (!builder.Environment.IsDevelopment())
    builder.Services.ConfigureApplicationCookie(options => options.Cookie.SecurePolicy = CookieSecurePolicy.Always);
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, AuditAuthorizationMiddlewareResultHandler>();

var app = builder.Build();
if (args.Length == 1 && args[0] == "--seed-organization-data")
{
    if (!app.Environment.IsDevelopment())
        throw new InvalidOperationException("Organization starter data is available only in Development.");
    await using var scope = app.Services.CreateAsyncScope();
    await HRMS.Infrastructure.Persistence.OrganizationDataSeed.RunAsync(scope.ServiceProvider);
    return;
}
if (args.Length == 1 && args[0] == "--seed-reference-data")
{
    if (!app.Environment.IsDevelopment())
        throw new InvalidOperationException("Starter reference data is available only in Development.");
    await using var scope = app.Services.CreateAsyncScope();
    var count = await HRMS.Infrastructure.Persistence.ReferenceDataSeed.RunAsync(scope.ServiceProvider);
    Console.WriteLine($"Created {count} leave types. Existing settings were preserved. Edit allowances in Leave > Leave types.");
    return;
}
if (args.Length == 1 && args[0] is "--admin-status" or "--admin-create" or
    "--admin-check-password" or "--admin-reset-password" or "--admin-unlock" or "--admin-activate")
{
    try
    {
        if (!app.Environment.IsDevelopment())
            throw new InvalidOperationException("Local administrator maintenance is available only in Development.");

        await using var scope = app.Services.CreateAsyncScope();
        await HRMS.DevelopmentAdminCommands.RunAsync(args[0], scope.ServiceProvider, builder.Configuration);
    }
    catch (InvalidOperationException error)
    {
        Console.Error.WriteLine(error.Message);
        Environment.ExitCode = 1;
    }
    return;
}
if (args.Contains("--seed-demo"))
{
    if (!app.Environment.IsDevelopment()) throw new InvalidOperationException("Demo seeding is restricted to Development.");
    await using var scope = app.Services.CreateAsyncScope();
    await HRMS.Infrastructure.Persistence.DevelopmentSeed.RunAsync(scope.ServiceProvider,
        Environment.GetEnvironmentVariable("HRMS_DEMO_PASSWORD") ?? "");
    return;
}
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


var supportedCultures = new[] { new CultureInfo("ar-JO"), new CultureInfo("en-US") };
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("ar-JO"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures,
    RequestCultureProviders =
    [
        new CookieRequestCultureProvider(),
        new AcceptLanguageHeaderRequestCultureProvider()
    ]
});

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseWhen(
    context => context.Request.Path.StartsWithSegments("/api"),
    apiPipeline => apiPipeline.UseExceptionHandler());

app.UseStatusCodePages(async statusContext =>
{
    var http = statusContext.HttpContext;
    if (http.Request.Path.StartsWithSegments("/api"))
        await Results.Problem(statusCode: http.Response.StatusCode,
            title: HRMS.Application.Common.Messages.Get("RequestFailedTitle"), extensions: new Dictionary<string, object?> { ["traceId"] = http.TraceIdentifier }).ExecuteAsync(http);
});
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapControllers();

app.Run();

public partial class Program { }
