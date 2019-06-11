# NEventStore.Persistence.Sql

## 6.1.0

Enlist in ambient transaction was marked obsolete and removed from the main library.

All the transactions (or their suppression) must be managed by the user.

Enlist in ambient transaction was moved to the persistence drivers implementations, each driver has its own way to enable or disable the feature.

A new .net 4.5.1 compilation target was added to solve TransactionScope problems with the .net 4.5.0 (take a look at : [#377](https://github.com/NEventStore/NEventStore/issues/377) and [#414](https://github.com/NEventStore/NEventStore/issues/414)).

Under the net451 and netstandard2.0 compilation targets all the transactions will be created with the correct async/await support: TransactionScopeAsyncFlowOption.Enabled.

Warning: you should not use the built-in transaction support with async/await and net45.

Some more tests were added to the project to show working scenarios (see PersistenceTests.Trsancations.cs).

### Breaking Changes

The previous transaction management was deprecated and marked obsolete, all the transactions have to be handled manually by the user.

To revert to the previous behavior, configure the Persistence driver calling:

- SuppressAmbientTransaction(): this will restore the previous behavior of suppressing any active transaction.
- EnlistInAmbientTransaction(): will enlist the code in the external ambient transaction.

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
