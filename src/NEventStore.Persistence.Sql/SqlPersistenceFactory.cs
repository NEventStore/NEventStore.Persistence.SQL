namespace NEventStore.Persistence.Sql
{
	using System;
	using System.Configuration;
	using System.Transactions;
	using NEventStore.Persistence.Sql.SqlDialects;
	using NEventStore.Serialization;

	/// <summary>
	/// Represents a SQL persistence factory.
	/// </summary>
	public class SqlPersistenceFactory : IPersistenceFactory
	{
		private const int DefaultPageSize = 128;
		private readonly TransactionScopeOption? _scopeOption;

#if NET462
		/// <summary>
		/// Initializes a new instance of the <see cref="SqlPersistenceFactory"/> class.
		/// </summary>
		public SqlPersistenceFactory(
			string connectionName,
			ISerialize serializer,
			ISerializeEvents eventSerializer,
			ISqlDialect? dialect = null,
			TransactionScopeOption? scopeOption = null)
			: this(
				new ConfigurationConnectionFactory(connectionName),
				dialect ?? ResolveDialect(new ConfigurationConnectionFactory(connectionName).Settings),
				serializer, eventSerializer, scopeOption, null, DefaultPageSize)
		{ }
#endif

		/// <summary>
		/// Initializes a new instance of the <see cref="SqlPersistenceFactory"/> class.
		/// </summary>
		/// <exception cref="ArgumentNullException"></exception>
		public SqlPersistenceFactory(
			IConnectionFactory factory,
			ISerialize serializer,
			ISerializeEvents eventSerializer,
			ISqlDialect dialect,
			IStreamIdHasher? streamIdHasher = null,
			TransactionScopeOption? scopeOption = null,
			int pageSize = DefaultPageSize)
			: this(factory, dialect, serializer, eventSerializer, scopeOption, streamIdHasher, pageSize)
		{ }

		/// <summary>
		/// Initializes a new instance of the <see cref="SqlPersistenceFactory"/> class.
		/// </summary>
		private SqlPersistenceFactory(
			IConnectionFactory factory, ISqlDialect dialect,
			ISerialize serializer, ISerializeEvents eventSerializer,
			TransactionScopeOption? scopeOption, IStreamIdHasher? streamIdHasher, int pageSize)
		{
			ConnectionFactory = factory;
			Dialect = dialect;
			Serializer = serializer;
			EventSerializer = eventSerializer;
			_scopeOption = scopeOption;
			StreamIdHasher = streamIdHasher ?? new Sha1StreamIdHasher();
			PageSize = pageSize;
		}

		/// <summary>
		/// Gets the connection factory.
		/// </summary>
		protected virtual IConnectionFactory ConnectionFactory { get; }

		/// <summary>
		/// Gets the SQL dialect.
		/// </summary>
		protected virtual ISqlDialect Dialect { get; }

		/// <summary>
		/// Gets the serializer.
		/// </summary>
		protected virtual ISerialize Serializer { get; }

		/// <summary>
		/// Gets the event serializer.
		/// </summary>
		protected virtual ISerializeEvents EventSerializer { get; }

		/// <summary>
		/// Gets the stream ID hasher.
		/// </summary>
		protected virtual IStreamIdHasher StreamIdHasher { get; }

		/// <summary>
		/// Get or Set the page size.
		/// </summary>
		protected int PageSize { get; set; }

		/// <summary>
		/// Builds the persistence engine.
		/// </summary>
		public virtual IPersistStreams Build()
		{
			return new SqlPersistenceEngine(ConnectionFactory, Dialect, Serializer, EventSerializer, PageSize, StreamIdHasher, _scopeOption);
		}

#if NET462
		/// <summary>
		/// Resolves the SQL dialect based on the connection string settings.
		/// </summary>
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