using NEventStore.Persistence.Sql.Tests;

namespace NEventStore.Persistence.AcceptanceTests
{
    using NEventStore.Persistence.Sql;
    using NEventStore.Persistence.Sql.SqlDialects;
    using NEventStore.Serialization;

    public partial class PersistenceEngineFixture
    {
        public PersistenceEngineFixture()
        {
#if !NETSTANDARD2_0
            _createPersistence = pageSize =>
                new SqlPersistenceFactory(
                    new EnviromentConnectionFactory("PostgreSql", "Npgsql"),
                    new BinarySerializer(),
                    new PostgreSqlDialect(),
                    pageSize: pageSize).Build();
#else
            _createPersistence = pageSize =>
                new SqlPersistenceFactory(
                    new EnviromentConnectionFactory("PostgreSql", Npgsql.NpgsqlFactory.Instance),
                    new BinarySerializer(),
                    new PostgreSqlDialect(),
                    pageSize: pageSize).Build();
#endif
        }
    }
}