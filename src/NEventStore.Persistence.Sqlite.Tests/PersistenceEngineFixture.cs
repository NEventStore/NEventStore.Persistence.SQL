namespace NEventStore.Persistence.AcceptanceTests
{
    using NEventStore.Persistence.Sql;
    using NEventStore.Persistence.Sql.SqlDialects;
    using NEventStore.Serialization;

    public partial class PersistenceEngineFixture
    {
#if NET461
        public PersistenceEngineFixture()
        {
            _createPersistence = pageSize =>
                new SqlPersistenceFactory(
                    new ConfigurationConnectionFactory("NEventStore.Persistence.AcceptanceTests.Properties.Settings.SQLite"),
                    new BinarySerializer(),
                    new SqliteDialect(),
                    pageSize: pageSize).Build();
        }
#else        
        public PersistenceEngineFixture()
        {
            _createPersistence = pageSize =>
                new SqlPersistenceFactory(
                    new NetStandardConnectionFactory(System.Data.SQLite.SQLiteFactory.Instance, "Data Source=NEventStore.db;"),
                    new BinarySerializer(),
                    new SqliteDialect(),
                    pageSize: pageSize).Build();

            /*
             * There are issues with Microsoft.Data.Sqlite > 2.2.6
             */

            /*
            _createPersistence = pageSize =>
                new SqlPersistenceFactory(
                    new NetStandardConnectionFactory(Microsoft.Data.Sqlite.SqliteFactory.Instance, "Data Source=NEventStore.db;"),
                    new BinarySerializer(),
                    new MicrosoftDataSqliteDialect(),
                    pageSize: pageSize).Build();
            */
        }
#endif
    }
}