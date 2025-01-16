using Microsoft.Extensions.Logging;
using System.Data;
using System.Data.Common;
using NEventStore.Serialization;

namespace NEventStore.Persistence.Sql
{
	public partial class SqlPersistenceEngine
	{
		/// <inheritdoc/>
		public Task GetFromAsync(long checkpointToken, IAsyncObserver<ICommit> asyncObserver, CancellationToken cancellationToken)
		{
			if (Logger.IsEnabled(LogLevel.Debug))
			{
				Logger.LogDebug(Messages.GettingAllCommitsFromCheckpoint, checkpointToken);
			}

			LambdaAsyncObserver<IDataRecord> adapter = BuildICommitAsyncObserverAdapter(asyncObserver, cancellationToken);

			return ExecuteQueryAsync((query, asyncObserver, cancellationToken) =>
			{
				string statement = _dialect.GetCommitsFromCheckpoint;
				query.AddParameter(_dialect.CheckpointNumber, checkpointToken);
				return query.ExecutePagedQueryAsync(statement, (_, _) => { }, asyncObserver, cancellationToken);
			},
			adapter, cancellationToken);
		}

		/// <inheritdoc/>
		public Task GetFromToAsync(long fromCheckpointToken, long toCheckpointToken, IAsyncObserver<ICommit> asyncObserver, CancellationToken cancellationToken)
		{
			if (Logger.IsEnabled(LogLevel.Debug))
			{
				Logger.LogDebug(Messages.GettingCommitsFromToCheckpoint, fromCheckpointToken, toCheckpointToken);
			}

			LambdaAsyncObserver<IDataRecord> adapter = BuildICommitAsyncObserverAdapter(asyncObserver, cancellationToken);

			return ExecuteQueryAsync((query, asyncObserver, cancellationToken) =>
			{
				string statement = _dialect.GetCommitsFromToCheckpoint;
				query.AddParameter(_dialect.FromCheckpointNumber, fromCheckpointToken);
				query.AddParameter(_dialect.ToCheckpointNumber, toCheckpointToken);
				return query.ExecutePagedQueryAsync(statement, (_, _) => { }, asyncObserver, cancellationToken);
			},
			adapter, cancellationToken);
		}

		/// <inheritdoc/>
		public Task GetFromAsync(string bucketId, long checkpointToken, IAsyncObserver<ICommit> asyncObserver, CancellationToken cancellationToken)
		{
			if (Logger.IsEnabled(LogLevel.Debug))
			{
				Logger.LogDebug(Messages.GettingAllCommitsFromBucketAndCheckpoint, bucketId, checkpointToken);
			}

			LambdaAsyncObserver<IDataRecord> adapter = BuildICommitAsyncObserverAdapter(asyncObserver, cancellationToken);

			return ExecuteQueryAsync((query, asyncObserver, cancellationToken) =>
			{
				string statement = _dialect.GetCommitsFromBucketAndCheckpoint;
				query.AddParameter(_dialect.BucketId, bucketId, DbType.AnsiString);
				query.AddParameter(_dialect.CheckpointNumber, checkpointToken);
				return query.ExecutePagedQueryAsync(statement, (_, _) => { }, asyncObserver, cancellationToken);
			},
			adapter, cancellationToken);
		}

		/// <inheritdoc/>
		public Task GetFromToAsync(string bucketId, long fromCheckpointToken, long toCheckpointToken, IAsyncObserver<ICommit> asyncObserver, CancellationToken cancellationToken)
		{
			if (Logger.IsEnabled(LogLevel.Debug))
			{
				Logger.LogDebug(Messages.GettingCommitsFromBucketAndFromToCheckpoint, bucketId, fromCheckpointToken, toCheckpointToken);
			}

			LambdaAsyncObserver<IDataRecord> adapter = BuildICommitAsyncObserverAdapter(asyncObserver, cancellationToken);

			return ExecuteQueryAsync((query, asyncObserver, cancellationToken) =>
			{
				string statement = _dialect.GetCommitsFromToBucketAndCheckpoint;
				query.AddParameter(_dialect.BucketId, bucketId, DbType.AnsiString);
				query.AddParameter(_dialect.FromCheckpointNumber, fromCheckpointToken);
				query.AddParameter(_dialect.ToCheckpointNumber, toCheckpointToken);
				return query.ExecutePagedQueryAsync(statement, (_, _) => { }, asyncObserver, cancellationToken);
			},
			adapter, cancellationToken);
		}

