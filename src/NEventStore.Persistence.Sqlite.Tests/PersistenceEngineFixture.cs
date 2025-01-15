using NEventStore.Persistence.Sql;
using NEventStore.Persistence.Sql.SqlDialects;
using NEventStore.Serialization.Binary;

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
					new ConfigurationConnectionFactory(
						"NEventStore.Persistence.AcceptanceTests.Properties.Settings.SQLite"),
					serializer,
					new DefaultEventSerializer(serializer),
					new SqliteDialect(),
					pageSize: pageSize).Build();
			};
#else
			_createPersistence = pageSize =>
			{
				var serializer = new BinarySerializer();
				return new SqlPersistenceFactory(
					new NetStandardConnectionFactory(System.Data.SQLite.SQLiteFactory.Instance,
						"Data Source=NEventStore.db;"),
					serializer,
					new DefaultEventSerializer(serializer),
					new SqliteDialect(),
					pageSize: pageSize).Build();
			};

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
#endif
		}
	}
}