# 6.0.0

### Breaking Changes

* the Commit Dispatching was removed from NEventStore 6.0.0: remove the "Dispatched" field from your database schema (the projection need to track the last Checkpoint they processed).
* PostgreSQL: the 'Commits.CheckpointNumber' field type changed from SERIAL to BIGSERIAL, fix the schema in your database. [PostgreSQL Data Types](https://www.postgresql.org/docs/current/static/datatype-numeric.html#DATATYPE-INT).
