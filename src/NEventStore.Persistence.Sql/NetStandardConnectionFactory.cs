using Microsoft.Extensions.Logging;
using NEventStore.Logging;
using System.Data.Common;

namespace NEventStore.Persistence.Sql
{
	/// <summary>
	/// Represents a NetStandard connection factory.
	/// </summary>
	public class NetStandardConnectionFactory : IConnectionFactory
	{
		private static readonly ILogger Logger = LogFactory.BuildLogger(typeof(NetStandardConnectionFactory));

		private readonly DbProviderFactory _providerFactory;
		private readonly string _connectionString;

		/// <summary>
		/// Initializes a new instance of the <see cref="NetStandardConnectionFactory"/> class.
		/// </summary>
		public NetStandardConnectionFactory(DbProviderFactory providerFactory, string connectionString)
		{
			_providerFactory = providerFactory;
			_connectionString = connectionString;
		}
		/// <inheritdoc/>
		public Type GetDbProviderFactoryType()
		{
			return _providerFactory.GetType();
		}
		/// <inheritdoc/>
		public ConnectionScope Open()
		{
			if (Logger.IsEnabled(LogLevel.Trace))
			{
				Logger.LogTrace(Messages.OpeningMasterConnection, _connectionString);
			}
			return Open(_connectionString);
		}
		/// <summary>
		/// Opens a new connection.
		/// </summary>
		protected virtual ConnectionScope Open(string connectionString)
		{
			return new ConnectionScope(connectionString, () => OpenConnection(connectionString));
		}
		/// <summary>
		/// Opens a new connection.
		/// </summary>
		/// <exception cref="ConfigurationErrorsException"></exception>
		/// <exception cref="StorageUnavailableException"></exception>
		protected virtual DbConnection OpenConnection(string connectionString)
		{
			DbProviderFactory factory = _providerFactory;
			DbConnection connection = factory.CreateConnection()
				?? throw new ConfigurationErrorsException(Messages.BadConnectionName);

			connection.ConnectionString = connectionString;

			try
			{
				if (Logger.IsEnabled(LogLevel.Trace))
				{
					Logger.LogTrace(Messages.OpeningConnection, connectionString);
				}
				connection.Open();
			}
			catch (Exception e)
			{
				if (Logger.IsEnabled(LogLevel.Warning))
				{
					Logger.LogWarning(Messages.OpenFailed, connectionString);
				}
				throw new StorageUnavailableException(e.Message, e);
			}

			return connection;
		}
	}
}
