using NEventStore.Persistence.Sql.Tests;

namespace NEventStore.Persistence.AcceptanceTests {
    using NEventStore.Persistence.Sql;
    using NEventStore.Persistence.Sql.SqlDialects;
    using NEventStore.Serialization;
    using System.Data.SqlClient;
    using System.Transactions;

    public partial class PersistenceEngineFixture {
        public ISqlDialect SqlDialect { get; set; } = new MsSqlDialect();

        /// <summary>
        /// this mimic the current NEventStore default values which is run outside any transaction (creates a scope that
        /// suppresses any transaction)
        /// </summary>
        public TransactionScopeOption? ScopeOption { get; set; }  // the old default: TransactionScopeOption.Suppress;

        public PersistenceEngineFixture()
        {
#if NET462
            _createPersistence = pageSize =>
                new SqlPersistenceFactory(new EnviromentConnectionFactory("MsSql", "System.Data.SqlClient"),
                    new BinarySerializer(),
                    SqlDialect,
                    pageSize: pageSize,
                    scopeOption: ScopeOption
                    ).Build();
#else
            _createPersistence = pageSize =>
                new SqlPersistenceFactory(new EnviromentConnectionFactory("MsSql", SqlClientFactory.Instance),
                    new BinarySerializer(),
                    SqlDialect,
                    pageSize: pageSize,
                    scopeOption: ScopeOption
                    ).Build();
#endif
        }
    }
}