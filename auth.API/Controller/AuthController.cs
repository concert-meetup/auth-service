using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using auth.API.Config;
using auth.API.Data;
using auth.API.DTO;
using auth.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace auth.API.Controller;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ILogger<AuthController> _logger;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly AuthDbContext _context;
    private readonly JwtConfig _jwtConfig;
    private readonly TokenValidationParameters _tokenValidationParameters;

    public AuthController(ILogger<AuthController> logger, 
        UserManager<IdentityUser> userManager,
        IOptionsMonitor<JwtConfig> optionsMonitor, AuthDbContext context, TokenValidationParameters tokenValidationParameters)
    {
        _logger = logger;
        _userManager = userManager;
        _context = context;
        _tokenValidationParameters = tokenValidationParameters;
        _jwtConfig = optionsMonitor.CurrentValue;
    }

    [HttpPost]
    [Route("register")]
    public async Task<IActionResult> Register([FromBody] UserRegistrationRequestDto request)
    {
        if (ModelState.IsValid)
        {
            // TODO validate email
            var emailExist = await _userManager.FindByEmailAsync(request.Email);

            if (emailExist != null)
            {
                return BadRequest("Email already exists");
            }

            var newUser = new IdentityUser()
            { 
                Email = request.Email,
                UserName = request.Email
            };

            var isCreated = await _userManager.CreateAsync(newUser, request.Password);
            if (isCreated.Succeeded)
            {
                var token = await GenerateJwtToken(newUser);
                return Ok(token);
            }

            return BadRequest(isCreated.Errors.Select(x => x.Description).ToList());

            // return BadRequest("Error creating user");
        }

        return BadRequest("Invalid request");
    }

    [HttpPost]
    [Route("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        if (ModelState.IsValid)
        {
            var existingUser = await _userManager.FindByEmailAsync(request.Email);
            if (existingUser == null)
            {
                return BadRequest("Invalid user");
            }

            var isPasswordValid = await _userManager.CheckPasswordAsync(existingUser, request.Password);

            if (isPasswordValid)
            {
                var token = await GenerateJwtToken(existingUser);
                return Ok(token);
            }

            return BadRequest("Wrong email or password");
        }

        return BadRequest("Invalid request");
    }
    
    [HttpPost]
    [Route("refresh")]
    public async Task<IActionResult> RefreshToken([FromBody] TokenRequest request)
    {
        if (ModelState.IsValid)
        {
            var result = await VerifyAndGenerateToken(request);

            if (result == null)
            {
                return BadRequest(new AuthToken()
                {
                    Errors = new List<string>()
                    {
                        "Invalid tokens"
                    },
                    Result = false
                });
            }

            return Ok(result);
        }

        return BadRequest(new AuthToken()
        {
            Errors = new List<string>()
            {
                "Invalid parameters"
            },
            Result = false
        });
    }

    [HttpGet]
    [Authorize]
    public string GetTestMessage()
    {
        return "Authorised message";
    }

    private async Task<AuthToken> GenerateJwtToken(IdentityUser user)
    {
        var jwtTokenHandler = new JwtSecurityTokenHandler();

        // var key = Encoding.ASCII.GetBytes(_jwtConfig.Secret);
        var key = "hiUilgpplHyDpbGIujiSOTKHqKfmhppDLATiXlYlXNLzURpUPqkKEqGXLTUsyhQR"u8.ToArray();

        var tokenDescriptor = new SecurityTokenDescriptor()
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim("Id", user.Id),
                new Claim(JwtRegisteredClaimNames.Sub, user.Email),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Iat, DateTime.UtcNow.ToUniversalTime().ToString())
            }),
            // Issuer = _jwtConfig.Issuer,
            // Audience = _jwtConfig.Audience,
            Audience = "http://concert-meetup/api",
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha512)
        };

        var token = jwtTokenHandler.CreateToken(tokenDescriptor);
        var jwtToken = jwtTokenHandler.WriteToken(token);

        var refreshToken = new RefreshToken()
        {
            JwtId = token.Id,
            Token = RandomStringGeneration(23), // generate refresh token
            Created = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddMonths(6),
            IsRevoked = false,
            IsUsed = false,
            UserId = user.Id,
        };

        await _context.RefreshTokens.AddAsync(refreshToken);
        await _context.SaveChangesAsync();

        return new AuthToken()
        {
            Token = jwtToken,
            RefreshToken = refreshToken.Token,
            Result = true
        };
    }

    private async Task<AuthToken> VerifyAndGenerateToken(TokenRequest request)
    {
        var jwtTokenHandler = new JwtSecurityTokenHandler();

        try
        {
            _tokenValidationParameters.ValidateLifetime = false; // testing ? false : true

            var tokenInVerification = 
                jwtTokenHandler.ValidateToken(request.Token, _tokenValidationParameters, out var validatedToken);

            if (validatedToken is JwtSecurityToken jwtSecurityToken)
            {
                var result = jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha512, 
                    StringComparison.InvariantCultureIgnoreCase);
                if (!result)
                {
                    return null!;
                }
            }

            var utcExpiryDate = long.Parse(tokenInVerification.Claims
                .FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.Iat)!.Value);

            var expiryDate = UnixTimeStampToDateTime(utcExpiryDate);
            if (expiryDate > DateTime.UtcNow)
            {
                return new AuthToken()
                {
                    Result = false,
                    Errors = ["Expired token"]
                };
            }

            var storedToken = await _context.RefreshTokens
                .FirstOrDefaultAsync(t => t.Token == request.RefreshToken);
            if (storedToken == null)
            {
                return new AuthToken()
                {
                    Result = false,
                    Errors = ["Invalid token"]
                };
            }

            if (storedToken.IsUsed || storedToken.IsRevoked)
            {
                return new AuthToken()
                {
                    Result = false,
                    Errors = ["Invalid token"]
                };
            }

            var jti = tokenInVerification.Claims
                .FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.Jti)!.Value;

            if (storedToken.JwtId != jti)
            {
                return new AuthToken()
                {
                    Result = false,
                    Errors = ["Invalid token"]
                };
            }

            if (storedToken.ExpiryDate < DateTime.UtcNow)
            {
                return new AuthToken()
                {
                    Result = false,
                    Errors = ["Expired token"]
                };
            }

            storedToken.IsUsed = true;
            _context.RefreshTokens.Update(storedToken);
            await _context.SaveChangesAsync();

            var dbUser = await _userManager.FindByIdAsync(storedToken.UserId);
            if (dbUser != null) return await GenerateJwtToken(dbUser);
        }
        catch (Exception ex)
        {            
            _logger.LogError(ex, "Server error");

            return new AuthToken()
            {
                Result = false,
                Errors = ["Server error"]
            };
        }

        return null!;
    }

    private DateTime UnixTimeStampToDateTime(long unixTimeStamp)
    {
        var dateTimeVal = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        dateTimeVal = dateTimeVal.AddSeconds(unixTimeStamp).ToUniversalTime();
        return dateTimeVal;
    }

    private string RandomStringGeneration(int length)
    {
        var random = new Random();
        var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890abcdefghijklmnopqrstuvwxyz_";
        return new string(Enumerable.Repeat(chars, length).Select(s => s[random.Next(s.Length)]).ToArray());
    }
}