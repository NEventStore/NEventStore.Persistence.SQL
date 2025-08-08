using System.Data;
using System.Data.Common;
using System.Transactions;
using Microsoft.Extensions.Logging;
using NEventStore.Logging;
using NEventStore.Serialization;

namespace NEventStore.Persistence.Sql
{
	/// <summary>
	/// Represents a persistence engine that stores and retrieves events from a SQL database.
	/// </summary>
	public partial class SqlPersistenceEngine : IPersistStreams
	{
		private static readonly ILogger Logger = LogFactory.BuildLogger(typeof(SqlPersistenceEngine));
		private static readonly DateTime EpochTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		private readonly IConnectionFactory _connectionFactory;
		private readonly ISqlDialect _dialect;
		private readonly int _pageSize;
		private readonly TransactionScopeOption? _scopeOption;
		private readonly ISerialize _serializer;
		private readonly ISerializeEvents _eventSerializer;
		private bool _disposed;
		private int _initialized;
		private readonly IStreamIdHasher _streamIdHasher;

		/// <summary>
		/// Initializes a new instance of the <see cref="SqlPersistenceEngine"/> class.
		/// </summary>
		public SqlPersistenceEngine(
			IConnectionFactory connectionFactory,
			ISqlDialect dialect,
			ISerialize serializer,
			ISerializeEvents eventSerializer,
			int pageSize,
			TransactionScopeOption? scopeOption = null)
			: this(connectionFactory, dialect, serializer, eventSerializer, pageSize, new Sha1StreamIdHasher(), scopeOption)
		{ }

		/// <summary>
		/// Initializes a new instance of the <see cref="SqlPersistenceEngine"/> class.
		/// </summary>
		public SqlPersistenceEngine(
			IConnectionFactory connectionFactory,
			ISqlDialect dialect,
			ISerialize serializer,
			ISerializeEvents eventSerializer,
			int pageSize,
			IStreamIdHasher streamIdHasher,
			TransactionScopeOption? scopeOption = null)
		{
			if (pageSize < 0)
			{
				throw new ArgumentException(nameof(pageSize));
			}

			if (streamIdHasher == null)
			{
				throw new ArgumentNullException(nameof(streamIdHasher));
			}

			_connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
			_dialect = dialect ?? throw new ArgumentNullException(nameof(dialect));
			_serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
			_eventSerializer = eventSerializer ?? throw new ArgumentNullException(nameof(eventSerializer));
			_scopeOption = scopeOption;
			_pageSize = pageSize;
			_streamIdHasher = new StreamIdHasherValidator(streamIdHasher);

			if (Logger.IsEnabled(LogLevel.Debug))
			{
				Logger.LogDebug(Messages.UsingScope, _scopeOption.ToString());
			}
		}

		/// <inheritdoc/>
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <inheritdoc/>
		public virtual void Initialize()
		{
			if (Interlocked.Increment(ref _initialized) > 1)
			{
				return;
			}

			if (Logger.IsEnabled(LogLevel.Debug))
			{
				Logger.LogDebug(Messages.InitializingStorage);
			}
			ExecuteCommand(statement => statement.ExecuteWithoutExceptions(_dialect.InitializeStorage));
		}

		/// <inheritdoc/>
		public virtual IEnumerable<ICommit> GetFrom(string bucketId, string streamId, int minRevision, int maxRevision)
		{
			if (Logger.IsEnabled(LogLevel.Debug))
			{
				Logger.LogDebug(Messages.GettingAllCommitsBetween, streamId, minRevision, maxRevision);
			}

			streamId = _streamIdHasher.GetHash(streamId);
			return ExecuteQuery(query =>
				{
					string statement = _dialect.GetCommitsFromStartingRevision;
					query.AddParameter(_dialect.BucketId, bucketId, DbType.AnsiString);
					query.AddParameter(_dialect.StreamId, streamId, DbType.AnsiString);
					query.AddParameter(_dialect.StreamRevision, minRevision);
					query.AddParameter(_dialect.MaxStreamRevision, maxRevision);
					query.AddParameter(_dialect.CommitSequence, 0);
					return query
						.ExecutePagedQuery(statement, _dialect.NextPageDelegate)
						.Select(x => x.GetCommit(_serializer, _eventSerializer, _dialect));
				});
		}

