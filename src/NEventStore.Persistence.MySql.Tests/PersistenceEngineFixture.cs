using NEventStore.Persistence.Sql.Tests;

namespace NEventStore.Persistence.AcceptanceTests
{
    using global::MySql.Data.MySqlClient;
    using NEventStore.Persistence.Sql;
    using NEventStore.Persistence.Sql.SqlDialects;
    using NEventStore.Serialization;

    public partial class PersistenceEngineFixture
    {
        public PersistenceEngineFixture()
        {
#if NET462
            _createPersistence = pageSize =>
                    new SqlPersistenceFactory(
                        new EnviromentConnectionFactory("MySql", "MySql.Data.MySqlClient"),
                        new BinarySerializer(),
                        new MySqlDialect(),
                        pageSize: pageSize).Build();

            // Wireup.Init().UsingSqlPersistence("test");
#else
            _createPersistence = pageSize =>
                new SqlPersistenceFactory(
                    new EnviromentConnectionFactory("MySql", MySqlClientFactory.Instance),
                    new BinarySerializer(),
                    new MySqlDialect(),
                    pageSize: pageSize).Build();

            // Wireup.Init().UsingSqlPersistence("test");
#endif
        }
    }
}