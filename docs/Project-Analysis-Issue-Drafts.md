# Project Analysis: Bugs And Performance Actions

Date: 2026-06-11

Scope: `src/` was reviewed as the owned code. `dependencies/NEventStore` was used only for acceptance-test and contract context.

## Summary

Recommended issue order:

1. Fix cross-bucket contamination in `GetStreamsRequiringSnapshots`.
2. Fix async paged-query infinite loop when `PageSize == 0` and the result set is empty.
3. Fix Oracle `CommitStampStart` recursion.
4. Harden provider duplicate detection and null binary handling.
5. Replace offset/ROW_NUMBER checkpoint paging with keyset paging.
6. Review query resource lifetime for unenumerated synchronous results.
7. Remove SQL Server `SET ROWCOUNT` usage from snapshot/natural paging.
8. Narrow snapshot projections and align indexes with fixed snapshot query.

## 1. Fix Cross-Bucket Snapshot Candidate Query

Priority: P1 correctness and performance

Evidence:

- `src/NEventStore.Persistence.Sql/SqlDialects/CommonSqlStatements.resx:182`
- `GetStreamsRequiringSnapshots` joins snapshots with `ON C.BucketId = @BucketId` but does not filter `Commits` with `WHERE C.BucketId = @BucketId`.
- The same join also omits `S.BucketId = C.BucketId`.

Impact:

- `GetStreamsToSnapshot(bucketId, threshold)` can return streams from other buckets.
- Snapshots from another bucket with the same `StreamId` can change `SnapshotRevision`, hiding or creating snapshot candidates incorrectly.
- The query groups more rows than needed, so large multi-bucket stores pay unnecessary scan/group cost.

Action:

- Add `WHERE C.BucketId = @BucketId`.
- Change the snapshot join to include `S.BucketId = C.BucketId`.
- Keep the existing keyset paging predicate `C.StreamId > @StreamId`.
- Apply the same logical fix to Oracle SQL.

Verification:

- Add sync and async acceptance tests with same `StreamId` in buckets `a` and `b`.
- Assert `GetStreamsToSnapshot("a", ...)` never returns bucket `b` streams.
- Assert a snapshot in bucket `b` does not affect bucket `a` snapshot eligibility.

## 2. Fix Async Paged Query Infinite Loop For Empty Results

Priority: P1 correctness

Evidence:

- `src/NEventStore.Persistence.Sql/SqlDialects/CommonDbStatement.cs:331`
- Loop condition is `while (Dialect.CanPage && recordsRead == PageSize)`.
- `SqlPersistenceEngine` allows `pageSize == 0`, which means infinite page size.

Impact:

- With `PageSize == 0`, async paged reads over an empty result set loop forever for dialects where `CanPage == true`.
- Affects async APIs using `ExecutePagedQueryAsync`, including checkpoint reads and stream reads.

Action:

- Base continuation on the computed local `pageSize`, not the configured `PageSize`.
- Expected shape: continue only when `pageSize > 0 && recordsRead == pageSize`.

Verification:

- Add async tests for empty results with configured page size `0`.
- Cover `GetFromAsync(0, observer, token)` and a bucket-scoped read.

## 3. Fix Oracle `CommitStampStart` Recursion

Priority: P1 correctness

Evidence:

- `src/NEventStore.Persistence.Sql/SqlDialects/OracleNativeDialect.cs:55`
- Getter calls `MakeOracleParameter(CommitStampStart)`, which recursively invokes itself.

Impact:

- Oracle date-range reads that need `CommitStampStart` can stack overflow before executing SQL.

Action:

- Change to `MakeOracleParameter(base.CommitStampStart)`.

Verification:

- Add/enable Oracle coverage for `GetFromTo(bucketId, startDate, endDate)`.
- Add a small unit test directly against `new OracleNativeDialect().CommitStampStart`.

## 4. Harden Duplicate Detection And Null Binary Reads

Priority: P2 correctness

Evidence:

- `src/NEventStore.Persistence.Sql/SqlDialects/MySqlDialect.cs:43`
- MySQL duplicate detection reflects a `Number` property and immediately casts it.
- `src/NEventStore.Persistence.Sql/CommitExtensions.cs:87`
- `GetByteArray` returns `[default]` for `null`/`DBNull.Value`.

Impact:

- MySQL can mask non-provider exceptions with `NullReferenceException` or invalid casts during error handling.
- Nullable `Headers` values can be deserialized from one zero byte instead of returning default, which can fail on legacy rows or manually inserted rows.

Action:

- Guard MySQL `IsDuplicate` for missing or non-int `Number`.
- Prefer provider-specific exception checks where available.
- Return `[]` for `null` and `DBNull.Value` in `GetByteArray`.

