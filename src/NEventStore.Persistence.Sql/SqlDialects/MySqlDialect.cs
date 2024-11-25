namespace NEventStore.Persistence.Sql.SqlDialects
{
	using System;
	using System.Reflection;

	/// <summary>
	/// Represents a MySQL dialect.
	/// </summary>
	public class MySqlDialect : CommonSqlDialect
	{
		private const int UniqueKeyViolation = 1062;
		/// <inheritdoc/>
		public override string InitializeStorage
		{
			get { return MySqlStatements.InitializeStorage; }
		}
		/// <inheritdoc/>
		public override string PersistCommit
		{
			get { return MySqlStatements.PersistCommit; }
		}
		/// <inheritdoc/>
		public override string AppendSnapshotToCommit
		{
			get { return base.AppendSnapshotToCommit.Replace("/*FROM DUAL*/", "FROM DUAL"); }
		}
		/// <inheritdoc/>
		public override object CoalesceParameterValue(object value)
		{
			if (value is Guid guid)
			{
				return guid.ToByteArray();
			}

			if (value is DateTime dateTime)
			{
				return dateTime.Ticks;
			}

			return value;
		}
		/// <inheritdoc/>
		public override bool IsDuplicate(Exception exception)
		{
			PropertyInfo property = exception.GetType().GetProperty("Number");
			return UniqueKeyViolation == (int)property.GetValue(exception, null);
		}
	}
}