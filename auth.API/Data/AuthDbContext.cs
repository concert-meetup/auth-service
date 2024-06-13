using auth.API.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace auth.API.Data;

public class AuthDbContext : IdentityDbContext
{
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options) 
    {
        
    }
}