Verification:

- Unit-test `MySqlDialect.IsDuplicate` against a generic exception.
- Add a commit materialization test for `Headers = NULL`.

## 5. Replace Offset Checkpoint Paging With Keyset Paging

Priority: P2 performance

Evidence:

- `src/NEventStore.Persistence.Sql/SqlPersistenceEngine.cs:347`
- Checkpoint reads pass a fixed checkpoint token.
- Common SQL uses `LIMIT @Limit OFFSET @Skip`.
- SQL Server transforms checkpoint reads to `ROW_NUMBER()` paging in `MsSqlDialect.CommonTableExpressionPaging`.

Impact:

- Long catch-up reads become increasingly expensive as `@Skip` grows.
- SQL Server recomputes row numbers for every page.
- This is a hot path for polling clients and projections.

Action:

- Page checkpoint reads by last seen `CheckpointNumber` instead of offset.
- Update sync and async paging delegates for checkpoint queries to set `@CheckpointNumber` to the last row.
- For range reads, keep `@ToCheckpointNumber` as the upper bound.

Verification:

- Add paging tests where result count exceeds page size.
- Add benchmark or integration timing for large checkpoint catch-up.
- Confirm ordered, gap-free results during multi-page reads.

## 6. Review Synchronous Query Resource Lifetime

Priority: P2 correctness/operability

Evidence:

- `src/NEventStore.Persistence.Sql/SqlPersistenceEngine.cs:460`
- `ExecuteQuery` opens connection/transaction/statement before returning the lazy enumerable.
- Disposal depends on the returned enumerable being enumerated and disposed.

Impact:

- Callers that create but do not enumerate a result can leak an open connection/command.
- This is easy to miss because most LINQ terminal operations dispose correctly.

Action:

- Consider making sync query methods iterator blocks that open resources on enumeration.
- Alternatively document the disposal contract and add analyzer/test coverage for common paths.

Verification:

- Add a fake connection/statement test proving no connection opens until enumeration starts, if implementation is changed.
- Add a test proving disposal on early termination.

## 7. Remove SQL Server `SET ROWCOUNT` Paging

Priority: P3 correctness/performance

Evidence:

- `src/NEventStore.Persistence.Sql/SqlDialects/MsSqlDialect.cs:21`
- `GetSnapshot` prepends `SET ROWCOUNT 1`.
- `NaturalPaging` uses `SET ROWCOUNT @Limit`.

Impact:

- `SET ROWCOUNT` is session-scoped behavior and easy to misuse.
- Snapshot reads can use `TOP (1)` instead.
- Natural paging can use dialect-specific `TOP (@Limit)` or keyset query shapes.

Action:

- Replace snapshot query with `SELECT TOP (1) ... ORDER BY StreamRevision DESC`.
- Replace natural paging with explicit `TOP (@Limit)` query forms.

Verification:

- SQL Server integration tests for `GetSnapshot` and `GetStreamsToSnapshot`.
- Confirm no rowcount setting affects later commands on the same connection.

## 8. Narrow Snapshot Projection And Index Review

Priority: P3 performance

Evidence:

- `src/NEventStore.Persistence.Sql/SqlDialects/CommonSqlStatements.resx:173`
- `GetSnapshot` uses `SELECT *`.
- Snapshot materialization needs `BucketId`, `StreamId`, `StreamRevision`, and `Payload`.

Impact:

- Low current blast radius because `Snapshots` has only four columns.
- Future schema additions could make this hot path read unnecessary data.

Action:

- Select explicit columns.
- After fixing `GetStreamsRequiringSnapshots`, review whether `IX_Snapshots_Stream_Revision` should include `BucketId` first or be replaced by `(BucketId, StreamId, StreamRevision)` depending on provider plans.

Verification:

- Existing snapshot acceptance tests should pass.
- Compare query plans before/after for SQL Server/PostgreSQL/MySQL/SQLite.

## Validation Plan

For implementation issues created from this document:

1. Add focused acceptance/unit tests first where possible.
2. Run nearest provider tests for the touched dialect.
3. Run `dotnet build ./src/NEventStore.Persistence.Sql.Core.sln -c Release --no-restore /p:ContinuousIntegrationBuild=true`.
4. Run `dotnet test ./src/NEventStore.Persistence.Sql.Core.sln -c Release --no-build` when DB prerequisites are available.

## Handoff Notes

### Issue #58: Oracle Support And Tests

Status: completed on 2026-06-11.

Oracle support and tests are back. The Oracle Docker setup was validated with the `gvenzl/oracle-xe` container exposed on `localhost:50005`, using:

