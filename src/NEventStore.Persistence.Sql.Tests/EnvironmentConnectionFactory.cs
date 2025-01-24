using System.Data.Common;
using System.Diagnostics;

namespace NEventStore.Persistence.Sql.Tests
{
	public class EnvironmentConnectionFactory : IConnectionFactory
	{
		private readonly string _envVarKey;
		private readonly DbProviderFactory _dbProviderFactory;

#if NET462
		public EnviromentConnectionFactory(string envDatabaseName, string providerInvariantName)
		{
			_envVarKey = string.Format("NEventStore.{0}", envDatabaseName);
			_dbProviderFactory = DbProviderFactories.GetFactory(providerInvariantName);
		}
#endif
		public EnvironmentConnectionFactory(string envDatabaseName, DbProviderFactory dbProviderFactory)
		{
			_envVarKey = string.Format("NEventStore.{0}", envDatabaseName);
			_dbProviderFactory = dbProviderFactory;
		}

		public ConnectionScope Open()
		{
			return new ConnectionScope("master", OpenInternal);
		}

		public async Task<ConnectionScope> OpenAsync(CancellationToken cancellationToken)
		{
			var connectionScope = new ConnectionScope("master", OpenInternalAsync);
			await connectionScope.InitAsync(cancellationToken).ConfigureAwait(false);
			return connectionScope;
		}

		public Type GetDbProviderFactoryType()
		{
			return _dbProviderFactory.GetType();
		}

		private DbConnection OpenInternal()
		{
			var connectionString = Environment.GetEnvironmentVariable(_envVarKey, EnvironmentVariableTarget.Process);
			if (connectionString == null)
			{
				string message =
					string.Format(
								  "Failed to get '{0}' environment variable. Please ensure " +
									  "you have correctly setup the connection string environment variables. Refer to the " +
									  "NEventStore wiki for details.",
						_envVarKey);
				throw new InvalidOperationException(message);
			}
			connectionString = connectionString.TrimStart('"').TrimEnd('"');
			var connection = _dbProviderFactory.CreateConnection();
			Debug.Assert(connection != null, "connection == null");
			connection!.ConnectionString = connectionString;
			try
			{
				connection.Open();
			}
			catch (Exception e)
			{
				throw new StorageUnavailableException(e.Message, e);
			}
			return connection;
		}

		private async Task<DbConnection> OpenInternalAsync(CancellationToken cancellationToken)
		{
			var connectionString = Environment.GetEnvironmentVariable(_envVarKey, EnvironmentVariableTarget.Process);
			if (connectionString == null)
			{
				string message =
					string.Format(
								  "Failed to get '{0}' environment variable. Please ensure " +
									  "you have correctly setup the connection string environment variables. Refer to the " +
									  "NEventStore wiki for details.",
						_envVarKey);
				throw new InvalidOperationException(message);
			}
			connectionString = connectionString.TrimStart('"').TrimEnd('"');
			var connection = _dbProviderFactory.CreateConnection();
			Debug.Assert(connection != null, "connection == null");
			connection!.ConnectionString = connectionString;
			try
			{
				await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
			}
			catch (Exception e)
			{
				throw new StorageUnavailableException(e.Message, e);
			}
			return connection;
		}
	}
}