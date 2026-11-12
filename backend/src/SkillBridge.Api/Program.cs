using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Scalar.AspNetCore;
using SkillBridge.Api;
using SkillBridge.Api.Errors;
using SkillBridge.Api.Hubs;
using SkillBridge.Api.Realtime;
using SkillBridge.Api.Services;
using SkillBridge.Api.Swagger;
using SkillBridge.Application;
using SkillBridge.Application.Common.Interfaces;
using SkillBridge.Infrastructure;
using SkillBridge.Infrastructure.Identity;

var builder = WebApplication.CreateBuilder(args);

if (OperatingSystem.IsWindows())
{
    builder.Logging.AddFilter<Microsoft.Extensions.Logging.EventLog.EventLogLoggerProvider>(null, LogLevel.None);
}

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddJsonFile("appsettings.Development.local.json", optional: true, reloadOnChange: false)
        .AddEnvironmentVariables()
        .AddCommandLine(args);
}

builder.Services
    .AddControllers(options => options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true)
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.AllowInputFormatterExceptionMessages = false;
    })
    .ConfigureApiBehaviorOptions(options => options.InvalidModelStateResponseFactory = context =>
    {
        var problem = new ValidationProblemDetails(context.ModelState)
        {
            Type = "https://httpstatuses.io/400",
            Title = "Invalid request",
            Status = StatusCodes.Status400BadRequest,
            Detail = "Please correct the invalid fields."
        };
        problem.Extensions["code"] = "validation.failed";
        problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
        var response = new BadRequestObjectResult(problem);
        response.ContentTypes.Add("application/problem+json");
        return response;
    });

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

builder.Services.AddSignalR();
builder.Services.AddSingleton<Microsoft.AspNetCore.SignalR.IUserIdProvider, CustomUserIdProvider>();
builder.Services.AddScoped<IRealtimeNotifier, SignalRNotifier>();
builder.Services.AddHostedService<JobExpirationWorker>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((options, jwtOptions) =>
    {
        var jwt = jwtOptions.Value;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            RequireSignedTokens = true,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ClockSkew = TimeSpan.Zero,
            NameClaimType = "name",
            RoleClaimType = ClaimTypes.Role
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                context.HandleResponse();
                context.Response.Headers.WWWAuthenticate = "Bearer";
                return ProblemDetailsWriter.WriteAsync(context.HttpContext, StatusCodes.Status401Unauthorized,
                    "auth.unauthenticated", "Authentication required", "Please sign in to continue.");
            },
            OnForbidden = context => ProblemDetailsWriter.WriteAsync(context.HttpContext,
                StatusCodes.Status403Forbidden, "auth.forbidden", "Forbidden", "Your account type cannot do this.")
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Max(1, retryAfter.TotalSeconds)).ToString();
        }
        else
        {
            context.HttpContext.Response.Headers.RetryAfter = "60";
        }

        await ProblemDetailsWriter.WriteAsync(
            context.HttpContext,
            StatusCodes.Status429TooManyRequests,
            "rate_limit.exceeded",
            "Too Many Requests",
            "Maximum login attempts exceeded. Please try again in 1 minute.");
    };

    options.AddPolicy("LoginRateLimit", httpContext =>
    {
        var clientIp = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()
            ?? httpContext.Connection.RemoteIpAddress?.ToString();

        if (string.IsNullOrEmpty(clientIp) && builder.Environment.IsDevelopment())
        {
            clientIp = httpContext.TraceIdentifier;
        }

        clientIp ??= "anonymous";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: clientIp,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    });
});
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<AppExceptionHandler>();

builder.Services.AddCors(o => o.AddPolicy("Angular", p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:4200"])
    .AllowAnyHeader()
    .AllowAnyMethod()
    .WithExposedHeaders("Location")
    .AllowCredentials()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Paste the accessToken from register or login."
    });
    options.DocumentFilter<AuthorizationDocumentFilter>();
});

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<SkillBridge.Infrastructure.Persistence.AppDbContext>();
    await dbContext.Database.MigrateAsync();

    var seedDemoUsers = app.Configuration.GetValue<bool?>("Seed:DemoUsers") ?? app.Environment.IsDevelopment();
    await scope.ServiceProvider.GetRequiredService<IdentitySeeder>().SeedAsync(seedDemoUsers);
}

app.UseExceptionHandler();

app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        return Task.CompletedTask;
    });
    await next();
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.MapScalarApiReference(options => options
        .WithTitle("SkillBridge API")
        .WithOpenApiRoutePattern("/swagger/{documentName}/swagger.json")
        .AddPreferredSecuritySchemes("Bearer")
        .DisableDefaultFonts())
        .AllowAnonymous();
}

app.UseCors("Angular");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<SkillBridgeHub>("/hubs/app");

app.Run();

public partial class Program;
