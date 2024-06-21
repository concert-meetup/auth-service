using System.Text;
using auth.API.Config;
using auth.API.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.


var jwtConfig = new JwtConfig();
builder.Configuration.Bind("JwtConfig", jwtConfig);
builder.Services.Configure<JwtConfig>(builder.Configuration.GetSection("JwtConfig"));


var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                       ?? $"Server={Environment.GetEnvironmentVariable("DB_HOST")};" +
                       $"Database={Environment.GetEnvironmentVariable("DB_NAME_AUTH")};" +
                       $"User Id={Environment.GetEnvironmentVariable("DB_USER")};" +
                       $"Password={Environment.GetEnvironmentVariable("DB_PASSWORD")};";

builder.Services.AddDbContext<AuthDbContext>(options => 
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));



builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer(); 
builder.Services.AddSwaggerGen();

builder.Services.AddDefaultIdentity<IdentityUser>(options =>
        options.SignIn.RequireConfirmedAccount = false)
    .AddEntityFrameworkStores<AuthDbContext>(); 

builder.Services.AddHealthChecks();

// auth
var key = "hiUilgpplHyDpbGIujiSOTKHqKfmhppDLATiXlYlXNLzURpUPqkKEqGXLTUsyhQR"u8.ToArray();
var tokenValidationParameters = new TokenValidationParameters()
{
    ValidateIssuerSigningKey = true,
    IssuerSigningKey = new SymmetricSecurityKey(key),
    ValidateIssuer = false,
    ValidateAudience = true,
    RequireExpirationTime = true,
    ValidateLifetime = true,
    ValidAudience = "http://concert-meetup/api",
    ClockSkew = TimeSpan.Zero
};

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(jwt =>
    {
        jwt.SaveToken = true;
        jwt.TokenValidationParameters = tokenValidationParameters;
    });

builder.Services.AddSingleton(tokenValidationParameters);

builder.Services.AddCors();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    dbContext.Database.Migrate();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHealthChecks("/health");

app.UseCors(x => x
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()
    .WithOrigins("http://localhost:8000", "98.66.224.145:8000"));

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
 
app.MapControllers();

app.Run();
