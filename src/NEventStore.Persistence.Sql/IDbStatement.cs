using System.Data;
using NEventStore.Persistence.Sql.SqlDialects;

namespace NEventStore.Persistence.Sql
{
	/// <summary>
	/// A Database statement.
	/// </summary>
	public interface IDbStatement : IDisposable
	{
		/// <summary>
		/// Infinite page size.
		/// </summary>
		int InfinitePageSize { get; }
		/// <summary>
		/// Page size.
		/// </summary>
		int PageSize { get; set; }

		/// <summary>
		/// Add a parameter to the statement.
		/// </summary>
		void AddParameter(string name, object value, DbType? parameterType = null);

		/// <summary>
		/// Execute a non-query command.
		/// </summary>
		int ExecuteNonQuery(string commandText);

		/// <summary>
		/// Execute a non-query command.
		/// </summary>
		Task<int> ExecuteNonQueryAsync(string commandText, CancellationToken cancellationToken);

		/// <summary>
		/// Execute a non-query command without exceptions.
		/// </summary>
		int ExecuteWithoutExceptions(string commandText);

		/// <summary>
		/// Execute a non-query command without exceptions.
		/// </summary>
		Task<int> ExecuteWithoutExceptionsAsync(string commandText, CancellationToken cancellationToken);

		/// <summary>
		/// Execute a scalar command.
		/// </summary>
		object ExecuteScalar(string commandText);

		/// <summary>
		/// Execute a scalar command.
		/// </summary>
		Task<object> ExecuteScalarAsync(string commandText, CancellationToken cancellationToken);

		/// <summary>
		/// Execute a query command.
		/// </summary>
		IEnumerable<IDataRecord> ExecuteWithQuery(string queryText);

		/// <summary>
		/// Execute a query command.
		/// </summary>
		Task ExecuteWithQueryAsync(string queryText, IAsyncObserver<IDataRecord> asyncObserver, CancellationToken cancellationToken);

		/// <summary>
		/// Execute a paged query command.
		/// </summary>
		IEnumerable<IDataRecord> ExecutePagedQuery(string queryText, NextPageDelegate nextPage);

		/// <summary>
		/// Execute a paged query command.
		/// </summary>
		Task ExecutePagedQueryAsync(string queryText, NextPageDelegate nextPage, IAsyncObserver<IDataRecord> asyncObserver, CancellationToken cancellationToken);
	}
}