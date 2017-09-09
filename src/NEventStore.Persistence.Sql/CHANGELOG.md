# netstandard

* HttpContext.Current is not available anymore, there are alternatives, but at the moment was removed from the ThreadScope class;
  it impacts: ConnectionScope, so connections might not be cached correctly in a web application.
* DbProviderFactories is not available in netstandard, we need to change how the configuration for the several data provider work:
  - we'll provide new coniguration methods explicitly target towards netstandard 2.0 that will require to provide the instance of the
    DbProviderFactory rather then the type (this is a quick workaround)

# 6.0.0

### Breaking Changes

* the Commit Dispatching was removed from NEventStore 6.0.0: remove the "Dispatched" field from your database schema (the projection need to track the last Checkpoint they processed).
* PostgreSQL: the 'Commits.CheckpointNumber' field type changed from SERIAL to BIGSERIAL, fix the schema in your database. [PostgreSQL Data Types](https://www.postgresql.org/docs/current/static/datatype-numeric.html#DATATYPE-INT).
