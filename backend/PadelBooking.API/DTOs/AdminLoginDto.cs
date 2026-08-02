using System.ComponentModel.DataAnnotations;

namespace PadelBooking.API.DTOs;

public class AdminLoginDto
{
    [Required]
    [StringLength(100)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Password { get; set; } = string.Empty;
}
