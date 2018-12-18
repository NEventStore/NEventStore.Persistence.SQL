using NEventStore.Persistence.Sql.Tests;

namespace NEventStore.Persistence.AcceptanceTests
{
    using NEventStore.Persistence.Sql;
    using NEventStore.Persistence.Sql.SqlDialects;
    using NEventStore.Serialization;
    using System.Data.SqlClient;

    public partial class PersistenceEngineFixture
    {
        public PersistenceEngineFixture()
        {
#if !NETSTANDARD2_0
            _createPersistence = pageSize =>
                new SqlPersistenceFactory(new EnviromentConnectionFactory("MsSql", "System.Data.SqlClient"),
                    new BinarySerializer(),
                    new MsSqlDialect(),
                    pageSize: pageSize).Build();
#else
            _createPersistence = pageSize =>
                new SqlPersistenceFactory(new EnviromentConnectionFactory("MsSql", SqlClientFactory.Instance),
                    new BinarySerializer(),
                    new MsSqlDialect(),
                    pageSize: pageSize).Build();
#endif
        }
    }
}