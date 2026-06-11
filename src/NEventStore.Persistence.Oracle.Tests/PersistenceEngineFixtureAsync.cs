using NEventStore.Persistence.Sql;
using NEventStore.Persistence.Sql.SqlDialects;
using NEventStore.Persistence.Sql.Tests;
using NEventStore.Serialization;
using NEventStore.Serialization.Binary;
using System.Transactions;

namespace NEventStore.Persistence.AcceptanceTests.Async
{
	public partial class PersistenceEngineFixtureAsync
	{
		/// <summary>
		/// this mimic the current NEventStore default values which is run outside any transaction (creates a scope that
		/// suppresses any transaction)
		/// </summary>
		public TransactionScopeOption? ScopeOption { get; set; } = null; // the old default: TransactionScopeOption.Suppress;

		public PersistenceEngineFixtureAsync()
		{
#if NET8_0_OR_GREATER
			AppContext.SetSwitch("System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization", true);
#endif
			var serializer = new BinarySerializer();
#if NET462_OR_GREATER
			_createPersistence = pageSize =>
				new SqlPersistenceFactory(
					new EnvironmentConnectionFactory("Oracle", "Oracle.ManagedDataAccess.Client"),
					serializer,
					new DefaultEventSerializer(serializer),
					new OracleNativeDialect(),
					scopeOption: ScopeOption,
					pageSize: pageSize).Build();
#else
			_createPersistence = pageSize =>
				new SqlPersistenceFactory(
					new EnvironmentConnectionFactory("Oracle", global::Oracle.ManagedDataAccess.Client.OracleClientFactory.Instance),
					serializer,
					new DefaultEventSerializer(serializer),
					new OracleNativeDialect(),
					pageSize: pageSize,
					scopeOption: ScopeOption
					).Build();
#endif
		}
	}
}