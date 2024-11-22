namespace NEventStore.Persistence.Sql.SqlDialects
{
	using System;
	using System.Data;
	using System.Transactions;
	using NEventStore.Persistence.Sql;

	public abstract class CommonSqlDialect : ISqlDialect
	{
		public abstract string InitializeStorage { get; }

		public virtual string PurgeStorage
		{
			get { return CommonSqlStatements.PurgeStorage; }
		}

		public string PurgeBucket
		{
			get { return CommonSqlStatements.PurgeBucket; }
		}

		public virtual string Drop
		{
			get { return CommonSqlStatements.DropTables; }
		}

		public virtual string DeleteStream
		{
			get { return CommonSqlStatements.DeleteStream; }
		}

		public virtual string GetCommitsFromStartingRevision
		{
			get { return CommonSqlStatements.GetCommitsFromStartingRevision; }
		}

		public virtual string GetCommitsFromInstant
		{
			get { return CommonSqlStatements.GetCommitsFromInstant; }
		}

		public virtual string GetCommitsFromToInstant
		{
			get { return CommonSqlStatements.GetCommitsFromToInstant; }
		}

		public abstract string PersistCommit { get; }

		public virtual string DuplicateCommit
		{
			get { return CommonSqlStatements.DuplicateCommit; }
		}

		public virtual string GetStreamsRequiringSnapshots
		{
			get { return CommonSqlStatements.GetStreamsRequiringSnapshots; }
		}

		public virtual string GetSnapshot
		{
			get { return CommonSqlStatements.GetSnapshot; }
		}

		public virtual string AppendSnapshotToCommit
		{
			get { return CommonSqlStatements.AppendSnapshotToCommit; }
		}

		public virtual string BucketId
		{
			get { return "@BucketId"; }
		}

		public virtual string StreamId
		{
			get { return "@StreamId"; }
		}

		public virtual string StreamIdOriginal
		{
			get { return "@StreamIdOriginal"; }
		}

		public virtual string StreamRevision
		{
			get { return "@StreamRevision"; }
		}

		public virtual string MaxStreamRevision
		{
			get { return "@MaxStreamRevision"; }
		}

		public virtual string Items
		{
			get { return "@Items"; }
		}

		public virtual string CommitId
		{
			get { return "@CommitId"; }
		}

		public virtual string CommitSequence
		{
			get { return "@CommitSequence"; }
		}

		public virtual string CommitStamp
		{
			get { return "@CommitStamp"; }
		}

		public virtual string CommitStampStart
		{
			get { return "@CommitStampStart"; }
		}

		public virtual string CommitStampEnd
		{
			get { return "@CommitStampEnd"; }
		}

		public virtual string Headers
		{
			get { return "@Headers"; }
		}

		public virtual string Payload
		{
			get { return "@Payload"; }
		}

		public virtual string Threshold
		{
			get { return "@Threshold"; }
		}

		public virtual string Limit
		{
			get { return "@Limit"; }
		}

		public virtual string Skip
		{
			get { return "@Skip"; }
		}

		public virtual bool CanPage
		{
			get { return true; }
		}

		public virtual string CheckpointNumber
		{
			get { return "@CheckpointNumber"; }
		}

		public virtual string FromCheckpointNumber
		{
			get { return "@FromCheckpointNumber"; }
		}

		public virtual string ToCheckpointNumber
		{
			get { return "@ToCheckpointNumber"; }
		}

		public virtual string GetCommitsFromCheckpoint
		{
			get { return CommonSqlStatements.GetCommitsFromCheckpoint; }
		}

		public virtual string GetCommitsFromToCheckpoint
		{
			get { return CommonSqlStatements.GetCommitsFromToCheckpoint; }
		}

		public virtual string GetCommitsFromBucketAndCheckpoint
		{
			get { return CommonSqlStatements.GetCommitsFromBucketAndCheckpoint; }
		}

		public virtual string GetCommitsFromToBucketAndCheckpoint
		{
			get { return CommonSqlStatements.GetCommitsFromToBucketAndCheckpoint; }
		}

		public virtual object CoalesceParameterValue(object value)
		{
			return value;
		}

		public abstract bool IsDuplicate(Exception exception);

		public virtual void AddPayloadParameter(IConnectionFactory connectionFactory, IDbConnection connection, IDbStatement cmd, byte[] payload)
		{
			cmd.AddParameter(Payload, payload);
		}

		public virtual DateTime ToDateTime(object value)
		{
			value = value is decimal v ? (long)v : value;
			return value is long v1 ? new DateTime(v1) : DateTime.SpecifyKind((DateTime)value, DateTimeKind.Utc);
		}

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
		public virtual IDbStatement BuildStatement(TransactionScope? scope, IDbConnection connection, IDbTransaction? transaction)
		{
			return new CommonDbStatement(this, scope, connection, transaction);
		}

		public virtual DbType GetDateTimeDbType()
		{
			return DbType.DateTime2;
		}
	}
}