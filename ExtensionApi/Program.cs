using System.Reflection;
using System.Threading.RateLimiting;
using ExtensionApi.Options;
using ExtensionApi.Services;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.Configure<GeminiOptions>(builder.Configuration.GetSection(GeminiOptions.SectionName));
builder.Services.Configure<SecurityOptions>(builder.Configuration.GetSection(SecurityOptions.SectionName));
builder.Services.AddHttpClient(ImageDownloader.ClientName)
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddHttpClient(GeminiPhotoOrderingService.ClientName);
builder.Services.AddScoped<IImageDownloader, ImageDownloader>();
builder.Services.AddScoped<IPhotoOrderingService, GeminiPhotoOrderingService>();
builder.Services.AddScoped<IStreetCheckingService, GeminiStreetCheckingService>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("listing-ai", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Nikas Extension AI API",
        Version = "v1",
        Description = "Isolated Gemini-powered listing photo ordering and constrained street checking. This API has no website database access."
    });
    options.AddSecurityDefinition("ExtensionApiKey", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Name = "X-Extension-Key",
        Description = "Shared access key configured as Security__ExtensionApiKey."
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("ExtensionApiKey", document)] = []
    });
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFile));
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Nikas Extension AI API v1");
    options.RoutePrefix = "swagger";
    options.DocumentTitle = "Nikas Extension AI API";
});

if (!app.Environment.IsDevelopment()) app.UseHttpsRedirection();

app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .WithTags("Health")
    .WithName("Health");

app.Run();

public partial class Program;
