using System.Data;
using System.Data.Common;
using System.Transactions;
using Microsoft.Extensions.Logging;
using NEventStore.Logging;

namespace NEventStore.Persistence.Sql.SqlDialects
{
	/// <summary>
	/// Common implementation of <see cref="IDbStatement"/> that provides basic functionality for executing SQL commands.
	/// </summary>
	public class CommonDbStatement : IDbStatement
	{
		private const int InfinitePageSize = 0;
		private static readonly ILogger Logger = LogFactory.BuildLogger(typeof(CommonDbStatement));
		private readonly ConnectionScope _connection;
		private readonly TransactionScope? _scope;
		private readonly DbTransaction? _transaction;

		/// <summary>
		/// Initializes a new instance of the <see cref="CommonDbStatement"/> class.
		/// </summary>
		public CommonDbStatement(
			ISqlDialect dialect,
			TransactionScope? scope,
			ConnectionScope connection,
			DbTransaction? transaction)
		{
			Parameters = new Dictionary<string, Tuple<object, DbType?>>();

			Dialect = dialect;
			_scope = scope;
			_connection = connection;
			_transaction = transaction;
		}

		/// <summary>
		/// Parameters to be used in the command.
		/// </summary>
		protected IDictionary<string, Tuple<object, DbType?>> Parameters { get; }

		/// <summary>
		/// SQL dialect to be used.
		/// </summary>
		protected ISqlDialect Dialect { get; }

		/// <inheritdoc/>
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <inheritdoc/>
		public virtual int PageSize { get; set; }

		/// <inheritdoc/>
		public virtual void AddParameter(string name, object value, DbType? parameterType = null)
		{
			if (Logger.IsEnabled(LogLevel.Debug))
			{
				Logger.LogDebug(Messages.AddingParameter, name);
			}
			Parameters[name] = Tuple.Create(Dialect.CoalesceParameterValue(value), parameterType);
		}

		/// <inheritdoc/>
		public virtual int ExecuteWithoutExceptions(string commandText)
		{
			try
			{
				return ExecuteNonQuery(commandText);
			}
			catch (Exception)
			{
				if (Logger.IsEnabled(LogLevel.Debug))
				{
					Logger.LogDebug(Messages.ExceptionSuppressed);
				}
				return 0;
			}
		}

		/// <inheritdoc/>
		public virtual async Task<int> ExecuteWithoutExceptionsAsync(string commandText, CancellationToken cancellationToken)
		{
			try
			{
				return await ExecuteNonQueryAsync(commandText, cancellationToken).ConfigureAwait(false);
			}
			catch (Exception)
			{
				if (Logger.IsEnabled(LogLevel.Debug))
				{
					Logger.LogDebug(Messages.ExceptionSuppressed);
				}
				return 0;
			}
		}

		/// <inheritdoc/>
		public virtual int ExecuteNonQuery(string commandText)
		{
			try
			{
				using (var command = BuildCommand(commandText))
				{
					return command.ExecuteNonQuery();
				}
			}
			catch (Exception e)
			{
				if (Dialect.IsDuplicate(e))
				{
					throw new UniqueKeyViolationException(e.Message, e);
				}

				throw;
			}
		}

		/// <inheritdoc/>
		public virtual async Task<int> ExecuteNonQueryAsync(string commandText, CancellationToken cancellationToken)
		{
			try
			{
				using (var command = BuildCommand(commandText))
				{
					return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
				}
			}
			catch (Exception e)
			{
				if (Dialect.IsDuplicate(e))
				{
					throw new UniqueKeyViolationException(e.Message, e);
				}

				throw;
			}
		}

		/// <inheritdoc/>
		public virtual object ExecuteScalar(string commandText)
		{
			try
			{
				using (var command = BuildCommand(commandText))
				{
					return command.ExecuteScalar();
				}
			}
			catch (Exception e)
			{
				if (Dialect.IsDuplicate(e))
				{
					throw new UniqueKeyViolationException(e.Message, e);
				}
				throw;
			}
		}

