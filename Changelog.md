# NEventStore.Persistence.Sql

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
