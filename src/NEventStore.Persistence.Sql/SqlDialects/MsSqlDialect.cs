using System.Data;
using System.Transactions;
using IsolationLevel = System.Data.IsolationLevel;

namespace NEventStore.Persistence.Sql.SqlDialects
{
	using System;

	/// <summary>
	/// Represents a SQL dialect for Microsoft SQL Server.
	/// </summary>
	public class MsSqlDialect : CommonSqlDialect
	{
		private const int UniqueIndexViolation = 2601;
		private const int UniqueKeyViolation = 2627;
		/// <inheritdoc/>
		public override string InitializeStorage
		{
			get { return MsSqlStatements.InitializeStorage; }
		}
		/// <inheritdoc/>
		public override string GetSnapshot
		{
			get { return "SET ROWCOUNT 1;\n" + base.GetSnapshot.Replace("LIMIT 1;", ";"); }
		}
		/// <inheritdoc/>
		public override string GetCommitsFromStartingRevision
		{
			get { return NaturalPaging(base.GetCommitsFromStartingRevision); }
		}
		/// <inheritdoc/>
		public override string GetCommitsFromInstant
		{
			get { return CommonTableExpressionPaging(base.GetCommitsFromInstant); }
		}
		/// <inheritdoc/>
		public override string GetCommitsFromToInstant
		{
			get { return CommonTableExpressionPaging(base.GetCommitsFromToInstant); }
		}
		/// <inheritdoc/>
		public override string PersistCommit
		{
			get { return MsSqlStatements.PersistCommits; }
		}
		/// <inheritdoc/>
		public override string GetCommitsFromCheckpoint
		{
			get { return CommonTableExpressionPaging(base.GetCommitsFromCheckpoint); }
		}
		/// <inheritdoc/>
		public override string GetCommitsFromToCheckpoint
		{
			get { return CommonTableExpressionPaging(base.GetCommitsFromToCheckpoint); }
		}
		/// <inheritdoc/>
		public override string GetCommitsFromBucketAndCheckpoint
		{
			get { return CommonTableExpressionPaging(base.GetCommitsFromBucketAndCheckpoint); }
		}
		/// <inheritdoc/>
		public override string GetCommitsFromToBucketAndCheckpoint
		{
			get { return CommonTableExpressionPaging(base.GetCommitsFromToBucketAndCheckpoint); }
		}
		/// <inheritdoc/>
		public override string GetStreamsRequiringSnapshots
		{
			get { return NaturalPaging(base.GetStreamsRequiringSnapshots); }
		}

		private static string NaturalPaging(string query)
		{
			return "SET ROWCOUNT @Limit;\n" + RemovePaging(query);
		}

		private static string CommonTableExpressionPaging(string query)
		{
			query = RemovePaging(query);
			int orderByIndex = query.IndexOf("ORDER BY");
			string orderBy = query.Substring(orderByIndex).Replace(";", string.Empty);
			query = query.Substring(0, orderByIndex);

			int fromIndex = query.IndexOf("FROM ");
			string from = query.Substring(fromIndex);
			string select = query.Substring(0, fromIndex);

			string value = MsSqlStatements.PagedQueryFormat.FormatWith(select, orderBy, from);
			return value;
		}

		private static string RemovePaging(string query)
		{
			return query
				.Replace("\n LIMIT @Limit OFFSET @Skip;", ";")
				.Replace("\n LIMIT @Limit;", ";");
		}
		/// <inheritdoc/>
		public override bool IsDuplicate(Exception exception)
		{
			return exception is Microsoft.Data.SqlClient.SqlException dbException
				&& (dbException.Number == UniqueIndexViolation || dbException.Number == UniqueKeyViolation);
		}

		/// <inheritdoc/>
		public override IDbTransaction? OpenTransaction(IDbConnection connection)
		{
			if (Transaction.Current == null)
				return connection.BeginTransaction(IsolationLevel.ReadCommitted);

			return base.OpenTransaction(connection);
		}
	}

	/// <summary>
	/// Represents a SQL dialect for Microsoft SQL Server 2005.
	/// </summary>
	public class MsSql2005Dialect : MsSqlDialect
	{
		/// <inheritdoc/>
		public override DbType GetDateTimeDbType()
		{
			return DbType.DateTime;
		}
	}
}