using System.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using quizTool.Models;
using quizTool.Services;

var builder = WebApplication.CreateBuilder(args);

// Encoding for Excel, etc.
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

// Controllers + safe JSON
builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.ReferenceHandler =
        System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});

// CORS from config
builder.Services.AddCors(options =>
{
    var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
                  ?? new[] { "http://localhost:4200", "https://localhost:4200" };

    options.AddPolicy("allowCors", policy =>
    {
        policy.WithOrigins(origins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// DbContext
builder.Services.AddDbContext<QuizTool_Dbcontext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("APIConnection")));

// JWT
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
var jwtSection  = builder.Configuration.GetSection("Jwt");
var jwtSecret   = jwtSection["Secret"]   ?? throw new Exception("Missing Jwt:Secret");
var jwtIssuer   = jwtSection["Issuer"];
var jwtAudience = jwtSection["Audience"];

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false; // Render terminates TLS
    options.SaveToken            = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ValidateIssuer   = !string.IsNullOrWhiteSpace(jwtIssuer),
        ValidIssuer      = jwtIssuer,
        ValidateAudience = !string.IsNullOrWhiteSpace(jwtAudience),
        ValidAudience    = jwtAudience,
        ClockSkew        = TimeSpan.Zero,
        RoleClaimType    = ClaimTypes.Role,
        NameClaimType    = ClaimTypes.Name
    };
});

builder.Services.AddAuthorization();

// Swagger toggle (enable in prod via env var Swagger__Enabled=true)
var swaggerEnabled = builder.Configuration.GetValue<bool>("Swagger:Enabled");
if (swaggerEnabled || builder.Environment.IsDevelopment())
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo { Title = "quizTool API", Version = "v1" });
        c.CustomSchemaIds(t => t.FullName);

        var jwtScheme = new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Description = "Enter JWT token only (no 'Bearer ' prefix).",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
        };
        c.AddSecurityDefinition("Bearer", jwtScheme);
        c.AddSecurityRequirement(new OpenApiSecurityRequirement { { jwtScheme, Array.Empty<string>() } });
    });
}

// Email
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.AddSingleton<IEmailSender, ResilientEmailSender>();

// Respect reverse proxy headers (Render/Cloud)
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();
app.UseCors("allowCors");

// Ensure wwwroot/uploads exists
var webRoot = app.Environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
Directory.CreateDirectory(Path.Combine(webRoot, "uploads"));

// Swagger in dev or when explicitly enabled
if (swaggerEnabled || app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "quizTool API v1"));
}

// Only redirect HTTPS in Development; in Render HTTPS is terminated before Kestrel
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Simple health endpoint
app.MapGet("/health", () => Results.Ok(new { status = "ok", time = DateTime.UtcNow }))
   .AllowAnonymous();

// DB migrate + seed
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<QuizTool_Dbcontext>();
        db.Database.Migrate();
        db.SeedUsers();
    }
    catch (Exception ex)
    {
        Console.WriteLine("DB init failed: " + ex.Message);
    }
}

app.Run();
