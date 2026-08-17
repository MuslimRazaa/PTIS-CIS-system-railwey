using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PremierErp.Api.Data;
using PremierErp.Api.Models;
using PremierErp.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------- database: SQLite for dev, Azure SQL / SQL Server for production ----------
var provider = builder.Configuration["Database:Provider"] ?? "Sqlite";
var connStr = builder.Configuration.GetConnectionString("Default")!;
builder.Services.AddDbContext<ErpDbContext>(o =>
{
    if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase)) o.UseSqlServer(connStr);
    else o.UseSqlite(connStr);
});

// ---------- auth ----------
builder.Services.AddSingleton<TokenService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
        NameClaimType = "email"
    };
});
builder.Services.AddAuthorization();

// ---------- CORS (the HTML app may be opened from file:// or any host) ----------
var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["*"];
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
{
    if (origins.Contains("*")) p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    else p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
}));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ---------- migrate + seed admin ----------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
    db.Database.EnsureCreated();
    if (!db.Users.Any())
    {
        var email = builder.Configuration["Seed:AdminEmail"] ?? "admin@premiertubular.com";
        var pw = builder.Configuration["Seed:AdminPassword"] ?? "ChangeMe!2026";
        db.Users.Add(new AppUser
        {
            Email = email.ToLower(),
            Name = "Administrator",
            Role = "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(pw)
        });
        db.SaveChanges();
    }
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// serve premier-erp.html from wwwroot at the site root
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();
app.MapFallbackToFile("premier-erp.html");

app.Run();
