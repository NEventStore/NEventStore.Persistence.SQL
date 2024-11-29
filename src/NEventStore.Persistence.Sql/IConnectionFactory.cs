namespace NEventStore.Persistence.Sql
{
	using System;
	using System.Data;

	/// <summary>
	/// Represents a connection factory.
	/// </summary>
	public interface IConnectionFactory
	{
		/// <summary>
		/// Opens a new connection.
		/// </summary>
		IDbConnection Open();
		/// <summary>
		/// Gets the type of the database provider factory.
		/// </summary>
		Type GetDbProviderFactoryType();
	}
}