using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using NotesApi.Auth;
using NotesApi.Data;
using NotesApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ---------- Database ----------
// The "Testing" environment (used by the integration tests) registers its own
// in-memory SQLite database instead.
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<NotesDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("NotesDb")));
}

// ---------- JWT authentication ----------
var jwtSection = builder.Configuration.GetSection("Jwt");
builder.Services.Configure<JwtOptions>(jwtSection);
var jwt = jwtSection.Get<JwtOptions>()
          ?? throw new InvalidOperationException("Missing 'Jwt' configuration section.");

if (System.Text.Encoding.UTF8.GetByteCount(jwt.Key) < 32)
    throw new InvalidOperationException("Jwt:Key must be at least 32 bytes long.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false; // keep "sub" / "unique_name" as-is
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = TokenService.CreateSigningKey(jwt.Key),
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = JwtRegisteredClaimNames.UniqueName
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

// ---------- MVC + OpenAPI ----------
builder.Services.AddControllers();
builder.Services.AddOpenApi(o => o.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();                                   // /openapi/v1.json
    app.UseSwaggerUI(o => o.SwaggerEndpoint("/openapi/v1.json", "Notes API")); // /swagger
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Lets the test project reference Program in WebApplicationFactory<Program>.
public partial class Program;
