using NEventStore.Persistence.Sql.Tests;

namespace NEventStore.Persistence.AcceptanceTests
{
    using NEventStore.Persistence.Sql;
    using NEventStore.Persistence.Sql.SqlDialects;
    using NEventStore.Serialization;
#if NET462
    using System.Data.SqlClient;
#else
    using Microsoft.Data.SqlClient;
#endif
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
#if NET462
            _createPersistence = pageSize =>
                new SqlPersistenceFactory(new EnviromentConnectionFactory("MsSql", "System.Data.SqlClient"),
                    new BinarySerializer(),
                    new MsSqlDialect(),
                    pageSize: pageSize,
                    scopeOption: ScopeOption
                    ).Build();
#else
            _createPersistence = pageSize =>
                new SqlPersistenceFactory(new EnviromentConnectionFactory("MsSql", SqlClientFactory.Instance),
                    new BinarySerializer(),
                    new MsSqlDialect(),
                    pageSize: pageSize,
                    scopeOption: ScopeOption
                    ).Build();
#endif
        }
    }
}