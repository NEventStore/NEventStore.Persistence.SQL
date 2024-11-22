namespace NEventStore.Persistence.Sql
{
	using System;
	using System.Collections.Generic;
	using System.Data;
	using NEventStore.Persistence.Sql.SqlDialects;

	/// <summary>
	/// A Database statement.
	/// </summary>
	public interface IDbStatement : IDisposable
	{
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
		/// <param name="commandText"></param>
		/// <returns></returns>
		int ExecuteNonQuery(string commandText);

		/// <summary>
		/// Execute a non-query command without exceptions.
		/// </summary>
		int ExecuteWithoutExceptions(string commandText);

		/// <summary>
		/// Execute a scalar command.
		/// </summary>
		object ExecuteScalar(string commandText);

		/// <summary>
		/// Execute a query command.
		/// </summary>
		/// <param name="queryText"></param>
		/// <returns></returns>
		IEnumerable<IDataRecord> ExecuteWithQuery(string queryText);

		/// <summary>
		/// Execute a paged query command.
		/// </summary>
		IEnumerable<IDataRecord> ExecutePagedQuery(string queryText, NextPageDelegate nextpage);
	}
}