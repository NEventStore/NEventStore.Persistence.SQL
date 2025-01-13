using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Transactions;

namespace NEventStore.Persistence.Sql.SqlDialects
{
	/// <summary>
	/// Represents a SQL dialect for Oracle.
	/// </summary>
	public class OracleNativeDialect : CommonSqlDialect
	{
		private Action<IConnectionFactory, IDbConnection, IDbStatement, byte[]>? _addPayloadParameter;
		/// <inheritdoc/>
		public override string AppendSnapshotToCommit
		{
			get { return OracleNativeStatements.AppendSnapshotToCommit; }
		}
		/// <inheritdoc/>
		public override string CheckpointNumber
		{
			get { return MakeOracleParameter(base.CheckpointNumber); }
		}
		/// <inheritdoc/>
		public override string FromCheckpointNumber
		{
			get { return MakeOracleParameter(base.FromCheckpointNumber); }
		}
		/// <inheritdoc/>
		public override string ToCheckpointNumber
		{
			get { return MakeOracleParameter(base.ToCheckpointNumber); }
		}
		/// <inheritdoc/>
		public override string CommitId
		{
			get { return MakeOracleParameter(base.CommitId); }
		}
		/// <inheritdoc/>
		public override string CommitSequence
		{
			get { return MakeOracleParameter(base.CommitSequence); }
		}
		/// <inheritdoc/>
		public override string CommitStamp
		{
			get { return MakeOracleParameter(base.CommitStamp); }
		}
		/// <inheritdoc/>
		public override string CommitStampEnd
		{
			get { return MakeOracleParameter(base.CommitStampEnd); }
		}
		/// <inheritdoc/>
		public override string CommitStampStart
		{
			get { return MakeOracleParameter(CommitStampStart); }
		}
		/// <inheritdoc/>
		public override string DuplicateCommit
		{
			get { return OracleNativeStatements.DuplicateCommit; }
		}
		/// <inheritdoc/>
		public override string GetSnapshot
		{
			get { return OracleNativeStatements.GetSnapshot; }
		}
		/// <inheritdoc/>
		public override string GetCommitsFromStartingRevision
		{
			get { return LimitedQuery(OracleNativeStatements.GetCommitsFromStartingRevision); }
		}
		/// <inheritdoc/>
		public override string GetCommitsFromInstant
		{
			get { return OraclePaging(OracleNativeStatements.GetCommitsFromInstant); }
		}
		/// <inheritdoc/>
		public override string GetCommitsFromCheckpoint
		{
			get { return OraclePaging(OracleNativeStatements.GetCommitsSinceCheckpoint); }
		}
		/// <inheritdoc/>
		public override string GetCommitsFromToCheckpoint
		{
			get { return OraclePaging(OracleNativeStatements.GetCommitsSinceToCheckpoint); }
		}
		/// <inheritdoc/>
		public override string GetCommitsFromBucketAndCheckpoint
		{
			get { return OraclePaging(OracleNativeStatements.GetCommitsFromBucketAndCheckpoint); }
		}
		/// <inheritdoc/>
		public override string GetCommitsFromToBucketAndCheckpoint
		{
			get { return OraclePaging(OracleNativeStatements.GetCommitsFromToBucketAndCheckpoint); }
		}
		/// <inheritdoc/>
		public override string GetStreamsRequiringSnapshots
		{
			get { return LimitedQuery(OracleNativeStatements.GetStreamsRequiringSnapshots); }
		}
		/// <inheritdoc/>
		public override string InitializeStorage
		{
			get { return OracleNativeStatements.InitializeStorage; }
		}
		/// <inheritdoc/>
		public override string Limit
		{
			get { return MakeOracleParameter(base.Limit); }
		}
		/// <inheritdoc/>
		public override string PersistCommit
		{
			get { return OracleNativeStatements.PersistCommit; }
		}
		/// <inheritdoc/>
		public override string PurgeStorage
		{
			get { return OracleNativeStatements.PurgeStorage; }
		}
		/// <inheritdoc/>
		public override string DeleteStream
		{
			get { return OracleNativeStatements.DeleteStream; }
		}
		/// <inheritdoc/>
		public override string Drop
		{
			get { return OracleNativeStatements.DropTables; }
		}
		/// <inheritdoc/>
		public override string Skip
		{
			get { return MakeOracleParameter(base.Skip); }
		}
		/// <inheritdoc/>
		public override string BucketId
		{
			get { return MakeOracleParameter(base.BucketId); }
		}
		/// <inheritdoc/>
		public override string StreamId
		{
			get { return MakeOracleParameter(base.StreamId); }
		}
		/// <inheritdoc/>
		public override string StreamIdOriginal
		{
			get { return MakeOracleParameter(base.StreamIdOriginal); }
		}
		/// <inheritdoc/>
		public override string Threshold
		{
			get { return MakeOracleParameter(base.Threshold); }
		}
		/// <inheritdoc/>
		public override string Payload
		{
			get { return MakeOracleParameter(base.Payload); }
		}
		/// <inheritdoc/>
		public override string StreamRevision
		{
			get { return MakeOracleParameter(base.StreamRevision); }
		}
		/// <inheritdoc/>
		public override string MaxStreamRevision
		{
			get { return MakeOracleParameter(base.MaxStreamRevision); }
		}
		/// <inheritdoc/>
		public override IDbStatement BuildStatement(TransactionScope? scope, DbConnection connection, DbTransaction? transaction)
		{
			return new OracleDbStatement(this, scope, connection, transaction);
		}
		/// <inheritdoc/>
		public override object CoalesceParameterValue(object value)
		{
			if (value is Guid guid)
			{
				value = guid.ToByteArray();
			}

			return value;
		}

