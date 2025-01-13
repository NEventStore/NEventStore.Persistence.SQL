using System.Data;
using System.Data.Common;
using System.Transactions;

namespace NEventStore.Persistence.Sql.SqlDialects
{
	/// <summary>
	/// Represents a <see cref="IDbStatement"/> that splits the command text by a delimiter and executes each command separately.
	/// </summary>
	public class DelimitedDbStatement : CommonDbStatement
	{
		private const string Delimiter = ";";

		/// <summary>
		/// Initializes a new instance of the <see cref="DelimitedDbStatement"/> class.
		/// </summary>
		public DelimitedDbStatement(
			ISqlDialect dialect,
			TransactionScope scope,
			DbConnection connection,
			DbTransaction transaction)
			: base(dialect, scope, connection, transaction)
		{ }

		/// <inheritdoc/>
		public override int ExecuteNonQuery(string commandText)
		{
			return SplitCommandText(commandText).Sum(x => base.ExecuteNonQuery(x));
		}

		private static IEnumerable<string> SplitCommandText(string delimited)
		{
			if (string.IsNullOrEmpty(delimited))
			{
				return [];
			}

			return delimited.Split(Delimiter.ToCharArray(), StringSplitOptions.RemoveEmptyEntries)
							.AsEnumerable().Select(x => x + Delimiter)
							.ToArray();
		}
	}
}