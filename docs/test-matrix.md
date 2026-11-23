# ClaimTrack — Test Matrix

Which tests prove each business rule and each endpoint. Test names follow
`MethodName_Scenario_ExpectedResult`; search for a name to find the test.

- **Unit** tests are in `tests/ClaimTrack.UnitTests` (no database).
- **Integration** tests are in `tests/ClaimTrack.IntegrationTests` (the real API over HTTP, against SQL Server in Testcontainers).

Last checked: 2026-11-20. 458 tests (264 unit, 194 integration), 98.5% line and 92.2% branch coverage.

---

## Business rules

| Rule | Unit tests | Integration tests |
|---|---|---|
| **BR-01** Amount > 0 and ≤ coverage limit | `ClaimDetailsValidatorTests.Validate_ClaimedAmountNotPositive_HasError`, `Validate_ClaimedAmountOneCent_HasNoError`; `ClaimPolicyRulesTests.Check_AmountExactlyAtLimit_ReturnsNull`, `Check_AmountOneCentOverLimit_ReturnsCoverageLimitError` | `ClaimsEndpointsTests.Submit_AmountOverCoverageLimit_Returns422`, `Update_AmountOverCoverageLimit_Returns422` |
| **BR-02** Incident within policy dates, not in the future | `ClaimDetailsValidatorTests.Validate_IncidentToday_HasNoError`, `Validate_IncidentTomorrow_HasError`; `ClaimPolicyRulesTests.Check_IncidentOnStartDate_ReturnsNull` … `Check_IncidentDayAfterEnd_ReturnsDateError` | `ClaimsEndpointsTests.Submit_IncidentBeforePolicyStart_Returns422`, `Submit_InvalidInput_Returns400WithCamelCaseFieldNames` |
| **BR-03** Claim only against your own active policy | `VisibilityRulesTests.Policies_Policyholder_SeesOnlyOwn` (the filter `SubmitAsync` uses), plus BR-02 for "active" | `VisibilityTests.SubmitClaim_OnAnotherHoldersPolicy_Returns422PolicyNotFound`, `ClaimsEndpointsTests.Submit_UnknownPolicy_Returns422` |
| **BR-04** Only supervisors assign; assignee must be an adjuster | `ControllerSecurityTests.EveryProtectedAction_RequiresTheRoleFromTheRequirements`; `SeedClaimBuilderTests.Assign_ToNonAdjuster_Throws` | `AssignClaimTests.Assign_ToUserWhoIsNotAnAdjuster_Returns422`, `Assign_UnknownAdjuster_Returns422`; `AuthorizationTests.RoleRestrictedEndpoint_WrongRole_Returns403ProblemDetails` |
| **BR-05** Status changes follow the §4 table | `ClaimLifecycleTests.CanTransition_EveryPair_MatchesTheRequirementsTable` (all 25 pairs), `IsFinal_EachStatus_MatchesRequirements` | `ClaimWorkflowTests.StartReview_Twice_Returns409`, `Withdraw_ClaimUnderReview_Returns409`, `Decide_ClaimStillSubmitted_Returns409`, `EveryAction_OnFinalClaim_Returns409`; `AssignClaimTests.Assign_FinalClaim_Returns409` |
| **BR-06** Denial needs a reason of ≥ 10 characters | `WorkflowRequestValidatorTests.Decide_DenyWithoutAReasonOfTenCharacters_HasError`, `Decide_DenyWithTenCharacterReason_HasNoErrors` | `ClaimWorkflowTests.Decide_DenyWithShortReason_Returns400`, `Decide_DenyWithReason_EndsDeniedAndStoresReason` |
| **BR-07** Every change audited, in the same transaction | `ClaimEditDetailsTests.*` (the "what changed" details) | `AuditLogTests.FullJourney_RecordsEveryStepWithActorAndStatuses`, `Update_RecordsWhatChanged`, `RejectedActions_RecordNothing`, **`FailedAuditWrite_AlsoUndoesTheStatusChange`** |
| **BR-08** Audit log is append-only | `AppDbContextTests.SaveChanges_ModifiedAuditEntry_Throws`, `SaveChanges_DeletedAuditEntry_Throws` (+ async versions) | `AuditLogTests.ModifyingAnAuditEntry_Throws`, `DeletingAnAuditEntry_Throws` |
| **BR-09** Only see what your role allows; 404 otherwise | `VisibilityRulesTests.*` (9 tests) | `VisibilityTests.*`; `ClaimHistoryTests.History_OtherPolicyholderOrUnassignedAdjuster_Returns404`; `ClaimWorkflowTests.StartReview_AdjusterNotAssigned_Returns404`, `Recommend_AdjusterNotAssigned_Returns404`, `Withdraw_SomeoneElsesClaim_Returns404`; `DatabaseSeederTests.SeedAsync_DemoUser_SeesOnlyTheirOwnOrAssignedClaims` |
| **BR-10** Unique system-generated numbers | `NumberGeneratorTests.Format_PrefixYearAndValue_PadsToFiveDigits`, `Format_ValuePastFiveDigits_GrowsInsteadOfWrapping` | `ApiBehaviourTests.CreatePolicy_TenConcurrentRequests_GetDistinctNumbers`; `PoliciesEndpointsTests.Create_ValidPolicy_Returns201WithLocationAndPolicyNumber` |
| **BR-11** Unique emails (any case); passwords ≥ 8, hashed | `RegisterRequestValidatorTests.Validate_PasswordOfSevenCharacters_HasError`, `Validate_PasswordOfEightCharacters_HasNoError`; `UserMappingsTests.UserResponse_HasNoPasswordProperty` | `AuthEndpointsTests.Register_EmailAlreadyUsedWithDifferentCase_Returns409`, `Register_ValidRequest_StoresOnlyASaltedHash`; `UsersTableTests.CreateUser_EmailDifferingOnlyInCase_IsRejected` |
| **BR-12** Claims are never hard-deleted | `ControllerSecurityTests.ClaimsController_HasNoDeleteEndpoint` | `ClaimsEndpointsTests.Delete_Claim_Returns405AndClaimStillExists` (all 3 roles) |

