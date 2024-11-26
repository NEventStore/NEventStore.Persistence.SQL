namespace NEventStore.Persistence.Sql
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Threading;
    using System.Transactions;
    using Microsoft.Extensions.Logging;
    using NEventStore.Logging;
    using NEventStore.Serialization;

    public class SqlPersistenceEngine : IPersistStreams
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

        public SqlPersistenceEngine(
            IConnectionFactory connectionFactory,
            ISqlDialect dialect,
            ISerialize serializer,
            ISerializeEvents eventSerializer,
            int pageSize,
            TransactionScopeOption? scopeOption = null)
            : this(connectionFactory, dialect, serializer, eventSerializer, pageSize, new Sha1StreamIdHasher(), scopeOption)
        { }

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

            Logger.LogDebug(Messages.UsingScope, _scopeOption.ToString());
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public virtual void Initialize()
        {
            if (Interlocked.Increment(ref _initialized) > 1)
            {
                return;
            }

            Logger.LogDebug(Messages.InitializingStorage);
            ExecuteCommand(statement => statement.ExecuteWithoutExceptions(_dialect.InitializeStorage));
        }

        public virtual IEnumerable<ICommit> GetFrom(string bucketId, string streamId, int minRevision, int maxRevision)
        {
            Logger.LogDebug(Messages.GettingAllCommitsBetween, streamId, minRevision, maxRevision);
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

        public virtual IEnumerable<ICommit> GetFrom(string bucketId, DateTime start)
        {
            start = start.AddTicks(-(start.Ticks % TimeSpan.TicksPerSecond)); // Rounds down to the nearest second.
            start = start < EpochTime ? EpochTime : start;

            Logger.LogDebug(Messages.GettingAllCommitsFrom, start, bucketId);
            return ExecuteQuery(query =>
                {
                    string statement = _dialect.GetCommitsFromInstant;
                    query.AddParameter(_dialect.BucketId, bucketId, DbType.AnsiString);
                    query.AddParameter(_dialect.CommitStamp, start, _dialect.GetDateTimeDbType());
                    return query.ExecutePagedQuery(statement, (q, r) => { })
                            .Select(x => x.GetCommit(_serializer, _eventSerializer, _dialect));
                });
        }

        public virtual IEnumerable<ICommit> GetFromTo(string bucketId, DateTime start, DateTime end)
        {
            start = start.AddTicks(-(start.Ticks % TimeSpan.TicksPerSecond)); // Rounds down to the nearest second.
            start = start < EpochTime ? EpochTime : start;
            end = end < EpochTime ? EpochTime : end;

            Logger.LogDebug(Messages.GettingAllCommitsFromTo, start, end);
            return ExecuteQuery(query =>
                {
                    string statement = _dialect.GetCommitsFromToInstant;
                    query.AddParameter(_dialect.BucketId, bucketId, DbType.AnsiString);
                    query.AddParameter(_dialect.CommitStampStart, start, _dialect.GetDateTimeDbType());
                    query.AddParameter(_dialect.CommitStampEnd, end, _dialect.GetDateTimeDbType());
                    return query.ExecutePagedQuery(statement, (q, r) => { })
                        .Select(x => x.GetCommit(_serializer, _eventSerializer, _dialect));
                });
        }

        public virtual ICommit Commit(CommitAttempt attempt)
        {
            ICommit commit;
            try
            {
                commit = PersistCommit(attempt);
                Logger.LogDebug(Messages.CommitPersisted, attempt.CommitId);
            }
            catch (Exception e)
            {
                if (!(e is UniqueKeyViolationException))
                {
                    throw;
                }

                if (DetectDuplicate(attempt))
                {
                    var msg = String.Format(Messages.DuplicateCommit, attempt.CommitId, attempt.BucketId, attempt.StreamId, attempt.CommitSequence);
                    Logger.LogInformation(msg);
                    throw new DuplicateCommitException($"{msg} inner exception: {e.Message}", e);
                }

                Logger.LogInformation(Messages.ConcurrentWriteDetected);
                throw new ConcurrencyException(e.Message, e);
            }
            return commit;
        }

        public virtual IEnumerable<IStreamHead> GetStreamsToSnapshot(string bucketId, int maxThreshold)
        {
            Logger.LogDebug(Messages.GettingStreamsToSnapshot);
            return ExecuteQuery(query =>
                {
                    string statement = _dialect.GetStreamsRequiringSnapshots;
                    query.AddParameter(_dialect.BucketId, bucketId, DbType.AnsiString);
                    query.AddParameter(_dialect.Threshold, maxThreshold);
                    return
                        query.ExecutePagedQuery(statement,
                            (q, s) => q.SetParameter(_dialect.StreamId, _dialect.CoalesceParameterValue(s.StreamId()), DbType.AnsiString))
                            .Select(x => x.GetStreamToSnapshot());
                });
        }

        public virtual ISnapshot GetSnapshot(string bucketId, string streamId, int maxRevision)
        {
            Logger.LogDebug(Messages.GettingRevision, streamId, maxRevision);
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

        public virtual bool AddSnapshot(ISnapshot snapshot)
        {
            Logger.LogDebug(Messages.AddingSnapshot, snapshot.StreamId, snapshot.StreamRevision);
            string streamId = _streamIdHasher.GetHash(snapshot.StreamId);
            return ExecuteCommand((connection, cmd) =>
                {
                    cmd.AddParameter(_dialect.BucketId, snapshot.BucketId, DbType.AnsiString);
                    cmd.AddParameter(_dialect.StreamId, streamId, DbType.AnsiString);
                    cmd.AddParameter(_dialect.StreamRevision, snapshot.StreamRevision);
                    _dialect.AddPayloadParamater(_connectionFactory, connection, cmd, _serializer.Serialize(snapshot.Payload));
                    return cmd.ExecuteWithoutExceptions(_dialect.AppendSnapshotToCommit);
                }) > 0;
        }

        public virtual void Purge()
        {
            Logger.LogWarning(Messages.PurgingStorage);
            ExecuteCommand(cmd => cmd.ExecuteNonQuery(_dialect.PurgeStorage));
        }

        public void Purge(string bucketId)
        {
            Logger.LogWarning(Messages.PurgingBucket, bucketId);
            ExecuteCommand(cmd =>
                {
                    cmd.AddParameter(_dialect.BucketId, bucketId, DbType.AnsiString);
                    return cmd.ExecuteNonQuery(_dialect.PurgeBucket);
                });
        }

        public void Drop()
        {
            Logger.LogWarning(Messages.DroppingTables);
            ExecuteCommand(cmd => cmd.ExecuteNonQuery(_dialect.Drop));
        }

        public void DeleteStream(string bucketId, string streamId)
        {
            Logger.LogWarning(Messages.DeletingStream, streamId, bucketId);
            streamId = _streamIdHasher.GetHash(streamId);
            ExecuteCommand(cmd =>
                {
                    cmd.AddParameter(_dialect.BucketId, bucketId, DbType.AnsiString);
                    cmd.AddParameter(_dialect.StreamId, streamId, DbType.AnsiString);
                    return cmd.ExecuteNonQuery(_dialect.DeleteStream);
                });
        }

        public IEnumerable<ICommit> GetFrom(string bucketId, Int64 checkpointToken)
        {
            Logger.LogDebug(Messages.GettingAllCommitsFromBucketAndCheckpoint, bucketId, checkpointToken);
            return ExecuteQuery(query =>
            {
                string statement = _dialect.GetCommitsFromBucketAndCheckpoint;
                query.AddParameter(_dialect.BucketId, bucketId, DbType.AnsiString);
                query.AddParameter(_dialect.CheckpointNumber, checkpointToken);
                return query.ExecutePagedQuery(statement, (q, r) => { })
                    .Select(x => x.GetCommit(_serializer, _eventSerializer, _dialect));
            });
        }

        public IEnumerable<ICommit> GetFromTo(String bucketId, Int64 from, Int64 to)
        {
            Logger.LogDebug(Messages.GettingCommitsFromBucketAndFromToCheckpoint, bucketId, from, to);
            return ExecuteQuery(query =>
            {
                string statement = _dialect.GetCommitsFromToBucketAndCheckpoint;
                query.AddParameter(_dialect.BucketId, bucketId, DbType.AnsiString);
                query.AddParameter(_dialect.FromCheckpointNumber, from);
                query.AddParameter(_dialect.ToCheckpointNumber, to);
                return query.ExecutePagedQuery(statement, (q, r) => { })
                    .Select(x => x.GetCommit(_serializer, _eventSerializer, _dialect));
            });
        }

        public IEnumerable<ICommit> GetFrom(Int64 checkpointToken)
        {
            Logger.LogDebug(Messages.GettingAllCommitsFromCheckpoint, checkpointToken);
            return ExecuteQuery(query =>
            {
                string statement = _dialect.GetCommitsFromCheckpoint;
                query.AddParameter(_dialect.CheckpointNumber, checkpointToken);
                return query.ExecutePagedQuery(statement, (q, r) => { })
                    .Select(x => x.GetCommit(_serializer, _eventSerializer, _dialect));
            });
        }

        public IEnumerable<ICommit> GetFromTo(Int64 from, Int64 to)
        {
            Logger.LogDebug(Messages.GettingCommitsFromToCheckpoint, from, to);
            return ExecuteQuery(query =>
            {
                string statement = _dialect.GetCommitsFromToCheckpoint;
                query.AddParameter(_dialect.FromCheckpointNumber, from);
                query.AddParameter(_dialect.ToCheckpointNumber, to);
                return query.ExecutePagedQuery(statement, (q, r) => { })
                    .Select(x => x.GetCommit(_serializer, _eventSerializer, _dialect));
            });
        }

        public bool IsDisposed
        {
            get { return _disposed; }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing || _disposed)
            {
                return;
            }

            Logger.LogDebug(Messages.ShuttingDownPersistence);
            _disposed = true;
        }

        protected virtual void OnPersistCommit(IDbStatement cmd, CommitAttempt attempt)
        { }

        private ICommit PersistCommit(CommitAttempt attempt)
        {
            Logger.LogDebug(Messages.AttemptingToCommit, attempt.Events.Count, attempt.StreamId, attempt.CommitSequence, attempt.BucketId);
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
                _dialect.AddPayloadParamater(_connectionFactory, connection, cmd, _eventSerializer.SerializeEventMessages(attempt.Events.ToList()));
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

        protected virtual IEnumerable<T> ExecuteQuery<T>(Func<IDbStatement, IEnumerable<T>> query)
        {
            ThrowWhenDisposed();

            TransactionScope scope = OpenQueryScope();
            IDbConnection connection = null;
            IDbTransaction transaction = null;
            IDbStatement statement = null;

            try
            {
                connection = _connectionFactory.Open();
                transaction = _dialect.OpenTransaction(connection);
                statement = _dialect.BuildStatement(scope, connection, transaction);
                statement.PageSize = _pageSize;

                Logger.LogTrace(Messages.ExecutingQuery);
                return query(statement);
            }
            catch (Exception e)
            {
                statement?.Dispose();
                transaction?.Dispose();
                connection?.Dispose();
                scope?.Dispose();

                Logger.LogDebug(Messages.StorageThrewException, e.GetType());
                if (e is StorageUnavailableException)
                {
                    throw;
                }

                throw new StorageException(e.Message, e);
            }
        }

        protected virtual TransactionScope OpenQueryScope()
        {
            if (_scopeOption == null)
            {
                return null;
            }
            return OpenCommandScope() ?? new TransactionScope(TransactionScopeOption.Suppress
                , TransactionScopeAsyncFlowOption.Enabled
                );
        }

        private void ThrowWhenDisposed()
        {
            if (!_disposed)
            {
                return;
            }

            Logger.LogWarning(Messages.AlreadyDisposed);
            throw new ObjectDisposedException(Messages.AlreadyDisposed);
        }

        private T ExecuteCommand<T>(Func<IDbStatement, T> command)
        {
            return ExecuteCommand((_, statement) => command(statement));
        }

        protected virtual T ExecuteCommand<T>(Func<IDbConnection, IDbStatement, T> command)
        {
            ThrowWhenDisposed();

            using (TransactionScope scope = OpenCommandScope())
            using (IDbConnection connection = _connectionFactory.Open())
            using (IDbTransaction transaction = _dialect.OpenTransaction(connection))
            using (IDbStatement statement = _dialect.BuildStatement(scope, connection, transaction))
            {
                try
                {
                    Logger.LogTrace(Messages.ExecutingCommand);
                    T rowsAffected = command(connection, statement);
                    Logger.LogTrace(Messages.CommandExecuted, rowsAffected);

                    transaction?.Commit();

                    scope?.Complete();

                    return rowsAffected;
                }
                catch (Exception e)
                {
                    Logger.LogDebug(Messages.StorageThrewException, e.GetType());
                    if (!RecoverableException(e))
                    {
                        throw new StorageException(e.Message, e);
                    }

                    Logger.LogInformation(Messages.RecoverableExceptionCompletesScope);

                    scope?.Complete();

                    throw;
                }
            }
        }

        protected virtual TransactionScope OpenCommandScope()
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