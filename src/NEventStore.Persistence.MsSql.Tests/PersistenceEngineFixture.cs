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
            _createPersistence = pageSize =>
                new SqlPersistenceFactory(new EnviromentConnectionFactory("MsSql", "System.Data.SqlClient"),
                    new BinarySerializer(),
                    new MsSqlDialect(),
                    pageSize: pageSize).Build();
        }
    }
}