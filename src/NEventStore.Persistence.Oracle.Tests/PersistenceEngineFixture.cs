using NEventStore.Persistence.Sql.Tests;

namespace NEventStore.Persistence.AcceptanceTests
{
    using NEventStore.Persistence.Sql;
    using NEventStore.Persistence.Sql.SqlDialects;
    using NEventStore.Serialization;
    using System.Transactions;

    public partial class PersistenceEngineFixture
    {
        /// <summary>
        /// this mimic the current NEventStore default values which is run outside any transaction (creates a scope that
        /// suppresses any transaction)
        /// </summary>
        public TransactionScopeOption? ScopeOption { get; set; } = null; // the old default: TransactionScopeOption.Suppress;

        public PersistenceEngineFixture()
        {
#if NET461
            _createPersistence = pageSize =>
                new SqlPersistenceFactory(
                    new EnviromentConnectionFactory("Oracle", "Oracle.ManagedDataAccess.Client"),
                    new BinarySerializer(),
                    new OracleNativeDialect(),
                    scopeOption: ScopeOption,
                    pageSize: pageSize).Build();
#else
            _createPersistence = pageSize =>
                new SqlPersistenceFactory(new EnviromentConnectionFactory("Oracle", global::Oracle.ManagedDataAccess.Client.OracleClientFactory.Instance),
                    new BinarySerializer(),
                    new OracleNativeDialect(),
                    pageSize: pageSize,
                    scopeOption: ScopeOption
                    ).Build();
#endif
        }
    }
}