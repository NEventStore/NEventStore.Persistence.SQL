using System.Data;
using System.Data.Common;
using System.Transactions;

namespace NEventStore.Persistence.Sql.SqlDialects
{
	/// <summary>
	/// Represents a common SQL dialect.
	/// </summary>
	public abstract class CommonSqlDialect : ISqlDialect
	{
		/// <inheritdoc/>
		public abstract string InitializeStorage { get; }
		/// <inheritdoc/>
		public virtual string PurgeStorage
		{
			get { return CommonSqlStatements.PurgeStorage; }
		}
		/// <inheritdoc/>
		public string PurgeBucket
		{
			get { return CommonSqlStatements.PurgeBucket; }
		}
		/// <inheritdoc/>
		public virtual string Drop
		{
			get { return CommonSqlStatements.DropTables; }
		}
		/// <inheritdoc/>
		public virtual string DeleteStream
		{
			get { return CommonSqlStatements.DeleteStream; }
		}
		/// <inheritdoc/>
		public virtual string GetCommitsFromStartingRevision
		{
			get { return CommonSqlStatements.GetCommitsFromStartingRevision; }
		}
		/// <inheritdoc/>
		public virtual string GetCommitsFromInstant
		{
			get { return CommonSqlStatements.GetCommitsFromInstant; }
		}
		/// <inheritdoc/>
		public virtual string GetCommitsFromToInstant
		{
			get { return CommonSqlStatements.GetCommitsFromToInstant; }
		}
		/// <inheritdoc/>
		public abstract string PersistCommit { get; }
		/// <inheritdoc/>
		public virtual string DuplicateCommit
		{
			get { return CommonSqlStatements.DuplicateCommit; }
		}
		/// <inheritdoc/>
		public virtual string GetStreamsRequiringSnapshots
		{
			get { return CommonSqlStatements.GetStreamsRequiringSnapshots; }
		}
		/// <inheritdoc/>
		public virtual string GetSnapshot
		{
			get { return CommonSqlStatements.GetSnapshot; }
		}
		/// <inheritdoc/>
		public virtual string AppendSnapshotToCommit
		{
			get { return CommonSqlStatements.AppendSnapshotToCommit; }
		}
		/// <inheritdoc/>
		public virtual string BucketId
		{
			get { return "@BucketId"; }
		}
		/// <inheritdoc/>
		public virtual string StreamId
		{
			get { return "@StreamId"; }
		}
		/// <inheritdoc/>
		public virtual string StreamIdOriginal
		{
			get { return "@StreamIdOriginal"; }
		}
		/// <inheritdoc/>
		public virtual string StreamRevision
		{
			get { return "@StreamRevision"; }
		}
		/// <inheritdoc/>
		public virtual string MaxStreamRevision
		{
			get { return "@MaxStreamRevision"; }
		}
		/// <inheritdoc/>
		public virtual string Items
		{
			get { return "@Items"; }
		}
		/// <inheritdoc/>
		public virtual string CommitId
		{
			get { return "@CommitId"; }
		}
		/// <inheritdoc/>
		public virtual string CommitSequence
		{
			get { return "@CommitSequence"; }
		}
		/// <inheritdoc/>
		public virtual string CommitStamp
		{
			get { return "@CommitStamp"; }
		}
		/// <inheritdoc/>
		public virtual string CommitStampStart
		{
			get { return "@CommitStampStart"; }
		}
		/// <inheritdoc/>
		public virtual string CommitStampEnd
		{
			get { return "@CommitStampEnd"; }
		}
		/// <inheritdoc/>
		public virtual string Headers
		{
			get { return "@Headers"; }
		}
		/// <inheritdoc/>
		public virtual string Payload
		{
			get { return "@Payload"; }
		}
		/// <inheritdoc/>
		public virtual string Threshold
		{
			get { return "@Threshold"; }
		}
		/// <inheritdoc/>
		public virtual string Limit
		{
			get { return "@Limit"; }
		}
		/// <inheritdoc/>
		public virtual string Skip
		{
			get { return "@Skip"; }
		}
		/// <inheritdoc/>
		public virtual bool CanPage
		{
			get { return true; }
		}
		/// <inheritdoc/>
		public virtual string CheckpointNumber
		{
			get { return "@CheckpointNumber"; }
		}
		/// <inheritdoc/>
		public virtual string FromCheckpointNumber
		{
			get { return "@FromCheckpointNumber"; }
		}
		/// <inheritdoc/>
		public virtual string ToCheckpointNumber
		{
			get { return "@ToCheckpointNumber"; }
		}
		/// <inheritdoc/>
		public virtual string GetCommitsFromCheckpoint
		{
			get { return CommonSqlStatements.GetCommitsFromCheckpoint; }
		}
		/// <inheritdoc/>
		public virtual string GetCommitsFromToCheckpoint
		{
			get { return CommonSqlStatements.GetCommitsFromToCheckpoint; }
		}
		/// <inheritdoc/>
		public virtual string GetCommitsFromBucketAndCheckpoint
		{
			get { return CommonSqlStatements.GetCommitsFromBucketAndCheckpoint; }
		}
		/// <inheritdoc/>
		public virtual string GetCommitsFromToBucketAndCheckpoint
		{
			get { return CommonSqlStatements.GetCommitsFromToBucketAndCheckpoint; }
		}
		/// <inheritdoc/>
		public virtual object CoalesceParameterValue(object value)
		{
			return value;
		}
		/// <inheritdoc/>
		public abstract bool IsDuplicate(Exception exception);
		/// <inheritdoc/>
		public virtual void AddPayloadParameter(IConnectionFactory connectionFactory, IDbConnection connection, IDbStatement cmd, byte[] payload)
		{
			cmd.AddParameter(Payload, payload);
		}
		/// <inheritdoc/>
		public virtual DateTime ToDateTime(object value)
		{
			value = value is decimal v ? (long)v : value;
			return value is long v1 ? new DateTime(v1) : DateTime.SpecifyKind((DateTime)value, DateTimeKind.Utc);
		}
		/// <inheritdoc/>
		public virtual NextPageDelegate NextPageDelegate
		{
			get { return (q, r) => q.SetParameter(CommitSequence, r.CommitSequence()); }
		}

		/// <inheritdoc/>
		public virtual IDbTransaction? OpenTransaction(IDbConnection connection)
		{
			return null;
		}

		/// <inheritdoc/>
		public virtual IDbStatement BuildStatement(TransactionScope? scope, DbConnection connection, DbTransaction? transaction)
		{
			return new CommonDbStatement(this, scope, connection, transaction);
		}
		/// <inheritdoc/>
		public virtual DbType GetDateTimeDbType()
		{
			return DbType.DateTime2;
		}
	}
}