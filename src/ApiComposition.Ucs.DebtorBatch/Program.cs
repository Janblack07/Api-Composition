// Program.cs
using ApiComposition.Ucs.DebtorBatch.Infrastructure;
using ApiComposition.Ucs.DebtorBatch.Json;
using ApiComposition.Ucs.DebtorBatch.Ports;
using ApiComposition.Ucs.DebtorBatch.Security;
using ApiComposition.Ucs.DebtorBatch.Workers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(new UpperCaseNamingPolicy())
        );
    });
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();

// --- Upload limits (multipart) ---
var maxFileSize = builder.Configuration.GetValue<long>("Upload:MaxFileSizeBytes", 10 * 1024 * 1024);
builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = maxFileSize;
});


// --- Validación defensiva de JWT config (para no crashear silencioso) ---
var jwtSection = builder.Configuration.GetSection("Jwt");
var issuer = jwtSection["Issuer"];
var audience = jwtSection["Audience"];
var key = jwtSection["Key"];

if (string.IsNullOrWhiteSpace(issuer) ||
    string.IsNullOrWhiteSpace(audience) ||
    string.IsNullOrWhiteSpace(key) ||
    key.Length < 32)
{
    throw new InvalidOperationException(
        "Falta configuración JWT en appsettings.json: Jwt:Issuer, Jwt:Audience, Jwt:Key (Key >= 32 chars).");
}

// --- JWT DIRECTO (sin Authority) ---
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key!)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

// --- Swagger ---
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "API Composition - UCS.DebtorBatch",
        Version = "v1"
    });

    var bearerScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Pega SOLO el token (Swagger agrega 'Bearer ')."
    };

    c.AddSecurityDefinition("Bearer", bearerScheme);

    c.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
    {
        { new OpenApiSecuritySchemeReference("Bearer"), new List<string>() }
    });
});

// --- DI (puertos) ---
builder.Services.AddScoped<ITenantContextAccessor, HttpTenantContextAccessor>();
builder.Services.AddSingleton<IImportJobStore, MemoryCacheImportJobStore>();
builder.Services.AddSingleton<IObjectStorage, LocalDiskObjectStorage>();
builder.Services.AddSingleton<IImportQueue, InMemoryImportQueue>();
// Readers
builder.Services.AddSingleton<ExcelImportFileReader>();
builder.Services.AddSingleton<CsvImportFileReader>();
builder.Services.AddSingleton<IImportFileReader, CompositeImportFileReader>();

// Validation + reports
builder.Services.AddSingleton<IDebtorRecordValidator, BasicDebtorRecordValidator>();
builder.Services.AddSingleton<IErrorReportWriter, ExcelErrorReportWriter>();

// Worker (ya lo tienes como hosted service)
builder.Services.AddHostedService<ImportJobWorker>();

// --- Worker (consume la cola y actualiza estados) ---
builder.Services.AddHostedService<ImportJobWorker>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.MapGet("/", () => Results.Redirect("/swagger"));

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// DEV: endpoint para descargar archivos del storage local (solo para pruebas)
app.MapGet("/dev-download/{objectKey}", (string objectKey, IConfiguration cfg) =>
{
    var root = cfg["Storage:Local:RootPath"] ?? "App_Data/uploads";
    var path = Path.Combine(root, objectKey);

    if (!System.IO.File.Exists(path))
        return Results.NotFound();

    return Results.File(path, "application/octet-stream", Path.GetFileName(path));
}).AllowAnonymous();

app.Run();
