using Microsoft.Extensions.Logging;
using System.Data;
using System.Data.Common;
using NEventStore.Serialization;

namespace NEventStore.Persistence.Sql
{
	public partial class SqlPersistenceEngine
	{
		public Task GetFromAsync(long checkpointToken, IAsyncObserver<ICommit> asyncObserver, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}

		public Task GetFromToAsync(long fromCheckpointToken, long toCheckpointToken, IAsyncObserver<ICommit> asyncObserver, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}

		public Task GetFromAsync(string bucketId, long checkpointToken, IAsyncObserver<ICommit> asyncObserver, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}

		public Task GetFromToAsync(string bucketId, long fromCheckpointToken, long toCheckpointToken, IAsyncObserver<ICommit> asyncObserver, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
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

		public Task GetFromAsync(string bucketId, string streamId, int minRevision, int maxRevision, IAsyncObserver<ICommit> observer, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
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

		public Task<ISnapshot?> GetSnapshotAsync(string bucketId, string streamId, int maxRevision, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
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

		public Task GetStreamsToSnapshotAsync(string bucketId, int maxThreshold, IAsyncObserver<IStreamHead> asyncObserver, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
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
	}
}
