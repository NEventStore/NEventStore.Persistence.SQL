using Microsoft.Data.SqlClient;
using NEventStore.Persistence.Sql.Tests;

namespace NEventStore.Persistence.AcceptanceTests
{
	using NEventStore.Persistence.Sql;
	using NEventStore.Persistence.Sql.SqlDialects;
	using NEventStore.Serialization;
	using NEventStore.Serialization.Binary;
	using System.Transactions;

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
			_createPersistence = pageSize =>
			{
				var serializer = new BinarySerializer();
				return new SqlPersistenceFactory(
					new EnviromentConnectionFactory("MsSql", SqlClientFactory.Instance),
					serializer,
					new DefaultEventSerializer(serializer),
					new MsSqlDialect(),
					pageSize: pageSize,
					scopeOption: ScopeOption
				).Build();
			};
		}
	}
}