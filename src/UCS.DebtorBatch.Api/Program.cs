
using Microsoft.OpenApi;
using System.Text.Json.Serialization;
using UCS.DebtorBatch.Api.Application.Abstractions;
using UCS.DebtorBatch.Api.Application.Import;
using UCS.DebtorBatch.Api.Application.Validation;
using UCS.DebtorBatch.Api.Application.Workers;
using UCS.DebtorBatch.Api.Infrastructure.Auth;
using UCS.DebtorBatch.Api.Infrastructure.Cache;
using UCS.DebtorBatch.Api.Infrastructure.Core;
using UCS.DebtorBatch.Api.Infrastructure.Excel;
using UCS.DebtorBatch.Api.Infrastructure.Storage;
using UCS.DebtorBatch.Api.Infrastructure.TenantRules;
using UCS.DebtorBatch.Api.Options;

var builder = WebApplication.CreateBuilder(args);

// ==============================
// MVC + JSON
// ==============================
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// ==============================
// Host / Background services behavior
// ==============================
builder.Services.Configure<HostOptions>(o =>
{
    
    o.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
});

// ==============================
// Cache
// ==============================
builder.Services.AddMemoryCache();

// ==============================
// Options (UNA sola fuente de verdad: appsettings)
// ==============================
builder.Services.Configure<ImportOptions>(builder.Configuration.GetSection("Import"));
builder.Services.Configure<CoreOptions>(builder.Configuration.GetSection("Core"));
builder.Services.Configure<ErrorPresentationOptions>(builder.Configuration.GetSection("ErrorPresentation"));
builder.Services.Configure<MockIdentityOptions>(builder.Configuration.GetSection("MockIdentity"));

// ==============================
// Swagger
// ==============================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "UCS.DebtorBatch - API de Carga Masiva",
        Version = "1.0.0"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header
    });

    c.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
    {
        { new OpenApiSecuritySchemeReference("Bearer"), new List<string>() }
    });
});

// ==============================
// Core BFF services
// ==============================

// Queue
builder.Services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();

// Job repo (IMemoryCache)
builder.Services.AddSingleton<IImportJobRepository, MemoryCacheJobRepository>();

// Tracker
builder.Services.AddSingleton<ImportJobTracker>();

// Storage local (./storage)
builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();
builder.Services.AddHostedService<LocalStorageCleanupHostedService>();

// Excel parser
builder.Services.AddSingleton<IExcelParser, MiniExcelAdapter>();

// Rules provider (mock por ahora)
builder.Services.AddSingleton<IValidationRuleProvider, MockValidationRuleProvider>();

// Validator factory
builder.Services.AddSingleton<DynamicValidatorFactory>();

// Core client (real)
builder.Services.AddHttpClient<IDebtorBatchClient, CoreDebtorHttpClient>();

// Dispatcher
builder.Services.AddSingleton<ImportJobDispatcher>();

// ==============================
// Worker (Executor + Hosted)
// ==============================

// 1) Registras el executor
builder.Services.AddSingleton<IImportJobExecutor, ImportBackgroundWorker>();

// 2) Lo expones como HostedService (sin duplicarlo)
builder.Services.AddHostedService(sp => (ImportBackgroundWorker)sp.GetRequiredService<IImportJobExecutor>());

var app = builder.Build();

// ==============================
// Middleware
// ==============================

// OJO: esto es para DEV/QA. En prod deberías condicionarlo por config/env.
app.UseMiddleware<MockIdentityMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.MapGet("/", () => Results.Redirect("/swagger"))
   .ExcludeFromDescription();

app.Run();