using ClaimTrack.Api.Enums;

namespace ClaimTrack.Api.Dtos.Users;

// Just enough to pick a person in the UI: never the email, password hash, or anything else.
public record UserSummaryResponse(Guid Id, string FullName, Role Role);
