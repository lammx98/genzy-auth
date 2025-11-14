using System.Text;
using Genzy.Auth.Configuration;
using Genzy.Auth.Data;
using Genzy.Auth.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

var env = builder.Environment;
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();
// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, ctx, ct) =>
    {
        document.Components ??= new();
        document.Components.SecuritySchemes ??= new Dictionary<string, OpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Name = "Authorization",
            Description = "JWT Authorization header using the Bearer scheme."
        };

        document.SecurityRequirements ??= new List<OpenApiSecurityRequirement>();
        document.SecurityRequirements.Add(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                }, Array.Empty<string>()
            }
        });
        return Task.CompletedTask;
    });
});

// Configure JWT settings
var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>();
builder.Services.AddSingleton(jwtSettings ?? throw new Exception("JWT Settings not configured"));

// Add DB context
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configure Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
})
.AddCookie()
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings?.Issuer,
        ValidAudience = jwtSettings?.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings?.Secret ?? throw new Exception("JWT Secret not configured")))
    };
})
.AddGoogle(options =>
{
    options.ClientId = builder.Configuration["Authentication:Google:ClientId"] ?? throw new Exception("Google ClientId not configured");
    options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? throw new Exception("Google ClientSecret not configured");
    options.CallbackPath = "/auth/signin-google";
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

// Add services
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<TokenService>();

// Add Cors
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowNextJs",
        policy => policy.WithOrigins("http://localhost:3000")
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials());
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Genzy Auth API v1");
        options.DocumentTitle = "Genzy Auth - Swagger";
    });
}

app.UseRouting();
app.UseCors("AllowNextJs");

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapDefaultControllerRoute();

app.Run();
