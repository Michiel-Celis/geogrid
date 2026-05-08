using System.ComponentModel.DataAnnotations;

namespace Geogrid.Api.Contracts;

public record RegisterRequest(
    [Required, EmailAddress] string Email,
    [Required, MinLength(8)] string Password);

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record AuthResponse(string Token, DateTimeOffset ExpiresAt, Guid UserId, string Email);

public record CreateProjectRequest(
    [Required, MaxLength(200)] string Name,
    [MaxLength(2000)] string? Description,
    [Range(1024, 999999)] int Srid,
    [Range(-90, 90)] double CenterLat,
    [Range(-180, 180)] double CenterLon);

public record UpdateProjectRequest(
    [Required, MaxLength(200)] string Name,
    [MaxLength(2000)] string? Description,
    [Range(1024, 999999)] int Srid,
    [Range(-90, 90)] double CenterLat,
    [Range(-180, 180)] double CenterLon);

public record ProjectResponse(
    Guid Id,
    string Name,
    string? Description,
    int Srid,
    double CenterLat,
    double CenterLon,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
