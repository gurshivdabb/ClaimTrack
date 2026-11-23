using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClaimTrack.Api.Common;

// The one place the API's JSON rules are defined. Program.cs applies them to both
// the controllers and the OpenAPI document, so the docs always match the real API.
public static class JsonSettings
{
    public static void Apply(JsonSerializerOptions options)
    {
        // Enums are sent and received as text ("Auto"), matching how they are stored in the database.
        options.Converters.Add(new JsonStringEnumConverter());
    }
}
