using ClaimTrack.Api.Entities;

namespace ClaimTrack.Api.Dtos.Auth;

public static class UserMappings
{
    public static UserResponse ToResponse(this User user) => new(
        user.Id,
        user.Email,
        user.FullName,
        user.Role,
        user.CreatedAt);
}
