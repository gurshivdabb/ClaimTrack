using ClaimTrack.Api.Common;
using ClaimTrack.Api.Data;
using ClaimTrack.Api.Dtos.Auth;
using ClaimTrack.Api.Entities;
using ClaimTrack.Api.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ClaimTrack.Api.Services;

public class AuthService(
    AppDbContext db, IPasswordHasher<User> passwordHasher, ITokenService tokenService, TimeProvider timeProvider)
    : IAuthService
{
    // The same message for an unknown email and a wrong password, so the API never
    // reveals which emails have accounts.
    private const string InvalidCredentials = "Invalid email or password.";

    // A real hash of a throwaway password, checked when the email is unknown. See LoginAsync.
    private static readonly string DummyPasswordHash =
        new PasswordHasher<User>().HashPassword(new User(), "not-a-real-password");

    public async Task<Result<UserResponse>> RegisterAsync(
        RegisterRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);

        // BR-11: emails are unique. The unique index on Users.Email is the safety net.
        var emailTaken = await db.Users.AnyAsync(u => u.Email == email, cancellationToken);
        if (emailTaken)
        {
            return Result<UserResponse>.Conflict("An account with this email already exists.");
        }

        var user = new User
        {
            Email = email,
            FullName = request.FullName.Trim(),
            Role = Role.Policyholder, // Requirements §2: public registration always creates a Policyholder.
            CreatedAt = timeProvider.GetUtcNow()
        };

        // BR-11: store only a salted hash, never the plain password.
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        return Result<UserResponse>.Ok(user.ToResponse());
    }

    public async Task<Result<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user is null)
        {
            // Checking a password is deliberately slow. Doing the same work here means an unknown
            // email takes as long as a wrong password, so timing can't reveal which emails exist.
            passwordHasher.VerifyHashedPassword(new User(), DummyPasswordHash, request.Password);
            return Result<LoginResponse>.Unauthorized(InvalidCredentials);
        }

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            return Result<LoginResponse>.Unauthorized(InvalidCredentials);
        }

        // The password is right, but the hash uses older settings (e.g. fewer iterations).
        // Re-hash it with the current settings while we have the plain password.
        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
            await db.SaveChangesAsync(cancellationToken);
        }

        return Result<LoginResponse>.Ok(tokenService.CreateToken(user));
    }

    // Stored emails are trimmed and lowercase, so every saved value looks the same
    // and login can compare them the same way.
    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
