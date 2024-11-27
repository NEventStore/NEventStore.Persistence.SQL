namespace NEventStore.Persistence.Sql.SqlDialects
{
	using System;
	using System.Reflection;

	public class MySqlDialect : CommonSqlDialect
	{
		private const int UniqueKeyViolation = 1062;

		public override string InitializeStorage
		{
			get { return MySqlStatements.InitializeStorage; }
		}

		public override string PersistCommit
		{
			get { return MySqlStatements.PersistCommit; }
		}

		public override string AppendSnapshotToCommit
		{
			get { return base.AppendSnapshotToCommit.Replace("/*FROM DUAL*/", "FROM DUAL"); }
		}

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

		public override bool IsDuplicate(Exception exception)
		{
			PropertyInfo property = exception.GetType().GetProperty("Number");
			return UniqueKeyViolation == (int)property.GetValue(exception, null);
		}
	}
}