---

## Endpoints

Every endpoint has at least one success and one failure integration test. Wrong-role (403)
cases for every restricted endpoint are in `AuthorizationTests.RoleRestrictedEndpoint_WrongRole_Returns403ProblemDetails`.

| Endpoint | Success | Failure |
|---|---|---|
| `POST /api/auth/register` | `Register_ValidRequest_Returns201AsPolicyholder` | `Register_InvalidInput_Returns400…`, `Register_EmailAlreadyUsedWithDifferentCase_Returns409` |
| `POST /api/auth/login` | `Login_CorrectCredentials_Returns200WithTokenForThatUser` | `Login_WrongPassword_Returns401`, `Login_UnknownEmail_Returns401WithTheSameMessage`, `Login_EmptyFields_Returns400` |
| `GET /api/policies` | `PoliciesEndpointsTests.GetAll_AfterCreatingPolicy_ContainsIt` | `AuthorizationTests.ProtectedEndpoint_NoToken_Returns401ProblemDetails` |
| `GET /api/policies/{id}` | `GetById_ExistingPolicy_Returns200` | `GetById_UnknownPolicy_Returns404ProblemDetails`, `VisibilityTests.Policyholder_OtherHoldersPolicyAndClaim_Return404` |
| `POST /api/policies` | `Create_ValidPolicy_Returns201WithLocationAndPolicyNumber` | `Create_InvalidPolicy_Returns400…`, `Create_UnknownHolder_Returns422`, `Create_HolderIsAnAdjuster_Returns422` |
| `PUT /api/policies/{id}` | `Update_ExistingPolicy_Returns200WithChanges` | `Update_InvalidPolicy_Returns400`, `Update_UnknownPolicy_Returns404` |
| `DELETE /api/policies/{id}` | `Delete_PolicyWithoutClaims_Returns204AndRemovesIt` | `Delete_PolicyWithClaims_Returns409`, `Delete_UnknownPolicy_Returns404` |
| `GET /api/claims` | `ClaimsEndpointsTests.GetAll_AfterSubmittingClaim_ContainsIt`; `ClaimListTests.List_FiveClaimsInPagesOfTwo_ReturnsTwoTwoOneThenEmpty`, `List_NoParameters_IsNewestFirst`, `List_FilterByStatus_…`, `List_SortByAmount_…`, `List_TotalCount_OnlyCountsClaimsTheUserCanSee` | `AuthorizationTests.ProtectedEndpoint_NoToken_Returns401ProblemDetails`; `ClaimListTests.List_InvalidParameter_Returns400NamingIt` (incl. page overflow) |
| `GET /api/claims/{id}` | `GetById_ExistingClaim_Returns200` | `GetById_UnknownClaim_Returns404` |
| `POST /api/claims` | `Submit_ValidClaim_Returns201AsSubmittedWithClaimNumber` | `Submit_InvalidInput_Returns400…`, `Submit_AmountOverCoverageLimit_Returns422`, `Submit_UnknownPolicy_Returns422` |
| `PUT /api/claims/{id}` | `Update_SubmittedClaim_Returns200WithChanges` | `Update_ClaimUnderReview_Returns409`, `Update_AmountOverCoverageLimit_Returns422`, `Update_UnknownClaim_Returns404` |
| `DELETE /api/claims/{id}` | — (removed, BR-12) | `Delete_Claim_Returns405AndClaimStillExists` |
| `POST /api/claims/{id}/assign` | `Assign_ToAdjuster_Returns200AndAdjusterCanSeeTheClaim` | `Assign_FinalClaim_Returns409`, `Assign_ToUserWhoIsNotAnAdjuster_Returns422`, `Assign_UnknownClaim_Returns404`, `Assign_EmptyAdjusterId_Returns400` |
| `POST /api/claims/{id}/start-review` | `FullJourney_SubmitAssignReviewRecommendApprove_EndsApproved` | `StartReview_Twice_Returns409`, `StartReview_AdjusterNotAssigned_Returns404` |
| `POST /api/claims/{id}/recommendation` | `FullJourney_…`, `Recommend_Twice_LatestReplacesEarlier` | `Recommend_ClaimStillSubmitted_Returns409`, `Recommend_AdjusterNotAssigned_Returns404`, `Recommend_MissingRecommendationAndNotes_Returns400` |
| `POST /api/claims/{id}/decision` | `FullJourney_…`, `Decide_DenyWithReason_EndsDeniedAndStoresReason` | `Decide_DenyWithShortReason_Returns400`, `Decide_WithoutRecommendation_Returns409`, `Decide_ClaimStillSubmitted_Returns409` |
| `POST /api/claims/{id}/withdraw` | `Withdraw_SubmittedClaim_EndsWithdrawn` | `Withdraw_ClaimUnderReview_Returns409`, `Withdraw_SomeoneElsesClaim_Returns404` |
| `POST /api/claims/{id}/documents` | `DocumentTests.Upload_PdfByOwner_Returns201ThenListsAndDownloadsTheSameBytes`, `Upload_AssignedAdjusterAndSupervisor_CanUpload`, `Upload_ContentTypeComesFromTheBytesNotTheUpload` | `Upload_ProgramRenamedToPdf_Returns400`, `Upload_EmptyOrMissingFile_Returns400`, `Upload_OverTheSizeLimit_Returns400`, `Upload_OverTheLimitPerClaim_Returns409`, `Upload_FinalClaim_Returns409`, `Documents_PeopleWhoCannotSeeTheClaim_Get404`, `Documents_NoToken_Returns401` |
| `GET /api/claims/{id}/documents` | `Upload_PdfByOwner_…`, `Upload_AssignedAdjusterAndSupervisor_CanUpload` | `Documents_PeopleWhoCannotSeeTheClaim_Get404` |
| `GET /api/claims/{id}/documents/{documentId}` | `Upload_PdfByOwner_…` (same bytes, `attachment`, `nosniff`) | `Documents_PeopleWhoCannotSeeTheClaim_Get404`, `Download_DocumentFromAnotherClaim_Returns404` |
| `GET /api/users?role=` (Supervisor) | `UsersEndpointsTests.List_AdjustersOnly_ReturnsAdjustersSortedByName`, `List_NoRole_ReturnsEveryRole`, `List_Response_HasNoEmailsOrPasswordData` | `List_NotASupervisor_Returns403`, `List_NoToken_Returns401`, `List_UnknownRole_Returns400NamingTheField` |
| `GET /api/claims/{id}/history` | `History_FullJourney_ListsEveryStepOldestFirst` | `History_OtherPolicyholderOrUnassignedAdjuster_Returns404`, `History_UnknownClaim_Returns404`, `History_NoToken_Returns401` |

