namespace NEventStore.Persistence.Sql.SqlDialects
{
	using System.Data;

	/// <summary>
	/// Delegate for the next page of a query.
	/// </summary>
	public delegate void NextPageDelegate(IDbCommand command, IDataRecord current);
}