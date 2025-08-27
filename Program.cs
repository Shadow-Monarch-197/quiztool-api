using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;                
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using quizTool.Models;
using quizTool.Services;
using System.IdentityModel.Tokens.Jwt;                    
using System.Text;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;


var builder = WebApplication.CreateBuilder(args);

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.ReferenceHandler =
            System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

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

builder.Services.AddDbContext<QuizTool_Dbcontext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("APIConnection")));

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
    options.RequireHttpsMetadata = false;
    options.SaveToken            = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),

        ValidateIssuer = !string.IsNullOrWhiteSpace(jwtIssuer),
        ValidIssuer    = jwtIssuer,

        ValidateAudience = !string.IsNullOrWhiteSpace(jwtAudience),
        ValidAudience    = jwtAudience,

        ClockSkew = TimeSpan.Zero,

        // Map standard claim types
        RoleClaimType = ClaimTypes.Role,
        NameClaimType = ClaimTypes.Name
    };
});



builder.Services.AddAuthorization();

// Swagger only in Development (recommended)
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

//builder.Services.AddScoped<quizTool.Services.IEmailSender, quizTool.Services.SmtpEmailSender>(); // NEW
//builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Email"));
//builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();

builder.Services.Configure<quizTool.Services.EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.AddSingleton<quizTool.Services.IEmailSender, quizTool.Services.ResilientEmailSender>();


var app = builder.Build();

app.UseCors("allowCors");

var webRoot = app.Environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
Directory.CreateDirectory(Path.Combine(webRoot, "uploads"));

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c => { c.SwaggerEndpoint("/swagger/v1/swagger.json", "quizTool API v1"); });
}

// app.UseSwagger();
// app.UseSwaggerUI(c => { c.SwaggerEndpoint("/swagger/v1/swagger.json", "quizTool API v1"); });

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseAuthentication();   
app.UseAuthorization();

app.MapControllers();

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
