namespace NEventStore.Persistence.Sql.SqlDialects
{
	using System;

	public class FirebirdSqlDialect : CommonSqlDialect
	{
		public override string InitializeStorage
		{
			get { return FirebirdSqlStatements.InitializeStorage; }
		}

		public override string PersistCommit
		{
			get { return FirebirdSqlStatements.PersistCommits; }
		}

		public override string GetUndispatchedCommits
		{
			get { return FirebirdSqlStatements.GetUndispatchedCommits; }
		}

		public override string GetCommitsFromInstant
		{
			get { return FirebirdSqlStatements.GetCommitsFromInstant; }
		}

		public override string GetCommitsFromToInstant
		{
			get { return FirebirdSqlStatements.GetCommitsFromToInstant; }
		}

		public override string GetCommitsFromStartingRevision
		{
			get { return FirebirdSqlStatements.GetCommitsFromStartingRevision; }
		}

		public override string GetSnapshot
		{
			get { return FirebirdSqlStatements.GetSnapshot; }
		}

		public override string GetStreamsRequiringSnapshots
		{
			get { return FirebirdSqlStatements.GetStreamsRequiringSnapshots; }
		}

		public override string GetCommitsFromCheckpoint
		{
			get { return FirebirdSqlStatements.GetCommitsFromCheckpoint; }
		}

		public override bool IsDuplicate(Exception exception)
		{
			string message = exception.Message.ToUpperInvariant();
			return message.Contains("DUPLICATE") || message.Contains("UNIQUE") || message.Contains("CONSTRAINT");
		}

		public override string AppendSnapshotToCommit
		{
			get { return FirebirdSqlStatements.AppendSnapshotToCommit; }
		}
	}
}