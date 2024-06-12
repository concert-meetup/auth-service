using System.ComponentModel.DataAnnotations;

namespace auth.API.DTO;

public class UserRegistrationRequestDto
{
    [Required]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    public string Password { get; set; } = string.Empty;
}