		private static string ExtractOrderBy(ref string query)
		{
			int orderByIndex = query.IndexOf("ORDER BY", StringComparison.Ordinal);
			string result = query.Substring(orderByIndex).Replace(";", String.Empty);
			query = query.Substring(0, orderByIndex);

			return result;
		}
		/// <inheritdoc/>
		public override bool IsDuplicate(Exception exception)
		{
			return exception.Message.Contains("ORA-00001");
		}
		/// <inheritdoc/>
		public override NextPageDelegate NextPageDelegate
		{
			get { return (_, _) => { }; }
		}
		/// <inheritdoc/>
		public override void AddPayloadParameter(IConnectionFactory connectionFactory, IDbConnection connection, IDbStatement cmd, byte[] payload)
		{
			if (_addPayloadParameter == null)
			{
				string dbProviderAssemblyName = connectionFactory.GetDbProviderFactoryType().Assembly.GetName().Name;
				const string oracleManagedDataAccessAssemblyName = "Oracle.ManagedDataAccess";
				const string oracleDataAccessAssemblyName = "Oracle.DataAccess";
				if (dbProviderAssemblyName.Equals(oracleManagedDataAccessAssemblyName, StringComparison.Ordinal))
				{
					_addPayloadParameter = CreateOraAddPayloadAction(oracleManagedDataAccessAssemblyName);
				}
				else if (dbProviderAssemblyName.Equals(oracleDataAccessAssemblyName, StringComparison.Ordinal))
				{
					_addPayloadParameter = CreateOraAddPayloadAction(oracleDataAccessAssemblyName);
				}
				else
				{
					_addPayloadParameter = (connectionFactory2, connection2, cmd2, payload2)
						=> base.AddPayloadParameter(connectionFactory2, connection2, cmd2, payload2);
				}
			}
			_addPayloadParameter(connectionFactory, connection, cmd, payload);
		}

		private Action<IConnectionFactory, IDbConnection, IDbStatement, byte[]> CreateOraAddPayloadAction(
			string assemblyName)
		{
			Assembly assembly = Assembly.Load(assemblyName);
			var oracleParameterType = assembly.GetType(assemblyName + ".Client.OracleParameter", true);
			var oracleParameterValueProperty = oracleParameterType.GetProperty("Value");
			var oracleBlobType = assembly.GetType(assemblyName + ".Types.OracleBlob", true);
			var oracleBlobWriteMethod = oracleBlobType.GetMethod("Write", [typeof(Byte[]), typeof(int), typeof(int)]);
			Type oracleParamType = assembly.GetType(assemblyName + ".Client.OracleDbType", true);
			FieldInfo blobField = oracleParamType.GetField("Blob");
			var blobDbType = blobField.GetValue(null);

			return (_, connection2, cmd2, payload2) =>
			{
				object payloadParam = Activator.CreateInstance(oracleParameterType, [Payload, blobDbType]);
				((OracleDbStatement)cmd2).AddParameter(Payload, payloadParam);
				object oracleConnection = ((ConnectionScope)connection2).Current;
				object oracleBlob = Activator.CreateInstance(oracleBlobType, [oracleConnection]);
				oracleBlobWriteMethod.Invoke(oracleBlob, [payload2, 0, payload2.Length]);
				oracleParameterValueProperty.SetValue(payloadParam, oracleBlob, null);
			};
		}

		private static string LimitedQuery(string query)
		{
			query = RemovePaging(query);
			if (query.EndsWith(";"))
			{
				query = query.TrimEnd([';']);
			}
			string value = OracleNativeStatements.LimitedQueryFormat.FormatWith(query);
			return value;
		}

		private static string MakeOracleParameter(string parameterName)
		{
			return parameterName.Replace('@', ':');
		}

		private static string OraclePaging(string query)
		{
			query = RemovePaging(query);

			string orderBy = ExtractOrderBy(ref query);

			int fromIndex = query.IndexOf("FROM ", StringComparison.Ordinal);
			string from = query.Substring(fromIndex);

			string select = query.Substring(0, fromIndex);

			string value = OracleNativeStatements.PagedQueryFormat.FormatWith(select, orderBy, from);

			return value;
		}

		private static string RemovePaging(string query)
		{
			return query
				.Replace("\n LIMIT @Limit OFFSET @Skip;", ";")
				.Replace("\n LIMIT @Limit;", ";")
				.Replace("WHERE ROWNUM <= :Limit;", ";")
				.Replace("\r\nWHERE ROWNUM <= (:Skip + 1) AND ROWNUM  > :Skip", ";");
		}
	}
}