namespace NEventStore.Persistence.Sql
{
    using System;
    using System.Configuration;
    using System.Transactions;
    using NEventStore.Persistence.Sql.SqlDialects;
    using NEventStore.Serialization;

    public class SqlPersistenceFactory : IPersistenceFactory
    {
        private const int DefaultPageSize = 128;
        private readonly TransactionScopeOption _scopeOption;

#if !NETSTANDARD2_0
        public SqlPersistenceFactory(string connectionName, ISerialize serializer, ISqlDialect dialect = null)
            : this(serializer, TransactionScopeOption.Suppress, null, DefaultPageSize)
        {
            ConnectionFactory = new ConfigurationConnectionFactory(connectionName);
            Dialect = dialect ?? ResolveDialect(new ConfigurationConnectionFactory(connectionName).Settings);
        }
#endif

        public SqlPersistenceFactory(
            IConnectionFactory factory,
            ISerialize serializer,
            ISqlDialect dialect,
            IStreamIdHasher streamIdHasher = null,
            TransactionScopeOption scopeOption = TransactionScopeOption.Suppress,
            int pageSize = DefaultPageSize)
            : this(serializer, scopeOption, streamIdHasher, pageSize)
        {
            ConnectionFactory = factory;
            Dialect = dialect ?? throw new ArgumentNullException(nameof(dialect));
        }

        private SqlPersistenceFactory(ISerialize serializer, TransactionScopeOption scopeOption, IStreamIdHasher streamIdHasher, int pageSize)
        {
            Serializer = serializer;
            _scopeOption = scopeOption;
            StreamIdHasher = streamIdHasher ?? new Sha1StreamIdHasher();
            PageSize = pageSize;
        }

        protected virtual IConnectionFactory ConnectionFactory { get; }

        protected virtual ISqlDialect Dialect { get; }

        protected virtual ISerialize Serializer { get; }

        protected virtual IStreamIdHasher StreamIdHasher { get; }

        protected int PageSize { get; set; }

        public virtual IPersistStreams Build()
        {
            return new SqlPersistenceEngine(ConnectionFactory, Dialect, Serializer, _scopeOption, PageSize, StreamIdHasher);
        }

#if !NETSTANDARD2_0
        protected static ISqlDialect ResolveDialect(ConnectionStringSettings settings)
        {
            string providerName = settings.ProviderName.ToUpperInvariant();

            if (providerName.Contains("MYSQL"))
            {
                return new MySqlDialect();
            }

            if (providerName.Contains("SQLITE"))
            {
                return new SqliteDialect();
            }

            if (providerName.Contains("POSTGRES") || providerName.Contains("NPGSQL"))
            {
                return new PostgreSqlDialect();
            }

            if (providerName.Contains("ORACLE") && providerName.Contains("DATAACCESS"))
            {
                return new OracleNativeDialect();
            }

            if (providerName == "SYSTEM.DATA.ORACLECLIENT")
            {
                return new OracleNativeDialect();
            }

            return new MsSqlDialect();
        }
#endif
    }
}