		/// <inheritdoc/>
		public virtual async Task<object> ExecuteScalarAsync(string commandText, CancellationToken cancellationToken)
		{
			try
			{
				using (var command = BuildCommand(commandText))
				{
					return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
				}
			}
			catch (Exception e)
			{
				if (Dialect.IsDuplicate(e))
				{
					throw new UniqueKeyViolationException(e.Message, e);
				}
				throw;
			}
		}

		/// <inheritdoc/>
		public virtual IEnumerable<IDataRecord> ExecuteWithQuery(string queryText)
		{
			return ExecuteQuery(queryText, (_, _) => { }, InfinitePageSize);
		}

		/// <inheritdoc/>
		public virtual IEnumerable<IDataRecord> ExecutePagedQuery(string queryText, NextPageDelegate nextPage)
		{
			int pageSize = Dialect.CanPage ? PageSize : InfinitePageSize;
			if (pageSize > 0)
			{
				if (Logger.IsEnabled(LogLevel.Trace))
				{
					Logger.LogTrace(Messages.MaxPageSize, pageSize);
				}
				Parameters.Add(Dialect.Limit, Tuple.Create((object)pageSize, (DbType?)null));
			}

			return ExecuteQuery(queryText, nextPage, pageSize);
		}

		/// <inheritdoc/>
		protected virtual void Dispose(bool disposing)
		{
			if (Logger.IsEnabled(LogLevel.Trace))
			{
				Logger.LogTrace(Messages.DisposingStatement);
			}

			_transaction?.Dispose();

			_connection?.Dispose();

			_scope?.Dispose();
		}

		/// <inheritdoc/>
		protected virtual IEnumerable<IDataRecord> ExecuteQuery(string queryText, NextPageDelegate nextPage, int pageSize)
		{
			Parameters.Add(Dialect.Skip, Tuple.Create((object)0, (DbType?)null));
			IDbCommand command = BuildCommand(queryText);

			try
			{
				return new PagedEnumerationCollection(_scope, Dialect, command, nextPage, pageSize, this);
			}
			catch (Exception)
			{
				command.Dispose();
				throw;
			}
		}

		/// <summary>
		/// Builds a command to be executed.
		/// </summary>
		protected virtual DbCommand BuildCommand(string statement)
		{
			if (Logger.IsEnabled(LogLevel.Trace))
			{
				Logger.LogTrace(Messages.CreatingCommand);
			}
			DbCommand command = _connection.Current.CreateCommand();

			if (Settings.CommandTimeout > 0)
			{
				command.CommandTimeout = Settings.CommandTimeout;
			}

			command.Transaction = _transaction;
			command.CommandText = statement;

			if (Logger.IsEnabled(LogLevel.Trace))
			{
				Logger.LogTrace(Messages.ClientControlledTransaction, _transaction != null);
				Logger.LogTrace(Messages.CommandTextToExecute, statement);
			}

			BuildParameters(command);

			return command;
		}

		/// <summary>
		/// Builds the parameters for the command.
		/// </summary>
		protected virtual void BuildParameters(IDbCommand command)
		{
			foreach (var item in Parameters)
			{
				BuildParameter(command, item.Key, item.Value.Item1, item.Value.Item2);
			}
		}

		/// <summary>
		/// Builds a parameter for the command.
		/// </summary>
		protected virtual void BuildParameter(IDbCommand command, string name, object value, DbType? dbType)
		{
			IDbDataParameter parameter = command.CreateParameter();
			parameter.ParameterName = name;
			SetParameterValue(parameter, value, dbType);

			if (Logger.IsEnabled(LogLevel.Trace))
			{
				Logger.LogTrace(Messages.BindingParameter, name, parameter.Value);
			}
			command.Parameters.Add(parameter);
		}

		/// <summary>
		/// Sets the value of a parameter.
		/// </summary>
		protected virtual void SetParameterValue(IDataParameter param, object value, DbType? type)
		{
			param.Value = value ?? DBNull.Value;
			param.DbType = type ?? (value == null ? DbType.Binary : param.DbType);
		}
	}
}