		/// <inheritdoc/>
		[Obsolete("DateTime is problematic in distributed systems. Use GetFrom(Int64 checkpointToken) instead. This method will be removed in a later version.")]
		public virtual IEnumerable<ICommit> GetFrom(string bucketId, DateTime startDate)
		{
			startDate = startDate.AddTicks(-(startDate.Ticks % TimeSpan.TicksPerSecond)); // Rounds down to the nearest second.
			startDate = startDate < EpochTime ? EpochTime : startDate;

			if (Logger.IsEnabled(LogLevel.Debug))
			{
				Logger.LogDebug(Messages.GettingAllCommitsFrom, startDate, bucketId);
			}

			return ExecuteQuery(query =>
				{
					string statement = _dialect.GetCommitsFromInstant;
					query.AddParameter(_dialect.BucketId, bucketId, DbType.AnsiString);
					query.AddParameter(_dialect.CommitStamp, startDate, _dialect.GetDateTimeDbType());
					return query.ExecutePagedQuery(statement, (_, _) => { })
							.Select(x => x.GetCommit(_serializer, _eventSerializer, _dialect));
				});
		}

		/// <inheritdoc/>
		[Obsolete("DateTime is problematic in distributed systems. Use GetFromTo(Int64 fromCheckpointToken, Int64 toCheckpointToken) instead. This method will be removed in a later version.")]
		public virtual IEnumerable<ICommit> GetFromTo(string bucketId, DateTime startDate, DateTime endDate)
		{
			startDate = startDate.AddTicks(-(startDate.Ticks % TimeSpan.TicksPerSecond)); // Rounds down to the nearest second.
			startDate = startDate < EpochTime ? EpochTime : startDate;
			endDate = endDate < EpochTime ? EpochTime : endDate;

			if (Logger.IsEnabled(LogLevel.Debug))
			{
				Logger.LogDebug(Messages.GettingAllCommitsFromTo, startDate, endDate);
			}

			return ExecuteQuery(query =>
				{
					string statement = _dialect.GetCommitsFromToInstant;
					query.AddParameter(_dialect.BucketId, bucketId, DbType.AnsiString);
					query.AddParameter(_dialect.CommitStampStart, startDate, _dialect.GetDateTimeDbType());
					query.AddParameter(_dialect.CommitStampEnd, endDate, _dialect.GetDateTimeDbType());
					return query.ExecutePagedQuery(statement, (_, _) => { })
						.Select(x => x.GetCommit(_serializer, _eventSerializer, _dialect));
				});
		}

		/// <inheritdoc/>
		public virtual ICommit Commit(CommitAttempt attempt)
		{
			ICommit commit;
			try
			{
				commit = PersistCommit(attempt);
				if (Logger.IsEnabled(LogLevel.Debug))
				{
					Logger.LogDebug(Messages.CommitPersisted, attempt.CommitId);
				}
			}
			catch (UniqueKeyViolationException e)
			{
				if (DetectDuplicate(attempt))
				{
					var msg = String.Format(Messages.DuplicateCommit, attempt.CommitId, attempt.BucketId, attempt.StreamId, attempt.CommitSequence);
					if (Logger.IsEnabled(LogLevel.Information))
					{
						Logger.LogInformation(msg);
					}
					throw new DuplicateCommitException($"{msg} inner exception: {e.Message}", e);
				}

				if (Logger.IsEnabled(LogLevel.Information))
				{
					Logger.LogInformation(Messages.ConcurrentWriteDetected);
				}
				throw new ConcurrencyException(e.Message, e);
			}
			return commit;
		}

