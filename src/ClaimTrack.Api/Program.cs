using ClaimTrack.Api.Common;
using ClaimTrack.Api.Data;
using ClaimTrack.Api.Data.Seed;
using ClaimTrack.Api.Entities;
using ClaimTrack.Api.Middleware;
using ClaimTrack.Api.Services;
using ClaimTrack.Api.Validators;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Formatting.Compact;

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Serilog replaces the built-in loggers. Levels come from the "Serilog" section of appsettings.json.
    // Development gets readable lines; every other environment (Docker, production) gets one JSON
    // object per line, which log tools can search by property (UserId, StatusCode, TraceId, ...).
    // preserveStaticLogger: the host owns and disposes its own logger instead of replacing the global
    // Log.Logger. Otherwise the CloseAndFlush in "finally" below could shut down another host's logger
    // (the tests start several copies of the API in one process).
    builder.Services.AddSerilog(preserveStaticLogger: true, configureLogger: (services, logger) =>
    {
        logger
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services) // picks up extra sinks registered in DI; the tests add one
            .Enrich.FromLogContext();

        if (builder.Environment.IsDevelopment())
        {
            logger.WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}");
        }
        else
        {
            logger.WriteTo.Console(new RenderedCompactJsonFormatter());
        }
    });

    // The connection string value comes from user-secrets, never from appsettings.json.
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("ClaimTrack")));

    builder.Services.AddScoped<IPolicyService, PolicyService>();
    builder.Services.AddScoped<IClaimService, ClaimService>();
    builder.Services.AddScoped<IAuditService, AuditService>();
    builder.Services.AddScoped<INumberGenerator, NumberGenerator>();
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<ITokenService, TokenService>();
    builder.Services.AddScoped<IUserService, UserService>();

    // Lets services ask "who is calling?" through ICurrentUser, without depending on HTTP.
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUser, CurrentUser>();

    // Demo data for development (requirements §9). The demo password comes from user-secrets.
    builder.Services.AddOptions<SeedOptions>().BindConfiguration(SeedOptions.SectionName);
    builder.Services.AddScoped<DatabaseSeeder>();

    // Built into ASP.NET Core: salted PBKDF2 hashing for passwords (BR-11).
    builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

    // The current time comes from TimeProvider so tests can use a fixed date.
    builder.Services.AddSingleton(TimeProvider.System);

    // JWT settings. The signing key comes from user-secrets (or an environment variable in Docker).
    // ValidateOnStart: the API refuses to start with a missing or too-short key, instead of failing at the first login.
    builder.Services.AddOptions<JwtOptions>()
        .BindConfiguration(JwtOptions.SectionName)
        .Validate(jwt => jwt.IsValid,
            $"Jwt settings are invalid. Set Jwt:SigningKey (at least {JwtOptions.MinimumSigningKeyBytes} bytes) in user-secrets.")
        .ValidateOnStart();

    // Reads "Authorization: Bearer <token>" on each request and checks it with the shared rules in JwtOptions.
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
    builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
        .Configure<IOptions<JwtOptions>, TimeProvider>((bearer, jwt, timeProvider) =>
        {
            // Keep claim names as they are in the token ("sub", "role") instead of renaming
            // them to long XML-style names.
            bearer.MapInboundClaims = false;
            bearer.TokenValidationParameters = jwt.Value.CreateValidationParameters(timeProvider);
        });

    // Finds and registers every validator in this project.
    builder.Services.AddValidatorsFromAssemblyContaining<Program>();

    // Controllers and the OpenAPI document each have their own JSON settings,
    // so both get the same rules from JsonSettings.
    builder.Services.AddControllers(options => options.Filters.Add<ValidationFilter>())
        .AddJsonOptions(options => JsonSettings.Apply(options.JsonSerializerOptions));
    builder.Services.ConfigureHttpJsonOptions(options => JsonSettings.Apply(options.SerializerOptions));

    // ASP.NET's automatic 400, for values it can't read at all (e.g. ?status=Pending or broken JSON),
    // uses the same builder as ValidationFilter, so every 400 has camelCase field names.
    builder.Services.Configure<ApiBehaviorOptions>(options =>
        options.InvalidModelStateResponseFactory = context =>
            ValidationProblems.Create(context.HttpContext, ValidationProblems.FromModelState(context.ModelState)));

    // The transformer adds the "Authorize" button for JWTs to Swagger UI.
    builder.Services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

    // Every error response uses the Problem Details shape; unexpected exceptions go to GlobalExceptionHandler.
    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

    // Background job: flags claims stuck UnderReview for too long (see OverdueClaimsJob).
    builder.Services.AddOptions<OverdueClaimsOptions>()
        .BindConfiguration(OverdueClaimsOptions.SectionName)
        .Validate(options => options.IsValid, "OverdueClaims ThresholdDays and CheckIntervalMinutes must be greater than 0.")
        .ValidateOnStart();
    builder.Services.AddScoped<IOverdueClaimsService, OverdueClaimsService>();
    builder.Services.AddHostedService<OverdueClaimsJob>();

    // Documents on claims: files in a local folder behind IDocumentStorage (swap for Azure Blob later).
    builder.Services.AddOptions<DocumentOptions>()
        .BindConfiguration(DocumentOptions.SectionName)
        .Validate(options => options.IsValid, "Documents StoragePath, MaxSizeBytes, and MaxPerClaim must be set.")
        .ValidateOnStart();
    builder.Services.AddSingleton<IDocumentStorage, LocalFileStorage>();
    builder.Services.AddScoped<IDocumentService, DocumentService>();

    // Caps the whole upload form while it's being read, so an oversized upload is refused early
    // instead of being read in full. 1 MB of headroom for the form's own parts; the validator then
    // enforces the exact per-file limit.
    builder.Services.AddOptions<FormOptions>()
        .Configure<IOptions<DocumentOptions>>((form, documents) =>
            form.MultipartBodyLengthLimit = documents.Value.MaxSizeBytes + 1024 * 1024);

    // Limits on how often one client can call the API (see RateLimitingSetup).
    builder.Services.AddClaimTrackRateLimiting();

    // GET /health checks the database too ("ready"); GET /health/live checks nothing external.
    // The 3-second timeout matters: an unreachable SQL Server otherwise takes ~15 s to fail, and load
    // balancers give a probe only a few seconds, so a slow "Unhealthy" would look like no answer at all.
    builder.Services.AddHealthChecks()
        .AddCheck<DatabaseHealthCheck>("database", tags: [HealthEndpoints.ReadyTag], timeout: TimeSpan.FromSeconds(3));

    var app = builder.Build();

    // One summary line per request: method, path (never the query string or headers, so no tokens),
    // status, time, and who made it. First in the pipeline, so it times and sees the final status of
    // everything after it, including 500s produced by the exception handler.
    // Logger: this host's Serilog logger. Without it the middleware writes to the global Log.Logger,
    // which this app deliberately doesn't use (see preserveStaticLogger above).
    app.UseSerilogRequestLogging(options =>
    {
        options.Logger = app.Services.GetRequiredService<Serilog.ILogger>();
        options.EnrichDiagnosticContext = RequestLogEnricher.Enrich;
        options.GetLevel = RequestLogEnricher.GetLevel; // successful health probes don't flood the log
    });

    // Catches exceptions from everything after it.
    app.UseExceptionHandler();

    // Error responses that have no body (e.g. an unknown route's 404) get a Problem Details body too.
    app.UseStatusCodePages();

    // Applies any pending migrations when the API starts. Off unless configured: docker compose turns
    // it on so `docker compose up` gives a ready database. EF Core locks the database while migrating,
    // so two API copies starting together can't both apply the same migration.
    if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
    {
        using var scope = app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    if (app.Environment.IsDevelopment())
    {
        // Adds the demo data that's missing. Development only: other environments never get test accounts.
        using (var scope = app.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync();
        }

        app.MapOpenApi();

        // Swagger UI page at /swagger, built from the OpenAPI document above.
        // EnablePersistAuthorization keeps the pasted token after a page refresh (stored in your browser).
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/openapi/v1.json", "ClaimTrack API");
            options.EnablePersistAuthorization();
        });
    }

    // In a container, HTTPS is handled by whatever sits in front of it (a proxy or load balancer), and
    // the API itself only speaks HTTP, so redirecting would just log a warning on every start.
    // Microsoft's container images set DOTNET_RUNNING_IN_CONTAINER=true.
    if (!app.Configuration.GetValue<bool>("DOTNET_RUNNING_IN_CONTAINER"))
    {
        app.UseHttpsRedirection();
    }

    // Authentication (who are you?) must run before authorization (are you allowed?).
    app.UseAuthentication();

    // After authentication, so the general limit can count per logged-in user.
    app.UseRateLimiter();

    app.UseAuthorization();

    // Secure by default: every controller endpoint needs a valid token unless it's marked [AllowAnonymous].
    // (Not a global fallback policy: that would also turn unknown URLs into 401 instead of 404.)
    app.MapControllers().RequireAuthorization();

    // For load balancers and container platforms, which can't log in. The response is only the word
    // "Healthy" or "Unhealthy" (503); the reason for a failure goes to the logs, never to the caller.
    app.MapHealthChecks(HealthEndpoints.Ready, new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains(HealthEndpoints.ReadyTag)
    }).AllowAnonymous();

    // Liveness checks nothing external: if the database is down, restarting the API won't help.
    app.MapHealthChecks(HealthEndpoints.Live, new HealthCheckOptions
    {
        Predicate = _ => false
    }).AllowAnonymous();

    app.Run();
}
// HostAbortedException is how the test framework stops this code after Build() to start the
// host itself; it isn't a crash. Anything else here means the API couldn't start (bad config,
// missing secret, database down), so it's written to the console before the process exits.
catch (Exception exception) when (exception is not HostAbortedException)
{
    Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();
    Log.Fatal(exception, "ClaimTrack failed to start");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
