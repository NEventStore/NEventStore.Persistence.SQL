using System.Data;
using System.Data.Common;
using System.Transactions;
using NEventStore.Persistence.Sql.SqlDialects;

namespace NEventStore.Persistence.Sql
{
	/// <summary>
	/// Represents a SQL dialect.
	/// </summary>
	public interface ISqlDialect
	{
		/// <summary>
		/// Queries to initialize the storage.
		/// </summary>
		string InitializeStorage { get; }
		/// <summary>
		/// Queries to purge the storage.
		/// </summary>
		string PurgeStorage { get; }
		/// <summary>
		/// Queries to purge a bucket.
		/// </summary>
		string PurgeBucket { get; }
		/// <summary>
		/// Queries to drop the storage (tables).
		/// </summary>
		string Drop { get; }
		/// <summary>
		/// Queries to delete a stream.
		/// </summary>
		string DeleteStream { get; }
		/// <summary>
		/// Queries to get commits from a starting revision.
		/// </summary>
		string GetCommitsFromStartingRevision { get; }
		/// <summary>
		/// Queries to get commits from an instant.
		/// </summary>
		string GetCommitsFromInstant { get; }
		/// <summary>
		/// Queries to get commits from an instant to another instant.
		/// </summary>
		string GetCommitsFromToInstant { get; }
		/// <summary>
		/// Queries to persist a commit.
		/// </summary>
		string PersistCommit { get; }
		/// <summary>
		/// Queries to check for a duplicate commit.
		/// </summary>
		string DuplicateCommit { get; }
		/// <summary>
		/// Queries to get streams requiring snapshots.
		/// </summary>
		string GetStreamsRequiringSnapshots { get; }
		/// <summary>
		/// Queries to get a snapshot.
		/// </summary>
		string GetSnapshot { get; }
		/// <summary>
		/// Queries to append a snapshot to a commit.
		/// </summary>
		string AppendSnapshotToCommit { get; }
		/// <summary>
		/// Bucket Id parameter.
		/// </summary>
		string BucketId { get; }
		/// <summary>
		/// Stream Id parameter.
		/// </summary>
		string StreamId { get; }
		/// <summary>
		/// Original Stream Id parameter.
		/// </summary>
		string StreamIdOriginal { get; }
		/// <summary>
		/// Stream Revision parameter.
		/// </summary>
		string StreamRevision { get; }
		/// <summary>
		/// Max Stream Revision parameter.
		/// </summary>
		string MaxStreamRevision { get; }
		/// <summary>
		/// Items parameter.
		/// </summary>
		string Items { get; }
		/// <summary>
		/// Commit Id parameter.
		/// </summary>
		string CommitId { get; }
		/// <summary>
		/// Commit Sequence parameter.
		/// </summary>
		string CommitSequence { get; }
		/// <summary>
		/// Commit Stamp parameter.
		/// </summary>
		string CommitStamp { get; }
		/// <summary>
		/// Commit Stamp Start parameter.
		/// </summary>
		string CommitStampStart { get; }
		/// <summary>
		/// Commit Stamp End parameter.
		/// </summary>
		string CommitStampEnd { get; }
		/// <summary>
		/// Headers parameter.
		/// </summary>
		string Headers { get; }
		/// <summary>
		/// Payload parameter.
		/// </summary>
		string Payload { get; }
		/// <summary>
		/// Threshold parameter.
		/// </summary>
		string Threshold { get; }
		/// <summary>
		/// Limit parameter.
		/// </summary>
		string Limit { get; }
		/// <summary>
		/// Skip parameter.
		/// </summary>
		string Skip { get; }
		/// <summary>
		/// Can executed pagination.
		/// </summary>
		bool CanPage { get; }
		/// <summary>
		/// Checkpoint Number parameter.
		/// </summary>
		string CheckpointNumber { get; }
		/// <summary>
		/// From Checkpoint Number parameter.
		/// </summary>
		string FromCheckpointNumber { get; }
		/// <summary>
		/// To Checkpoint Number parameter.
		/// </summary>
		string ToCheckpointNumber { get; }
		/// <summary>
		/// Queries to get commits from a checkpoint.
		/// </summary>
		string GetCommitsFromCheckpoint { get; }
		/// <summary>
		/// Queries to get commits from a checkpoint to a checkpoint.
		/// </summary>
		string GetCommitsFromToCheckpoint { get; }
		/// <summary>
		/// Queries to get commits from a bucket and a checkpoint.
		/// </summary>
		string GetCommitsFromBucketAndCheckpoint { get; }
		/// <summary>
		/// Queries to get commits from-to a bucket and a checkpoint.
		/// </summary>
		string GetCommitsFromToBucketAndCheckpoint { get; }
		/// <summary>
		/// Properly setup the Coalesces the parameter value.
		/// </summary>
		object CoalesceParameterValue(object value);

		/// <summary>
		/// Opens a Transaction.
		/// </summary>
		DbTransaction? OpenTransaction(DbConnection connection);

		/// <summary>
		/// Builds a statement.
		/// </summary>
		IDbStatement BuildStatement(TransactionScope? scope, ConnectionScope connection, DbTransaction? transaction);
		/// <summary>
		/// Check if the exception represents a unique index violation.
		/// </summary>
		bool IsDuplicate(Exception exception);
		/// <summary>
		/// Adds a payload parameter.
		/// </summary>
		void AddPayloadParameter(IConnectionFactory connectionFactory, DbConnection connection, IDbStatement cmd, byte[] payload);
		/// <summary>
		/// Converts a value to a DateTime.
		/// </summary>
		DateTime ToDateTime(object value);
		/// <summary>
		/// The next page delegate.
		/// </summary>
		NextPageDelegate NextPageDelegate { get; }
		/// <summary>
		/// Gets the DbType for a DateTime.
		/// </summary>
		DbType GetDateTimeDbType();
	}
}