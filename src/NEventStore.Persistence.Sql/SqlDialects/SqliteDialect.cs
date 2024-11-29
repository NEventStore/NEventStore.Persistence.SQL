namespace NEventStore.Persistence.Sql.SqlDialects
{
	using System;
	using System.Data;

	/// <summary>
	/// Dialect that should be used with System.Data.Sqlite
	/// </summary>
	public class SqliteDialect : CommonSqlDialect
	{
		/// <inheritdoc />
		public override string InitializeStorage
		{
			get { return SqliteStatements.InitializeStorage; }
		}

		/// <inheritdoc />
		public override string GetCommitsFromStartingRevision
		{
			// Sqlite wants all parameters to be a part of the query
			get { return base.GetCommitsFromStartingRevision.Replace("\n ORDER BY ", "\n  AND @Skip = @Skip\nORDER BY "); }
		}
		/// <inheritdoc />
		public override string PersistCommit
		{
			get { return SqliteStatements.PersistCommit; }
		}
		/// <inheritdoc />
		public override bool IsDuplicate(Exception exception)
		{
			string message = exception.Message.ToUpperInvariant();
			return message.Contains("DUPLICATE") || message.Contains("UNIQUE") || message.Contains("CONSTRAINT");
		}
		/// <inheritdoc />
		public override DateTime ToDateTime(object value)
		{
			return ((DateTime)value).ToUniversalTime();
		}
		/// <inheritdoc />
		public override DbType GetDateTimeDbType()
		{
			return DbType.DateTime;
		}
	}

	/// <summary>
	/// Dialect that should be used with Microsoft.Data.Sqlite.
	/// </summary>
	public class MicrosoftDataSqliteDialect : SqliteDialect
	{
		/// <inheritdoc />
		public override DateTime ToDateTime(object value)
		{
			// original code
			// return ((DateTime) value).ToUniversalTime();
			// not working, 'value' is already an utc value in ISO86001 format
			// Convert.ToDateTime() will return an unspecified kind
			// return Convert.ToDateTime(value).ToUniversalTime();

			// CommitStamp is always an UTC value
			return DateTime.SpecifyKind(Convert.ToDateTime(value), DateTimeKind.Utc);
		}
	}
}