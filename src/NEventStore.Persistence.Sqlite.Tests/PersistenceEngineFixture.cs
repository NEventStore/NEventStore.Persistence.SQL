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
                    new ConfigurationConnectionFactory("NEventStore.Persistence.AcceptanceTests.Properties.Settings.SQLite"),
                    new BinarySerializer(),
                    new SqliteDialect(),
                    pageSize: pageSize).Build();
#else
            _createPersistence = pageSize =>
                new SqlPersistenceFactory(
                    new NetStandardConnectionFactory(Microsoft.Data.Sqlite.SqliteFactory.Instance, "Data Source=NEventStore.db;"),
                    new BinarySerializer(),
                    new SqliteDialect(),
                    pageSize: pageSize).Build();
#endif
        }
    }
}