		/// <inheritdoc/>
		public virtual IEnumerable<IStreamHead> GetStreamsToSnapshot(string bucketId, int maxThreshold)
		{
			if (Logger.IsEnabled(LogLevel.Debug))
			{
				Logger.LogDebug(Messages.GettingStreamsToSnapshot);
			}

			return ExecuteQuery(query =>
				{
					string statement = _dialect.GetStreamsRequiringSnapshots;
					query.AddParameter(_dialect.BucketId, bucketId, DbType.AnsiString);
					query.AddParameter(_dialect.Threshold, maxThreshold);
					return
						query.ExecutePagedQuery(statement,
							(q, s) => { } // There is no need for next page delegate in the Snapshot stream
							.Select(x => x.GetStreamToSnapshot());
				});
		}

		/// <inheritdoc/>
		public virtual ISnapshot GetSnapshot(string bucketId, string streamId, int maxRevision)
		{
			if (Logger.IsEnabled(LogLevel.Debug))
			{
				Logger.LogDebug(Messages.GettingRevision, streamId, maxRevision);
			}

			var streamIdHash = _streamIdHasher.GetHash(streamId);
			return ExecuteQuery(query =>
				{
					string statement = _dialect.GetSnapshot;
					query.AddParameter(_dialect.BucketId, bucketId, DbType.AnsiString);
					query.AddParameter(_dialect.StreamId, streamIdHash, DbType.AnsiString);
					query.AddParameter(_dialect.StreamRevision, maxRevision);
					return query.ExecuteWithQuery(statement).Select(x => x.GetSnapshot(_serializer, streamId));
				}).FirstOrDefault();
		}

		/// <inheritdoc/>
		public virtual bool AddSnapshot(ISnapshot snapshot)
		{
			if (Logger.IsEnabled(LogLevel.Debug))
			{
				Logger.LogDebug(Messages.AddingSnapshot, snapshot.StreamId, snapshot.StreamRevision);
			}

			string streamId = _streamIdHasher.GetHash(snapshot.StreamId);
			return ExecuteCommand((connection, cmd) =>
				{
					cmd.AddParameter(_dialect.BucketId, snapshot.BucketId, DbType.AnsiString);
					cmd.AddParameter(_dialect.StreamId, streamId, DbType.AnsiString);
					cmd.AddParameter(_dialect.StreamRevision, snapshot.StreamRevision);
					_dialect.AddPayloadParameter(_connectionFactory, connection, cmd, _serializer.Serialize(snapshot.Payload));
					return cmd.ExecuteWithoutExceptions(_dialect.AppendSnapshotToCommit);
				}) > 0;
		}

		/// <inheritdoc/>
		public virtual void Purge()
		{
			if (Logger.IsEnabled(LogLevel.Warning))
			{
				Logger.LogWarning(Messages.PurgingStorage);
			}
			ExecuteCommand(cmd => cmd.ExecuteNonQuery(_dialect.PurgeStorage));
		}

		/// <inheritdoc/>
		public void Purge(string bucketId)
		{
			if (Logger.IsEnabled(LogLevel.Warning))
			{
				Logger.LogWarning(Messages.PurgingBucket, bucketId);
			}
			ExecuteCommand(cmd =>
				{
					cmd.AddParameter(_dialect.BucketId, bucketId, DbType.AnsiString);
					return cmd.ExecuteNonQuery(_dialect.PurgeBucket);
				});
		}

		/// <inheritdoc/>
		public void Drop()
		{
			if (Logger.IsEnabled(LogLevel.Warning))
			{
				Logger.LogWarning(Messages.DroppingTables);
			}
			ExecuteCommand(cmd => cmd.ExecuteNonQuery(_dialect.Drop));
		}

		/// <inheritdoc/>
		public void DeleteStream(string bucketId, string streamId)
		{
			if (Logger.IsEnabled(LogLevel.Warning))
			{
				Logger.LogWarning(Messages.DeletingStream, streamId, bucketId);
			}

			streamId = _streamIdHasher.GetHash(streamId);
			ExecuteCommand(cmd =>
				{
					cmd.AddParameter(_dialect.BucketId, bucketId, DbType.AnsiString);
					cmd.AddParameter(_dialect.StreamId, streamId, DbType.AnsiString);
					return cmd.ExecuteNonQuery(_dialect.DeleteStream);
				});
		}

		/// <inheritdoc/>
		public IEnumerable<ICommit> GetFrom(string bucketId, Int64 checkpointToken)
		{
			if (Logger.IsEnabled(LogLevel.Debug))
			{
				Logger.LogDebug(Messages.GettingAllCommitsFromBucketAndCheckpoint, bucketId, checkpointToken);
			}

			return ExecuteQuery(query =>
			{
				string statement = _dialect.GetCommitsFromBucketAndCheckpoint;
				query.AddParameter(_dialect.BucketId, bucketId, DbType.AnsiString);
				query.AddParameter(_dialect.CheckpointNumber, checkpointToken);
				return query.ExecutePagedQuery(statement, (_, _) => { })
					.Select(x => x.GetCommit(_serializer, _eventSerializer, _dialect));
			});
		}

		/// <inheritdoc/>
		public IEnumerable<ICommit> GetFromTo(String bucketId, Int64 fromCheckpointToken, Int64 toCheckpointToken)
		{
			if (Logger.IsEnabled(LogLevel.Debug))
			{
				Logger.LogDebug(Messages.GettingCommitsFromBucketAndFromToCheckpoint, bucketId, fromCheckpointToken, toCheckpointToken);
			}

			return ExecuteQuery(query =>
			{
				string statement = _dialect.GetCommitsFromToBucketAndCheckpoint;
				query.AddParameter(_dialect.BucketId, bucketId, DbType.AnsiString);
				query.AddParameter(_dialect.FromCheckpointNumber, fromCheckpointToken);
				query.AddParameter(_dialect.ToCheckpointNumber, toCheckpointToken);
				return query.ExecutePagedQuery(statement, (_, _) => { })
					.Select(x => x.GetCommit(_serializer, _eventSerializer, _dialect));
			});
		}

		/// <inheritdoc/>
		public IEnumerable<ICommit> GetFrom(Int64 checkpointToken)
		{
			if (Logger.IsEnabled(LogLevel.Debug))
			{
				Logger.LogDebug(Messages.GettingAllCommitsFromCheckpoint, checkpointToken);
			}

			return ExecuteQuery(query =>
			{
				string statement = _dialect.GetCommitsFromCheckpoint;
				query.AddParameter(_dialect.CheckpointNumber, checkpointToken);
				return query.ExecutePagedQuery(statement, (_, _) => { })
					.Select(x => x.GetCommit(_serializer, _eventSerializer, _dialect));
			});
		}

		/// <inheritdoc/>
		public IEnumerable<ICommit> GetFromTo(Int64 fromCheckpointToken, Int64 toCheckpointToken)
		{
			if (Logger.IsEnabled(LogLevel.Debug))
			{
				Logger.LogDebug(Messages.GettingCommitsFromToCheckpoint, fromCheckpointToken, toCheckpointToken);
			}

			return ExecuteQuery(query =>
			{
				string statement = _dialect.GetCommitsFromToCheckpoint;
				query.AddParameter(_dialect.FromCheckpointNumber, fromCheckpointToken);
				query.AddParameter(_dialect.ToCheckpointNumber, toCheckpointToken);
				return query.ExecutePagedQuery(statement, (_, _) => { })
					.Select(x => x.GetCommit(_serializer, _eventSerializer, _dialect));
			});
		}

		/// <inheritdoc/>
		public bool IsDisposed
		{
			get { return _disposed; }
		}

		/// <inheritdoc/>
		protected virtual void Dispose(bool disposing)
		{
			if (!disposing || _disposed)
			{
				return;
			}

			if (Logger.IsEnabled(LogLevel.Debug))
			{
				Logger.LogDebug(Messages.ShuttingDownPersistence);
			}
			_disposed = true;
		}

		/// <inheritdoc/>
		protected virtual void OnPersistCommit(IDbStatement cmd, CommitAttempt attempt)
		{ }

		private ICommit PersistCommit(CommitAttempt attempt)
		{
			if (Logger.IsEnabled(LogLevel.Debug))
			{
				Logger.LogDebug(Messages.AttemptingToCommit, attempt.Events.Count, attempt.StreamId, attempt.CommitSequence, attempt.BucketId);
			}

			string streamId = _streamIdHasher.GetHash(attempt.StreamId);
			return ExecuteCommand((connection, cmd) =>
			{
				cmd.AddParameter(_dialect.BucketId, attempt.BucketId, DbType.AnsiString);
				cmd.AddParameter(_dialect.StreamId, streamId, DbType.AnsiString);
				cmd.AddParameter(_dialect.StreamIdOriginal, attempt.StreamId);
				cmd.AddParameter(_dialect.StreamRevision, attempt.StreamRevision);
				cmd.AddParameter(_dialect.Items, attempt.Events.Count);
				cmd.AddParameter(_dialect.CommitId, attempt.CommitId);
				cmd.AddParameter(_dialect.CommitSequence, attempt.CommitSequence);
				cmd.AddParameter(_dialect.CommitStamp, attempt.CommitStamp, _dialect.GetDateTimeDbType());
				cmd.AddParameter(_dialect.Headers, _serializer.Serialize(attempt.Headers));
				_dialect.AddPayloadParameter(_connectionFactory, connection, cmd, _eventSerializer.SerializeEventMessages(attempt.Events));
				OnPersistCommit(cmd, attempt);
				var checkpointNumber = cmd.ExecuteScalar(_dialect.PersistCommit).ToLong();
				return new Commit(
					attempt.BucketId,
					attempt.StreamId,
					attempt.StreamRevision,
					attempt.CommitId,
					attempt.CommitSequence,
					attempt.CommitStamp,
					checkpointNumber,
					attempt.Headers,
					attempt.Events);
			});
		}

		private bool DetectDuplicate(CommitAttempt attempt)
		{
			string streamId = _streamIdHasher.GetHash(attempt.StreamId);
			return ExecuteCommand(cmd =>
				{
					cmd.AddParameter(_dialect.BucketId, attempt.BucketId, DbType.AnsiString);
					cmd.AddParameter(_dialect.StreamId, streamId, DbType.AnsiString);
					cmd.AddParameter(_dialect.CommitId, attempt.CommitId);
					cmd.AddParameter(_dialect.CommitSequence, attempt.CommitSequence);
					object value = cmd.ExecuteScalar(_dialect.DuplicateCommit);
					return (value is long val ? val : (int)value) > 0;
				});
		}

		/// <summary>
		/// Executes a query against the database.
		/// </summary>
		/// <exception cref="StorageException"></exception>
		protected virtual IEnumerable<T> ExecuteQuery<T>(Func<IDbStatement, IEnumerable<T>> query)
		{
			ThrowWhenDisposed();

			TransactionScope? scope = OpenQueryScope();
			ConnectionScope? connection = null;
			DbTransaction? transaction = null;
			IDbStatement? statement = null;

			try
			{
				connection = _connectionFactory.Open();
				transaction = _dialect.OpenTransaction(connection.Current);
				statement = _dialect.BuildStatement(scope, connection, transaction);
				statement.PageSize = _pageSize;

				if (Logger.IsEnabled(LogLevel.Trace))
				{
					Logger.LogTrace(Messages.ExecutingQuery);
				}
				return query(statement);
			}
			catch (Exception e)
			{
				statement?.Dispose();
				transaction?.Dispose();
				connection?.Dispose();
				scope?.Dispose();

				if (Logger.IsEnabled(LogLevel.Debug))
				{
					Logger.LogDebug(Messages.StorageThrewException, e.GetType());
				}
				if (e is StorageUnavailableException)
				{
					throw;
				}

				throw new StorageException(e.Message, e);
			}
		}

		/// <summary>
		/// Opens a new query scope if a <see cref="TransactionScopeOption"/> was configured.
		/// </summary>
		protected virtual TransactionScope? OpenQueryScope()
		{
			if (_scopeOption == null)
			{
				return null;
			}
			return OpenCommandScope() ?? new TransactionScope(TransactionScopeOption.Suppress
				, TransactionScopeAsyncFlowOption.Enabled
				);
		}

		/// <summary>
		/// Throws an <see cref="ObjectDisposedException"/> if the instance has been disposed.
		/// </summary>
		/// <exception cref="ObjectDisposedException"></exception>
		protected void ThrowWhenDisposed()
		{
			if (!_disposed)
			{
				return;
			}

			if (Logger.IsEnabled(LogLevel.Warning))
			{
				Logger.LogWarning(Messages.AlreadyDisposed);
			}
			throw new ObjectDisposedException(Messages.AlreadyDisposed);
		}

		/// <summary>
		/// Executes a command against the database.
		/// </summary>
		protected T ExecuteCommand<T>(Func<IDbStatement, T> command)
		{
			return ExecuteCommand((_, statement) => command(statement));
		}

		/// <summary>
		/// Executes a command against the database.
		/// </summary>
		/// <exception cref="StorageException"></exception>
		protected virtual T ExecuteCommand<T>(Func<DbConnection, IDbStatement, T> command)
		{
			ThrowWhenDisposed();

			using (var scope = OpenCommandScope())
			using (var connection = _connectionFactory.Open())
			using (var transaction = _dialect.OpenTransaction(connection.Current))
			using (IDbStatement statement = _dialect.BuildStatement(scope, connection, transaction))
			{
				try
				{
					if (Logger.IsEnabled(LogLevel.Trace))
					{
						Logger.LogTrace(Messages.ExecutingCommand);
					}
					T rowsAffected = command(connection.Current, statement);
					if (Logger.IsEnabled(LogLevel.Trace))
					{
						Logger.LogTrace(Messages.CommandExecuted, rowsAffected);
					}

					transaction?.Commit();

					scope?.Complete();

					return rowsAffected;
				}
				catch (Exception e)
				{
					if (Logger.IsEnabled(LogLevel.Debug))
					{
						Logger.LogDebug(Messages.StorageThrewException, e.GetType());
					}
					if (!RecoverableException(e))
					{
						throw new StorageException(e.Message, e);
					}

					if (Logger.IsEnabled(LogLevel.Information))
					{
						Logger.LogInformation(Messages.RecoverableExceptionCompletesScope);
					}

					scope?.Complete();

					throw;
				}
			}
		}

		/// <summary>
		/// Opens a new command scope if a <see cref="TransactionScopeOption"/> was configured.
		/// </summary>
		protected virtual TransactionScope? OpenCommandScope()
		{
			if (_scopeOption == null)
			{
				return null;
			}
			if (Transaction.Current == null)
			{
				return new TransactionScope(_scopeOption.Value, new TransactionOptions
				{
					IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted
				}
				, TransactionScopeAsyncFlowOption.Enabled
				);
			}
			// todo: maybe add a warning for the isolation level
			/*
			if (Transaction.Current.IsolationLevel == System.Transactions.IsolationLevel.Serializable)
			{
				if (Logger.IsWarnEnabled) Logger.Warn("Serializable can be troublesome");
			}
			*/
			return new TransactionScope(_scopeOption.Value
				, TransactionScopeAsyncFlowOption.Enabled
				);
		}

		private static bool RecoverableException(Exception e)
		{
			return e is UniqueKeyViolationException || e is StorageUnavailableException;
		}

		private class StreamIdHasherValidator : IStreamIdHasher
		{
			private readonly IStreamIdHasher _streamIdHasher;
			private const int MaxStreamIdHashLength = 40;

			public StreamIdHasherValidator(IStreamIdHasher streamIdHasher)
			{
				_streamIdHasher = streamIdHasher ?? throw new ArgumentNullException(nameof(streamIdHasher));
			}

			public string GetHash(string streamId)
			{
				if (string.IsNullOrWhiteSpace(streamId))
				{
					throw new ArgumentException(Messages.StreamIdIsNullEmptyOrWhiteSpace);
				}
				string streamIdHash = _streamIdHasher.GetHash(streamId);
				if (string.IsNullOrWhiteSpace(streamIdHash))
				{
					throw new InvalidOperationException(Messages.StreamIdHashIsNullEmptyOrWhiteSpace);
				}
				if (streamIdHash.Length > MaxStreamIdHashLength)
				{
					throw new InvalidOperationException(Messages.StreamIdHashTooLong.FormatWith(streamId, streamIdHash, streamIdHash.Length, MaxStreamIdHashLength));
				}
				return streamIdHash;
			}
		}
	}
}
