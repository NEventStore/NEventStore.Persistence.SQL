namespace NEventStore.Persistence.Sql.SqlDialects
{
	using System;
	using System.Collections.Generic;
	using System.Data;
	using System.Linq;
	using System.Transactions;
	using NEventStore.Persistence.Sql;

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
			IDbConnection connection,
			IDbTransaction transaction)
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