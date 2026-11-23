namespace ClaimTrack.IntegrationTests.Infrastructure;

// Test classes marked [Collection(ApiCollection.Name)] share one ClaimTrackApiFactory,
// so the SQL Server container starts once per run instead of once per class.
[CollectionDefinition(Name)]
public class ApiCollection : ICollectionFixture<ClaimTrackApiFactory>
{
    public const string Name = "ClaimTrack API";
}
