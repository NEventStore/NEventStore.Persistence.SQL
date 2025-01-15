using Microsoft.Data.SqlClient;
using NEventStore.Persistence.Sql.Tests;
using NEventStore.Persistence.Sql;
using NEventStore.Persistence.Sql.SqlDialects;
using NEventStore.Serialization.Binary;
using System.Transactions;

namespace NEventStore.Persistence.AcceptanceTests
{
	public partial class PersistenceEngineFixture
	{
		/// <summary>
		/// this mimic the current NEventStore default values which is run outside any transaction (creates a scope that
		/// suppresses any transaction)
		/// </summary>
		public TransactionScopeOption? ScopeOption { get; set; } =
			null; // the old default: TransactionScopeOption.Suppress;

		public PersistenceEngineFixture()
		{
#if NET8_0_OR_GREATER
			AppContext.SetSwitch("System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization", true);
#endif
			_createPersistence = pageSize =>
			{
				var serializer = new BinarySerializer();
				return new SqlPersistenceFactory(
					new EnvironmentConnectionFactory("MsSql", SqlClientFactory.Instance),
					serializer,
					new DefaultEventSerializer(serializer),
					new MsSqlDialect(),
					scopeOption: ScopeOption,
					pageSize: pageSize).Build();
			};
		}
	}
}