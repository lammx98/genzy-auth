using System.Security.Claims;
using Genzy.Base.Security.Jwt;
using Genzy.Auth.Data;
using Genzy.Auth.Services;
using Genzy.Base.Utils;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Logging;
using Microsoft.AspNetCore.HttpOverrides;
using Genzy.Base.Extensions;
using Genzy.Auth.Mapping;
using Genzy.Base.Security;
IdentityModelEventSource.ShowPII = true;
var builder = WebApplication.CreateBuilder(args);

var env = builder.Environment;
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();
// Add services to the container.
builder.Services.AddHttpClient();
builder.Services.AddControllers();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
    {
        Title = "Genzy Authenticate",
        Version = "v1",
        Description = "API documentation for Genzy Authenticate System"
    });
    options.SupportNonNullableReferenceTypes();
});

// Configure shared JWT via base library (section name 'Jwt')
var authBuilder = builder.Services.AddJwtCore(builder.Configuration, cfg =>
{
    // Keep existing events from AddJwtCore and extend with custom behavior
    var existingEvents = cfg.Events ?? new JwtBearerEvents();

    cfg.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
            string? token = null;

            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                token = authHeader.Substring("Bearer ".Length).Trim();
            }

            if (string.IsNullOrEmpty(token))
            {
                token = context.Request.Cookies["auth_token"]; // SSR cookie fallback
            }

            if (!string.IsNullOrEmpty(token))
            {
                context.Token = token;
            }

            return existingEvents.OnMessageReceived?.Invoke(context) ?? Task.CompletedTask;
        },
        OnAuthenticationFailed = ctx =>
        {
            Console.WriteLine($"[JWT] Authentication failed: {ctx.Exception.GetType().Name} - {ctx.Exception.Message}");
            if (ctx.Exception.InnerException != null)
            {
                Console.WriteLine($"[JWT] Inner exception: {ctx.Exception.InnerException.Message}");
            }
            return existingEvents.OnAuthenticationFailed?.Invoke(ctx) ?? Task.CompletedTask;
        },
        OnTokenValidated = ctx =>
        {
            var userId = ctx.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return existingEvents.OnTokenValidated?.Invoke(ctx) ?? Task.CompletedTask;
        },
        OnChallenge = ctx =>
        {
            return existingEvents.OnChallenge?.Invoke(ctx) ?? Task.CompletedTask;
        }
    };
});

// Add DB context
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Chain additional auth handlers (cookie + external providers)
authBuilder
    .AddCookie()
    .AddGoogle(options =>
{
    options.ClientId = builder.Configuration["Authentication:Google:ClientId"] ?? throw new Exception("Google ClientId not configured");
    options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? throw new Exception("Google ClientSecret not configured");
    options.CallbackPath = "/api/v1/auth/signin-google";
    options.SaveTokens = true; // persist id_token/access_token for callback usage
    // Optional: request extra info
    options.Scope.Add("profile");
    options.Scope.Add("email");
})
    .AddFacebook(options =>
{
    options.AppId = builder.Configuration["Authentication:Facebook:AppId"] ?? throw new Exception("Facebook AppId not configured");
    options.AppSecret = builder.Configuration["Authentication:Facebook:AppSecret"] ?? throw new Exception("Facebook AppSecret not configured");
});
builder.Services.AddAuthorization();

// Snowflake generator
var nodeId = Environment.GetEnvironmentVariable("SNOWFLAKE_NODE_ID");
builder.Services.AddSingleton(new SnowflakeIdGenerator(new SnowflakeOptions
{
    NodeId = long.TryParse(nodeId, out var n) ? n : 1000
}));

// Add services
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<TokenService>();

// AutoMapper, JWT auth, UserContext
builder.Services.AddAutoMapper(typeof(AuthMappingProfile).Assembly);
builder.Services.AddUserContext();

// Add Cors
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowNextJs",
        policy => policy.WithOrigins("http://localhost:3020")
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials());
});

// Config Forward Headers
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                               ForwardedHeaders.XForwardedProto |
                               ForwardedHeaders.XForwardedHost;

    // WARN: ONLY CLEAR IN DEVELOPMENT ENVIRONMENT
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// CORS must be before UseRouting
app.UseCors("AllowNextJs");

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseRouting();

app.UseForwardedHeaders();
app.UseAuthentication();
app.UseAuthorization();

app.MapGroup("/api/v1/auth").MapControllers();
app.MapDefaultControllerRoute();

// Add exception handling middleware
app.UseExceptionHandling();

app.Run();
