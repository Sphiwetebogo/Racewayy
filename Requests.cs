using System.ComponentModel.DataAnnotations;
using RaceDay.Api.Models;

namespace RaceDay.Api.Dtos;

public class RegisterRequest
{
    [Required, StringLength(100)] public string FullName { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(150)] public string Email { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 8)]
    [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).+$", ErrorMessage = "Password must contain at least one letter and one number.")]
    public string Password { get; set; } = string.Empty;

    /// <summary>1 = Organiser, 2 = Participant.</summary>
    [Range(1, 2, ErrorMessage = "RoleId must be 1 (Organiser) or 2 (Participant).")]
    public int RoleId { get; set; }

    /// <summary>Secret registration code issued by the system owner. Required only when RoleId = 1 (Organiser).</summary>
    public string? OrganiserCode { get; set; }
}

public class LoginRequest
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required] public string Password { get; set; } = string.Empty;
}

public class UpdateProfileRequest
{
    [Required, StringLength(100)] public string FullName { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(150)] public string Email { get; set; } = string.Empty;
}

public class EventRequest
{
    [Required, StringLength(150)] public string Title { get; set; } = string.Empty;
    [StringLength(4000)] public string? Description { get; set; }
    [Required] public DateTime? EventDate { get; set; }
    [Required, StringLength(150)] public string Location { get; set; } = string.Empty;
    [Range(0.1, 999.99, ErrorMessage = "DistanceKM must be between 0.1 and 999.99.")] public decimal DistanceKM { get; set; }
    /// <summary>Run, Walk or Cycle.</summary>
    [Required] public EventType? EventType { get; set; }
}

public class CategoryRequest
{
    [Required, StringLength(100)] public string CategoryName { get; set; } = string.Empty;
    [Range(0.01, 999.99, ErrorMessage = "DistanceKM must be greater than 0.")] public decimal DistanceKM { get; set; }
    [Range(0, 99999999.99, ErrorMessage = "Fee cannot be negative.")] public decimal Fee { get; set; }
}

public class EnrolRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "A valid categoryId is required.")] public int CategoryId { get; set; }
}

public class ResultRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "A valid enrolmentId is required.")] public int EnrolmentId { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "FinishTimeSeconds must be greater than 0.")] public int FinishTimeSeconds { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Position must be greater than 0.")] public int Position { get; set; }
}