		/// <inheritdoc/>
		public Task PurgeAsync(CancellationToken cancellationToken)
		{
			if (Logger.IsEnabled(LogLevel.Warning))
			{
				Logger.LogWarning(Messages.PurgingStorage);
			}
			return ExecuteCommandAsync((cmd, cancellationToken) => cmd.ExecuteNonQueryAsync(_dialect.PurgeStorage, cancellationToken), cancellationToken);
		}

		/// <inheritdoc/>
		public Task PurgeAsync(string bucketId, CancellationToken cancellationToken)
		{
			if (Logger.IsEnabled(LogLevel.Warning))
			{
				Logger.LogWarning(Messages.PurgingBucket, bucketId);
			}
			return ExecuteCommandAsync((cmd, cancellationToken) =>
			{
				cmd.AddParameter(_dialect.BucketId, bucketId, DbType.AnsiString);
				return cmd.ExecuteNonQueryAsync(_dialect.PurgeBucket, cancellationToken);
			}, cancellationToken);
		}

		/// <inheritdoc/>
		public Task DeleteStreamAsync(string bucketId, string streamId, CancellationToken cancellationToken)
		{
			if (Logger.IsEnabled(LogLevel.Warning))
			{
				Logger.LogWarning(Messages.DeletingStream, streamId, bucketId);
			}

			streamId = _streamIdHasher.GetHash(streamId);
			return ExecuteCommandAsync((cmd, cancellationToken) =>
			{
				cmd.AddParameter(_dialect.BucketId, bucketId, DbType.AnsiString);
				cmd.AddParameter(_dialect.StreamId, streamId, DbType.AnsiString);
				return cmd.ExecuteNonQueryAsync(_dialect.DeleteStream, cancellationToken);
			}, cancellationToken);
		}

		/// <inheritdoc/>
		public Task GetFromAsync(string bucketId, string streamId, int minRevision, int maxRevision, IAsyncObserver<ICommit> observer, CancellationToken cancellationToken)
		{
			if (Logger.IsEnabled(LogLevel.Debug))
			{
				Logger.LogDebug(Messages.GettingAllCommitsBetween, streamId, minRevision, maxRevision);
			}

			streamId = _streamIdHasher.GetHash(streamId);
			LambdaAsyncObserver<IDataRecord> adapter = BuildICommitAsyncObserverAdapter(observer, cancellationToken);

			return ExecuteQueryAsync((query, asyncObserver, cancellationToken) =>
			{
				string statement = _dialect.GetCommitsFromStartingRevision;
				query.AddParameter(_dialect.BucketId, bucketId, DbType.AnsiString);
				query.AddParameter(_dialect.StreamId, streamId, DbType.AnsiString);
				query.AddParameter(_dialect.StreamRevision, minRevision);
				query.AddParameter(_dialect.MaxStreamRevision, maxRevision);
				query.AddParameter(_dialect.CommitSequence, 0);
				return query.ExecutePagedQueryAsync(statement, _dialect.NextPageDelegate, asyncObserver, cancellationToken);
			},
			adapter, cancellationToken);
		}

		private LambdaAsyncObserver<IDataRecord> BuildICommitAsyncObserverAdapter(IAsyncObserver<ICommit> observer, CancellationToken cancellationToken)
		{
			return new LambdaAsyncObserver<IDataRecord>(
				onNextAsync: (record, _) => observer.OnNextAsync(record.GetCommit(_serializer, _eventSerializer, _dialect), cancellationToken),
				onErrorAsync: observer.OnErrorAsync,
				onCompletedAsync: observer.OnCompletedAsync
				);
		}

