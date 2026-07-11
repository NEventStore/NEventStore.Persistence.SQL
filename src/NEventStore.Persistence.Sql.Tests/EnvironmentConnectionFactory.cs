using System.Data.Common;
using System.Diagnostics;

namespace NEventStore.Persistence.Sql.Tests
{
	public class EnvironmentConnectionFactory : IConnectionFactory
	{
		private readonly string _envDatabaseName;
		private readonly string _envVarKey;
		private readonly DbProviderFactory _dbProviderFactory;

#if NET462_OR_GREATER
		public EnvironmentConnectionFactory(string envDatabaseName, string providerInvariantName)
		{
			_envDatabaseName = envDatabaseName;
			_envVarKey = string.Format("NEventStore.{0}", envDatabaseName);
			_dbProviderFactory = DbProviderFactories.GetFactory(providerInvariantName);
		}
#endif
		public EnvironmentConnectionFactory(string envDatabaseName, DbProviderFactory dbProviderFactory)
		{
			_envDatabaseName = envDatabaseName;
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
			var connection = CreateConnection();
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
			var connection = CreateConnection();
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

		private DbConnection CreateConnection()
		{
			var connectionString = Environment.GetEnvironmentVariable(_envVarKey, EnvironmentVariableTarget.Process);
			if (string.IsNullOrWhiteSpace(connectionString))
			{
				connectionString = EnvironmentFileConnectionString.TryBuild(_envDatabaseName);
			}

			if (string.IsNullOrWhiteSpace(connectionString))
			{
				throw new InvalidOperationException(
					string.Format(
						"Failed to get '{0}' and the selected .env file was not found. Start the database environment or define the existing connection-string environment variable.",
						_envVarKey));
			}

			var connection = _dbProviderFactory.CreateConnection();
			Debug.Assert(connection != null, "connection == null");
			connection!.ConnectionString = connectionString.TrimStart('"').TrimEnd('"');
			return connection;
		}
	}
}