---

## Logging

| Behaviour | Tests |
|---|---|
| One request summary line with method, path, status, `UserId`, `UserRole` | `LoggingTests.Request_LoggedIn_LogsOneSummaryWithUserRoleAndStatus`; `RequestLogEnricherTests.*` |
| Tokens, `Bearer` headers, and query strings never appear in any log event | `LoggingTests.Logs_NeverContainTheTokenOrTheQueryString` |
| Anonymous requests log no user | `LoggingTests.Request_Anonymous_LogsStatusButNoUser` |
| A client hanging up is a 499 at Information level, not a server error | `LoggingTests.ClientCancelledRequest_IsNotLoggedAsAServerError` |

---

## Startup and deployment

| Behaviour | How it's checked |
|---|---|
| `Database:MigrateOnStartup` creates every table in a new, empty database; off by default | `MigrateOnStartupTests.Startup_WithMigrateOnStartup_CreatesTheDatabaseWithEveryMigration`, `Startup_WithoutMigrateOnStartup_LeavesTheDatabaseAlone` |
| `GET /health` and `/health/live` answer `Healthy` without a token; with the database unreachable, `/health` is 503 `Unhealthy` (no details) while `/health/live` stays 200 | `HealthCheckTests.Health_DatabaseUp_Returns200HealthyWithoutAToken`, `Health_DatabaseDown_ReadyIs503ButLiveIsStill200` |
| Successful health probes don't appear in the Information-level log; failing ones are Errors | `HealthCheckTests.Health_SuccessfulProbe_IsNotLoggedAtInformation`; `RequestLogEnricherTests.GetLevel_PathAndStatus_PicksTheRightLevel` |
| A stopped SQL Server makes `/health` answer within ~3 s, not ~15 s | Manual run in Docker, 2026-11-20: three probes at 3.0–3.1 s each |
| Login/register over the limit get 429 Problem Details with `Retry-After`; separate budgets per IP | `RateLimitingTests.Login_OverTheLimit_Returns429WithRetryAfterAsProblemDetails`, `Login_DifferentIps_HaveSeparateLimits` |
| The general limit counts per user; health checks are never limited; invalid limits stop startup | `RateLimitingTests.GeneralLimit_OneUserOverTheLimit_DoesNotBlockAnotherUser`, `HealthChecks_AreNeverLimited`, `Startup_WithZeroLimit_FailsImmediately`; `RateLimitingOptionsTests.*` |
| Real limits in Docker: 5 wrong logins → 401, the 6th → 429 with `Retry-After: 10`; `/health` still 200 | Manual run, 2026-11-20 |
| Overdue claims: flagged from exactly 14 days after review start (not 13); a recommendation doesn't reset the clock; never re-flagged; Submitted and decided claims never flagged | `OverdueClaimsTests.Check_Reviewed15DaysAgoWithRecommendation_IsFlagged`, `Check_AroundTheThreshold_FlagsFrom14Days`, `Check_RunTwice_KeepsTheFirstFlagTime`, `Check_SubmittedOrDecidedClaims_AreNeverFlagged` |
| `?overdue=true/false` filters the claims list, within what the user may see (BR-09) | `OverdueClaimsTests.List_OverdueFilter_ReturnsOnlyFlaggedClaimsTheUserCanSee` |
| The background job flags claims on its own; a failing run is logged and the API keeps serving | `OverdueClaimsTests.Job_WhenEnabled_FlagsOverdueClaimsOnItsOwn`, `Job_WhenACheckFails_LogsTheErrorAndTheApiKeepsRunning`; `OverdueClaimsOptionsTests.*` |
| In Docker, the job flagged the 2 overdue demo claims at startup and logged a Warning | Manual run, 2026-11-20 |
| Upload security: file type from the bytes; names cleaned (path traversal); stored under random keys only; storage refuses non-key paths; no orphan file if the database save fails; uploads audited | `DocumentFileRulesTests.*`, `LocalFileStorageTests.*`; `DocumentTests.Upload_PathTraversalName_IsCleanedAndStoredUnderARandomName`, `Upload_DatabaseSaveFails_LeavesNoOrphanFile`, `Upload_IsRecordedInTheClaimHistory` |
| In Docker: the non-root container can write documents, a disguised program gets 400, and a document survives a full stack restart (volume) | Manual run, 2026-11-20 |
| The API refuses to start without a valid JWT key | `AuthEndpointsTests.Startup_WithoutSigningKey_FailsImmediately` |
| `docker compose up`: API waits for a healthy database, migrates, seeds once; restart adds nothing | Manual run, 2026-11-19: 2 starts, migrations applied once, demo data seeded once, demo logins see 10 / 5 / 3 claims both times |
| Container image: non-root, no shell, no source or secrets | Manual inspection of the exported image, 2026-11-19 |

---

## Code deliberately left uncovered

| Code | Why |
|---|---|
| `Program.cs` Development-only block (Swagger UI, demo seeding) | Tests run as "Testing" so they can never load user-secrets or touch the real database. Checked by running the app. |
| `ResultExtensions` final `throw` | Unreachable: every failed `ResultStatus` has a mapping. |
| `ValidationFilter` null-argument check | Defensive; MVC never passes a null body that reaches it. |
| `AppDbContext.SaveChanges()` success path | The app only uses `SaveChangesAsync`; the sync guard itself is unit-tested. |
| `RateLimitingSetup`: using a `Retry-After` reported by the limiter | Our sliding-window limiter never reports one; the calculated fallback is tested |
| `OverdueClaimsJob`: rethrowing cancellation on shutdown | Only happens while the host is stopping mid-check |
| `Claim.AssignedAdjuster` getter | Navigation property for EF's relationship; nothing reads it yet. |