```text
NEventStore.Oracle="Data Source=localhost:50005/XE;User Id=system;Password=Password1;Persist Security Info=True;"
```

Implementation notes:

- Modern .NET Oracle tests use `Oracle.ManagedDataAccess.Core`; `net472` keeps `Oracle.ManagedDataAccess`.
- Oracle payload binding now uses the active `OracleConnection` directly.
- Oracle parameter binding avoids unsupported provider `DbType` assignments and uses `DbType.DateTime` instead of `DbType.DateTime2`.
- Oracle snapshot candidate paging was fixed so `GetStreamsToSnapshot` behaves like the other providers.

Validation:

- `dotnet test .\src\NEventStore.Persistence.Oracle.Tests\NEventStore.Persistence.Oracle.Core.Tests.csproj -c Release --no-build -f net8.0`
- Result: 137 passed, 0 failed, 0 skipped.

### Issue #59: Cross-Bucket Snapshot Candidate Query

Status: completed on 2026-06-11.

Snapshot candidate queries now stay bucket-scoped. `GetStreamsRequiringSnapshots` filters `Commits` by the requested bucket and joins `Snapshots` by both bucket and stream id, preventing a snapshot in bucket `b` from hiding or changing eligibility for the same stream id in bucket `a`.

Implementation notes:

- Added sync and async regression tests under `src/NEventStore.Persistence.Sql.Tests/` and linked them into all provider test projects.
- The regression creates the same stream id in buckets `a` and `b`, adds a bucket `b` snapshot, and verifies bucket `a` still returns its snapshot candidate with `SnapshotRevision == 0`.
- Applied the SQL fix to common provider SQL and Oracle SQL now that Oracle support is available.

Validation:

- Test-first check: SQLite focused regression failed before the SQL fix, then passed after it.
- `dotnet test .\src\NEventStore.Persistence.Sqlite.Tests\NEventStore.Persistence.Sqlite.Core.Tests.csproj -c Release --no-build -f net8.0`
- Result: 141 passed, 0 failed, 0 skipped.
- `dotnet test .\src\NEventStore.Persistence.Oracle.Tests\NEventStore.Persistence.Oracle.Core.Tests.csproj -c Release -f net8.0 --no-build`
- Result: 141 passed, 0 failed, 0 skipped.
- `dotnet build .\src\NEventStore.Persistence.Sql.Core.sln -c Release --no-restore /p:ContinuousIntegrationBuild=true -m:1 -nr:false`
- Result: passed.

### Issue #60: Async Infinite Page Size Empty Reads

Status: completed locally on 2026-06-11. GitHub issue update is pending review.

Async paged reads now complete when the configured page size is `0`. For pageable dialects, `ExecutePagedQueryAsync` still binds the SQL limit parameter with an effectively unbounded value, but keeps the local paging size at `0` so continuation does not request another page.

Implementation notes:

- Added async regression coverage under `src/NEventStore.Persistence.Sql.Tests/PersistenceTests.InfinitePageSize.Async.cs` and linked it into all provider test projects.
- The regression initializes the provider fixture with page size `0` and verifies both `GetFromAsync(0, observer, token)` and `GetFromAsync(Bucket.Default, 0, observer, token)` complete over an empty store.
- The production fix is scoped to async paged reads in `CommonDbStatement`; sync paging behavior was left unchanged.

Validation:

- Test-first check: SQLite focused regression failed before the fix with missing `@Limit` binding for page size `0`, then passed after the fix.
- `dotnet test .\src\NEventStore.Persistence.Sqlite.Tests\NEventStore.Persistence.Sqlite.Core.Tests.csproj -c Release -f net8.0 --filter "FullyQualifiedName~when_reading_empty_async_pages_with_infinite_page_size"`
- Result: 2 passed, 0 failed, 0 skipped.
- `dotnet test .\src\NEventStore.Persistence.Sqlite.Tests\NEventStore.Persistence.Sqlite.Core.Tests.csproj -c Release --no-build -f net8.0`
- Result: 143 passed, 0 failed, 0 skipped.
- `dotnet test .\src\NEventStore.Persistence.Oracle.Tests\NEventStore.Persistence.Oracle.Core.Tests.csproj -c Release -f net8.0 --no-build --filter "FullyQualifiedName~when_reading_empty_async_pages_with_infinite_page_size"`
- Result: 2 passed, 0 failed, 0 skipped.
- `dotnet build .\src\NEventStore.Persistence.Sql.Core.sln -c Release --no-restore /p:ContinuousIntegrationBuild=true -m:1 -nr:false`
- Result: passed.
