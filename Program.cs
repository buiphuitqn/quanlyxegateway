using System;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Add YARP
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// JWT Authentication setup
var jwt = builder.Configuration.GetSection("AppSettings");
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwt["Issuer"],
        ValidAudience = jwt["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Secret"] ?? "default_secret_key_change_me")),
        ClockSkew = TimeSpan.FromMinutes(5)
    };
});

// Authorization setup
builder.Services.AddAuthorization(options =>
{
    // Define a "default" policy that requires an authenticated user
    options.AddPolicy("default", policy => policy.RequireAuthenticatedUser());
});

// Output Caching setup
builder.Services.AddOutputCache(options =>
{
    options.AddPolicy("DefaultCache", builder =>
    {
        builder.Expire(TimeSpan.FromSeconds(60))
               .SetVaryByHeader("Authorization") // Correct method name
               .SetVaryByQuery("*"); // Vary by all query parameters
    });
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("GatewayCors", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors("GatewayCors");

// Authentication & Authorization must be BEFORE MapReverseProxy
app.UseAuthentication();
app.UseAuthorization();

// OutputCache must be after UseRouting (implicit in MapReverseProxy) but before MapReverseProxy
// Actually in .NET 8/9, UseOutputCache should be before endpoints that use it.
app.UseOutputCache();

// Map YARP
app.MapReverseProxy();

app.Run();
