using System.Linq.Expressions;
using ClaimTrack.Api.Entities;

namespace ClaimTrack.Api.Dtos.Claims;

public static class AuditLogMappings
{
    // An expression rather than a method, so EF can turn it into SQL: the query selects only
    // these columns, with one join to Users for the actor's name and role.
    // A normal method would make EF load whole rows (including the actor's password hash).
    public static readonly Expression<Func<AuditLogEntry, AuditLogEntryResponse>> ToResponse =
        entry => new AuditLogEntryResponse(
            entry.Id,
            entry.Action,
            entry.FromStatus,
            entry.ToStatus,
            entry.Details,
            entry.Timestamp,
            entry.ActorUserId,
            entry.Actor.FullName,
            entry.Actor.Role);
}
