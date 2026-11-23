using ClaimTrack.Api.Data;
using ClaimTrack.Api.Dtos.Users;
using ClaimTrack.Api.Enums;
using Microsoft.EntityFrameworkCore;

namespace ClaimTrack.Api.Services;

// Lets a supervisor pick an adjuster to assign, or a policyholder for a new policy. Supervisor-only
// (UsersController), and it returns only ID, name, and role: the minimum needed to choose someone.
public class UserService(AppDbContext db) : IUserService
{
    public async Task<List<UserSummaryResponse>> GetAllAsync(Role? role, CancellationToken cancellationToken)
    {
        var users = db.Users.AsNoTracking();
        if (role is not null)
        {
            users = users.Where(u => u.Role == role);
        }

        // Projected in SQL, so password hashes and emails are never even loaded.
        return await users
            .OrderBy(u => u.FullName)
            .ThenBy(u => u.Id)
            .Select(u => new UserSummaryResponse(u.Id, u.FullName, u.Role))
            .ToListAsync(cancellationToken);
    }
}
