// ReSharper disable once CheckNamespace
namespace NEventStore
{
	using NEventStore.Persistence.Sql;
	using System.Data.Common;

	/// <summary>
	/// Provides a set of extension methods to configure the SQL persistence engine.
	/// </summary>
	public static class SqlPersistenceWireupExtensions
	{
		// netstandard does not have support for DbFactoryProviders, we need a totally different way to initialize the driver
#if NET462
		/// <summary>
		/// Configures the persistence engine to use the specified connection string.
		/// </summary>
		public static SqlPersistenceWireup UsingSqlPersistence(this Wireup wireup, string connectionName)
		{
			var factory = new ConfigurationConnectionFactory(connectionName);
			return wireup.UsingSqlPersistence(factory);
		}

		/// <summary>
		/// Configures the persistence engine to use the specified connection string and provider.
		/// </summary>
		public static SqlPersistenceWireup UsingSqlPersistence(this Wireup wireup, string connectionName, string providerName, string connectionString)
		{
			var factory = new ConfigurationConnectionFactory(connectionName, providerName, connectionString);
			return wireup.UsingSqlPersistence(factory);
		}
#endif
		/// <summary>
		/// Configures the persistence engine to use the specified connection factory.
		/// </summary>
		public static SqlPersistenceWireup UsingSqlPersistence(this Wireup wireup, DbProviderFactory providerFactory, string connectionString)
		{
			var factory = new NetStandardConnectionFactory(providerFactory, connectionString);
			return wireup.UsingSqlPersistence(factory);
		}

		/// <summary>
		/// Configures the persistence engine to use the specified connection factory.
		/// </summary>
		public static SqlPersistenceWireup UsingSqlPersistence(this Wireup wireup, IConnectionFactory factory)
		{
#if NET462
			// init the global settings if needed
			if (int.TryParse(System.Configuration.ConfigurationManager.AppSettings["NEventStore.SqlCommand.Timeout"], out int timeout))
			{
				Settings.CommandTimeout = timeout;
			}
#endif
			return new SqlPersistenceWireup(wireup, factory);
		}

		/// <summary>
		/// Command timeout.
		/// </summary>
		public static SqlPersistenceWireup WithCommandTimeout(this SqlPersistenceWireup wireup, int commandTimeout)
		{
			Settings.CommandTimeout = commandTimeout;
			return wireup;
		}
	}
}