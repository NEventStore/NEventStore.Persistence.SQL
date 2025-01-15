namespace NEventStore.Persistence.Sql
{
	/// <summary>
	/// Represents a connection factory.
	/// </summary>
	public interface IConnectionFactory
	{
		/// <summary>
		/// Opens a new connection.
		/// </summary>
		ConnectionScope Open();
		/// <summary>
		/// Gets the type of the database provider factory.
		/// </summary>
		Type GetDbProviderFactoryType();
	}
}