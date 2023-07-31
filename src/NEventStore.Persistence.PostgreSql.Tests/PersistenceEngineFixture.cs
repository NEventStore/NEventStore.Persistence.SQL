using NEventStore.Persistence.Sql.Tests;

namespace NEventStore.Persistence.AcceptanceTests
{
    using NEventStore.Persistence.Sql;
    using NEventStore.Persistence.Sql.SqlDialects;
    using NEventStore.Serialization;
    using System;

    public partial class PersistenceEngineFixture
    {
        public PersistenceEngineFixture()
        {
            // It will be done when creating the PostgreNpgsql6Dialect dialect
            // AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

#if NET462
            _createPersistence = pageSize =>
                new SqlPersistenceFactory(
                    new EnviromentConnectionFactory("PostgreSql", "Npgsql"),
                    new BinarySerializer(),
                    new PostgreNpgsql6Dialect(npgsql6timestamp: true),
                    pageSize: pageSize).Build();
#else
            _createPersistence = pageSize =>
                new SqlPersistenceFactory(
                    new EnviromentConnectionFactory("PostgreSql", Npgsql.NpgsqlFactory.Instance),
                    new BinarySerializer(),
                    new PostgreNpgsql6Dialect(npgsql6timestamp: true),
                    pageSize: pageSize).Build();
#endif
        }
    }
}