		/// <inheritdoc/>
		public async Task<ICommit?> CommitAsync(CommitAttempt attempt, CancellationToken cancellationToken)
		{
			ICommit commit;
			try
			{
				commit = await PersistCommitAsync(attempt, cancellationToken).ConfigureAwait(false);
				if (Logger.IsEnabled(LogLevel.Debug))
				{
					Logger.LogDebug(Messages.CommitPersisted, attempt.CommitId);
				}
			}
			catch (UniqueKeyViolationException e)
			{
				if (await DetectDuplicateAsync(attempt, cancellationToken).ConfigureAwait(false))
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
		public async Task<ISnapshot?> GetSnapshotAsync(string bucketId, string streamId, int maxRevision, CancellationToken cancellationToken)
		{
			if (Logger.IsEnabled(LogLevel.Debug))
			{
				Logger.LogDebug(Messages.GettingRevision, streamId, maxRevision);
			}

			var streamIdHash = _streamIdHasher.GetHash(streamId);

			ISnapshot? snapshot = null;

			var asyncObserver = new LambdaAsyncObserver<IDataRecord>((x, _) => { snapshot = x.GetSnapshot(_serializer, streamId); return Task.FromResult(true); });

			await ExecuteQueryAsync((query, asyncObserver, cancellationToken) =>
			{
				string statement = _dialect.GetSnapshot;
				query.AddParameter(_dialect.BucketId, bucketId, DbType.AnsiString);
				query.AddParameter(_dialect.StreamId, streamIdHash, DbType.AnsiString);
				query.AddParameter(_dialect.StreamRevision, maxRevision);
				return query.ExecuteWithQueryAsync(statement, asyncObserver, cancellationToken);
			}, asyncObserver, cancellationToken).ConfigureAwait(false);

			return snapshot;
		}

		/// <inheritdoc/>
		public async Task<bool> AddSnapshotAsync(ISnapshot snapshot, CancellationToken cancellationToken)
		{
			if (Logger.IsEnabled(LogLevel.Debug))
			{
				Logger.LogDebug(Messages.AddingSnapshot, snapshot.StreamId, snapshot.StreamRevision);
			}

			string streamId = _streamIdHasher.GetHash(snapshot.StreamId);
			var result = await ExecuteCommandAsync((connection, cmd, cancellationToken) =>
			{
				cmd.AddParameter(_dialect.BucketId, snapshot.BucketId, DbType.AnsiString);
				cmd.AddParameter(_dialect.StreamId, streamId, DbType.AnsiString);
				cmd.AddParameter(_dialect.StreamRevision, snapshot.StreamRevision);
				_dialect.AddPayloadParameter(_connectionFactory, connection, cmd, _serializer.Serialize(snapshot.Payload));
				return cmd.ExecuteWithoutExceptionsAsync(_dialect.AppendSnapshotToCommit, cancellationToken);
			}, cancellationToken)
				.ConfigureAwait(false);
			return result > 0;
		}

		/// <inheritdoc/>
		public Task GetStreamsToSnapshotAsync(string bucketId, int maxThreshold, IAsyncObserver<IStreamHead> asyncObserver, CancellationToken cancellationToken)
		{
			if (Logger.IsEnabled(LogLevel.Debug))
			{
				Logger.LogDebug(Messages.GettingStreamsToSnapshot);
			}

			var adapter = new LambdaAsyncObserver<IDataRecord>(
				onNextAsync: (record, _) => asyncObserver.OnNextAsync(record.GetStreamToSnapshot(), cancellationToken),
				onErrorAsync: asyncObserver.OnErrorAsync,
				onCompletedAsync: asyncObserver.OnCompletedAsync
				);
			return ExecuteQueryAsync((query, asyncObserver, cancellationToken) =>
			{
				string statement = _dialect.GetStreamsRequiringSnapshots;
				query.AddParameter(_dialect.BucketId, bucketId, DbType.AnsiString);
				query.AddParameter(_dialect.Threshold, maxThreshold);
				return query.ExecutePagedQueryAsync(
					statement, (q, s) => q.SetParameter(_dialect.StreamId, _dialect.CoalesceParameterValue(s.StreamId()), DbType.AnsiString), // todo: I'm not sure this is used, the query does not have a "StreamId" parameter
					asyncObserver, cancellationToken);
			},
			adapter, cancellationToken);
		}

		/// <summary>
		/// Executes a command against the database.
		/// </summary>
		protected Task<T> ExecuteCommandAsync<T>(Func<IDbStatement, CancellationToken, Task<T>> command, CancellationToken cancellationToken)
		{
			return ExecuteCommandAsync((_, statement, cancellationToken) => command(statement, cancellationToken), cancellationToken);
		}

		/// <summary>
		/// Executes a command against the database.
		/// </summary>
		/// <exception cref="StorageException"></exception>
		protected virtual async Task<T> ExecuteCommandAsync<T>(Func<DbConnection, IDbStatement, CancellationToken, Task<T>> command, CancellationToken cancellationToken)
		{
			ThrowWhenDisposed();

			using (var scope = OpenCommandScope())
			using (var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false))
			using (var transaction = _dialect.OpenTransaction(connection.Current))
			using (IDbStatement statement = _dialect.BuildStatement(scope, connection, transaction))
			{
				try
				{
					if (Logger.IsEnabled(LogLevel.Trace))
					{
						Logger.LogTrace(Messages.ExecutingCommand);
					}
					T rowsAffected = await command(connection.Current, statement, cancellationToken).ConfigureAwait(false);
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

		private Task<Commit> PersistCommitAsync(CommitAttempt attempt, CancellationToken cancellationToken)
		{
			if (Logger.IsEnabled(LogLevel.Debug))
			{
				Logger.LogDebug(Messages.AttemptingToCommit, attempt.Events.Count, attempt.StreamId, attempt.CommitSequence, attempt.BucketId);
			}

			string streamId = _streamIdHasher.GetHash(attempt.StreamId);
			return ExecuteCommandAsync(async (connection, cmd, cancellationToken) =>
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
				var checkpointNumber = (await cmd.ExecuteScalarAsync(_dialect.PersistCommit, cancellationToken).ConfigureAwait(false))
					.ToLong();
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
			}, cancellationToken);
		}

		private Task<bool> DetectDuplicateAsync(CommitAttempt attempt, CancellationToken cancellationToken)
		{
			string streamId = _streamIdHasher.GetHash(attempt.StreamId);
			return ExecuteCommandAsync(async (cmd, cancellationToken) =>
			{
				cmd.AddParameter(_dialect.BucketId, attempt.BucketId, DbType.AnsiString);
				cmd.AddParameter(_dialect.StreamId, streamId, DbType.AnsiString);
				cmd.AddParameter(_dialect.CommitId, attempt.CommitId);
				cmd.AddParameter(_dialect.CommitSequence, attempt.CommitSequence);
				object value = await cmd.ExecuteScalarAsync(_dialect.DuplicateCommit, cancellationToken).ConfigureAwait(false);
				return (value is long val ? val : (int)value) > 0;
			}, cancellationToken);
		}

		/// <summary>
		/// Executes a query against the database.
		/// </summary>
		/// <exception cref="StorageException"></exception>
		protected virtual async Task ExecuteQueryAsync(
			Func<IDbStatement, IAsyncObserver<IDataRecord>, CancellationToken, Task> query,
			IAsyncObserver<IDataRecord> asyncObserver,
			CancellationToken cancellationToken)
		{
			ThrowWhenDisposed();

			using (var scope = OpenCommandScope())
			using (var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false))
			using (var transaction = _dialect.OpenTransaction(connection.Current))
			using (IDbStatement statement = _dialect.BuildStatement(scope, connection, transaction))
			{
				try
				{
					statement.PageSize = _pageSize;

					if (Logger.IsEnabled(LogLevel.Trace))
					{
						Logger.LogTrace(Messages.ExecutingQuery);
					}
					await query(statement, asyncObserver, cancellationToken).ConfigureAwait(false);
				}
				catch (Exception e)
				{
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
		}
	}
}
