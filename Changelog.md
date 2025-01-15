# NEventStore.Persistence.Sql

## vNext

- async methods

### Breaking Changes

- `ISerializeEvents.SerializeEventMessages()` signature changed to accept `IEnumerable` instead of `IReadOnlyList`.

## 9.3.1

- Added a dedicated `ISerializeEvents` interface that allows customizing event deserialization with access to metadata from the `ICommit` class. [#47](https://github.com/NEventStore/NEventStore.Persistence.SQL/issues/47), [#49](https://github.com/NEventStore/NEventStore.Persistence.SQL/issues/49), [#506](https://github.com/NEventStore/NEventStore/issues/506)
- Improved some Comments and Documentation.
- Updated Microsoft.Data.SqlClient to version 5.2.2
- Updated nuget package to include symbols and new icon.

Thanks to: [@chrischu](https://github.com/chrischu)

## 9.2.0

- Using Microsoft.Data.SqlClient instead of System.Data.SqlClient [#40](https://github.com/NEventStore/NEventStore.Persistence.SQL/issues/40)

### Breaking Change

- System.Data.SqlClient is not supported anymore.
- To properly update to Microsoft.Data.SqlClient follow the guirelines in [SqlClient porting cheat sheet](https://github.com/dotnet/SqlClient/blob/main/porting-cheat-sheet.md).

## 9.1.2

- fixed .nuspec file (wrong net462 references)

## 9.1.1

- Target Framework supported: netstandard2.0, net462
- Updated System.Data.SqlClient 4.8.5
- Fix: NEventStore constraint failed with MySql 8.x (works with 5.7) [#487](https://github.com/NEventStore/NEventStore/issues/487)

### Breaking Change

- The fix for [#487](https://github.com/NEventStore/NEventStore/issues/487) changed how the `Commits` table is created for MySql 8.x:
  to update an existing database in order to run on 8.x you need to manually update the `Commits` table schema and change the constraint of the `CommitId` column
  from: `CommitId binary(16) NOT NULL CHECK (CommitId != 0)` to: `CommitId binary(16) NOT NULL CHECK (CommitId <> 0x00)`.

## 9.0.1 

- Added documentation files to NuGet packages (improved intellisense support) [#36](https://github.com/NEventStore/NEventStore.Persistence.SQL/issues/36)
- Updated NEventStore reference to 9.0.1
- Commit order of commits with same timestamp is random when using GetFrom/GetFromTo with a DateTime [#35](https://github.com/NEventStore/NEventStore.Persistence.SQL/issues/35)

## 9.0.0

- Updated NEventStore core library to 9.0.0.
- Added support for net6.0.
- Added a new PostgreSQL dialect (`PostgreNpgsql6Dialect`) to deal with Npgsql version 6 timestamp breaking changes. If you update the driver you might need to update the table schema manually [#34](https://github.com/NEventStore/NEventStore.Persistence.SQL/issues/34).

## 8.0.0

- Updated NEventStore core library to 8.0.0.
- Supports net5.0, net4.6.1.
- Schema initialization does not work with case insensitive database collations [#27](https://github.com/NEventStore/NEventStore.Persistence.SQL/issues/27)
- Use 32bit integer for the items column on the Commits table [#15](https://github.com/NEventStore/NEventStore.Persistence.SQL/pull/15)
- SqlPersistenceEngine, commit stamps are only saved with datetime precision, even though DB field is datetime2 [#1](https://github.com/NEventStore/NEventStore.Persistence.SQL/issues/1), [#399](https://github.com/NEventStore/NEventStore/issues/399)
- Added docker files to have test environments up and running in minutes.

### Breaking Changes

- Dropped net45, net451.
- Created a new dialect (MicrosoftDataSqliteDialect) to support Microfot.Data.Sqlite up to version 2.2.6, there are issues with versions 3.x and 5.x.
  System.Data.Sqlite is supported by the default SqliteDialect.
- [Commits] table structure has been changed due to [#15](https://github.com/NEventStore/NEventStore.Persistence.SQL/pull/15), you'll need to update it manually: 
  the [Items] column has been modified:
    - MsSql from 'tinyint' to 'int'.
    - MySql from 'tinyint' to 'int'.
    - PostgreSql from 'smallint' to 'int'.
- the default SqlDialects now use DbType.DateTime2 to store dates: `ISqlDialect.GetDateTimeDbType()`, if you need something else override the default dialects and adapt them to your needs. 

## 7.2.0

Fixes incorrect NEventStore reference, assembly version numbers and NuGet package [#30](https://github.com/NEventStore/NEventStore.Persistence.SQL/issues/30)

Please do not use the previous NEventStore.Persistence.Sql 7.1.0 because the package is broken.
Update to version 7.2.0 as soon as possible.

## 7.1.0

- Updated NEventStore core library to 7.0.0 (previous 7.0.0 version was still referencing NEventStore version 6.x).
- Updated the Persistence.Engine to implement new IPersistStreams.GetFromTo interface methods.

## 7.0.0

The default behavior when it comes to ambient transaction has been changed and it will impact mainly Microsoft SQL Server users:

- Enlist in ambient transaction has been removed from the main library.
- All the transactions (or their suppression) should be managed by the user, by default NEventStore will not automatically create any TransactionScope anymore.
- Enlist in ambient transaction was moved to the persistence drivers implementations, each driver has its own way to enable or disable the feature (the current driver implementation supports only Microsoft SQL Server).

For more considerations and an indepth discussion on transaction support in NEventStore take a look at 'Prevent writing to > 1 stream in a transaction' [#287](https://github.com/NEventStore/NEventStore/issues/287)

A new .net 4.5.1 compilation target was added to solve TransactionScope issues with the .net 4.5.0 (take a look at : [#377](https://github.com/NEventStore/NEventStore/issues/377) and [#414](https://github.com/NEventStore/NEventStore/issues/414)).

Under net451 and netstandard2.0 compilation targets all the transactions will be created with the correct async/await support: TransactionScopeAsyncFlowOption.Enabled.

Warning: you should not use the built-in transaction support with async/await in net45 projects.

Some more tests were added to the project to show working scenarios (see PersistenceTests.Trsancations.cs).

### Breaking Changes

The previous transaction management was disabled, all the transactions should be handled manually by the user.

To revert to the previous behavior, configure the Persistence driver calling:

- SuppressAmbientTransaction(): this will restore the previous behavior of suppressing any active transaction; every operation will be surrounded by a private nested TransactionScope with TransactionScopeOption.Suppress, so that any NEventStore code will run in a seperated transaction.
- EnlistInAmbientTransaction(): will enlist the code in the external ambient transaction (if it exists), or it will create a new TransactionScope for the operation (same behavior as before).

For more considerations and an indepth discussion on transaction support in NEventStore take a look at 'Prevent writing to > 1 stream in a transaction' [#287](https://github.com/NEventStore/NEventStore/issues/287)

## 6.0.0

__Version 6.x is not backwards compatible with version 5.x.__ Updating to NEventStore 6.x without doing some preparation work will result in problems.

Please take a look at all the previous 6.x release notes.

## 6.0.0-rc-0

__Version 6.x is not backwards compatible with version 5.x.__ Updating to NEventStore 6.x without doing some preparation work will result in problems.

### Breaking Changes

- the Commit Dispatching logic has been removed from NEventStore 6.0.0: remove the "Dispatched" field from your database schema (the projections need to track the last Checkpoint they processed).
- PostgreSQL: the 'Commits.CheckpointNumber' field type changed from SERIAL to BIGSERIAL, fix the schema in your database. [PostgreSQL Data Types](https://www.postgresql.org/docs/current/static/datatype-numeric.html#DATATYPE-INT).

**netstandard**

- HttpContext.Current is not available anymore, there are alternatives, but at the moment it has been removed from the ThreadScope class;
  it impacts: ConnectionScope, so connections might not be cached correctly in a web application.
- DbProviderFactories is not available in netstandard, we need to change how the configuration for the several data providers work:
  - we'll provide new coniguration methods explicitly targetted towards netstandard 2.0 that will require to provide the instance of the
    DbProviderFactory rather then the type.