using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Transactions;

namespace NEventStore.Persistence.Sql.SqlDialects
{
	/// <summary>
	/// Represents a SQL dialect for Oracle.
	/// </summary>
	public class OracleDbStatement : CommonDbStatement
	{
		private readonly ISqlDialect _dialect;
		/// <summary>
		/// Initializes a new instance of the <see cref="OracleDbStatement"/> class.
		/// </summary>
		public OracleDbStatement(ISqlDialect dialect, TransactionScope? scope, ConnectionScope connection, DbTransaction? transaction)
			: base(dialect, scope, connection, transaction)
		{
			_dialect = dialect;
		}
		/// <inheritdoc/>
		public override void AddParameter(string name, object value, DbType? dbType = null)
		{
			name = name.Replace('@', ':');

			if (value is Guid guid)
			{
				base.AddParameter(name, guid.ToByteArray(), null);
			}
			else
			{
				base.AddParameter(name, value, dbType);
			}
		}
		/// <inheritdoc/>
		public override int ExecuteNonQuery(string commandText)
		{
			try
			{
				using (IDbCommand command = BuildCommand(commandText))
					return command.ExecuteNonQuery();
			}
			catch (Exception e)
			{
				if (Dialect.IsDuplicate(e))
				{
					throw new UniqueKeyViolationException(e.Message, e);
				}

				throw;
			}
		}
		/// <inheritdoc/>
		protected override DbCommand BuildCommand(string statement)
		{
			DbCommand command = base.BuildCommand(statement);
			PropertyInfo pi = command.GetType().GetProperty("BindByName");
			pi?.SetValue(command, true, null);
			return command;
		}
		/// <inheritdoc/>
		protected override void BuildParameter(IDbCommand command, string name, object value, DbType? dbType)
		{
			//HACK
			if (name == _dialect.Payload && value is DbParameter)
			{
				command.Parameters.Add(value);
				return;
			}
			base.BuildParameter(command, name, value, dbType);
		}
	}
}