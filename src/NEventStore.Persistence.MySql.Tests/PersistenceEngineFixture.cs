using NEventStore.Persistence.Sql.Tests;
using NEventStore.Serialization.Binary;
using global::MySql.Data.MySqlClient;
using NEventStore.Persistence.Sql;
using NEventStore.Persistence.Sql.SqlDialects;

namespace NEventStore.Persistence.AcceptanceTests
{
    public partial class PersistenceEngineFixture
    {
        public PersistenceEngineFixture()
        {
#if NET8_0_OR_GREATER
            AppContext.SetSwitch("System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization", true);
#endif
#if NET462
            _createPersistence = pageSize =>
            {
                var serializer = new BinarySerializer();
                return new SqlPersistenceFactory(
                    new EnviromentConnectionFactory("MySql", "MySql.Data.MySqlClient"),
                    serializer,
                    new DefaultEventSerializer(serializer),
                    new MySqlDialect(),
                    pageSize: pageSize).Build();
            };

            // Wireup.Init().UsingSqlPersistence("test");
#else
            _createPersistence = pageSize =>
            {
                var serializer = new BinarySerializer();
                return new SqlPersistenceFactory(
                    new EnvironmentConnectionFactory("MySql", MySqlClientFactory.Instance),
                    serializer,
                    new DefaultEventSerializer(serializer),
                    new MySqlDialect(),
                    pageSize: pageSize).Build();
            };

            // Wireup.Init().UsingSqlPersistence("test");
#endif
        }
    }
}