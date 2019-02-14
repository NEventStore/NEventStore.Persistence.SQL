namespace NEventStore.Persistence.Sql
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Threading;
    using System.Transactions;
    using NEventStore.Logging;
    using NEventStore.Serialization;

    public class SqlPersistenceEngine : IPersistStreams
    {
        private static readonly ILog Logger = LogFactory.BuildLogger(typeof(SqlPersistenceEngine));
        private static readonly DateTime EpochTime = new DateTime(1970, 1, 1);
        private readonly IConnectionFactory _connectionFactory;
        private readonly ISqlDialect _dialect;
        private readonly int _pageSize;
        private readonly TransactionScopeOption _scopeOption;
        private readonly ISerialize _serializer;
        private bool _disposed;
        private int _initialized;
        private readonly IStreamIdHasher _streamIdHasher;

        public SqlPersistenceEngine(
            IConnectionFactory connectionFactory,
            ISqlDialect dialect,
            ISerialize serializer,
            TransactionScopeOption scopeOption,
            int pageSize)
            : this(connectionFactory, dialect, serializer, scopeOption, pageSize, new Sha1StreamIdHasher())
        { }

        public SqlPersistenceEngine(
            IConnectionFactory connectionFactory,
            ISqlDialect dialect,
            ISerialize serializer,
            TransactionScopeOption scopeOption,
            int pageSize,
            IStreamIdHasher streamIdHasher)
        {
            if (connectionFactory == null)
            {
                throw new ArgumentNullException("connectionFactory");
            }

            if (dialect == null)
            {
                throw new ArgumentNullException("dialect");
            }

            if (serializer == null)
            {
                throw new ArgumentNullException("serializer");
            }

            if (pageSize < 0)
            {
                throw new ArgumentException("pageSize");
            }

            if (streamIdHasher == null)
            {
                throw new ArgumentNullException("streamIdHasher");
            }

            _connectionFactory = connectionFactory;
            _dialect = dialect;
            _serializer = serializer;
            _scopeOption = scopeOption;
            _pageSize = pageSize;
            _streamIdHasher = new StreamIdHasherValidator(streamIdHasher);

            if (Logger.IsDebugEnabled) Logger.Debug(Messages.UsingScope, _scopeOption.ToString());
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

            if (Logger.IsDebugEnabled) Logger.Debug(Messages.InitializingStorage);
            ExecuteCommand(statement => statement.ExecuteWithoutExceptions(_dialect.InitializeStorage));
        }

        public virtual IEnumerable<ICommit> GetFrom(string bucketId, string streamId, int minRevision, int maxRevision)
        {
            if (Logger.IsDebugEnabled) Logger.Debug(Messages.GettingAllCommitsBetween, streamId, minRevision, maxRevision);
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
                        .Select(x => x.GetCommit(_serializer, _dialect));
                });
        }

        public virtual IEnumerable<ICommit> GetFrom(string bucketId, DateTime start)
        {
            start = start.AddTicks(-(start.Ticks % TimeSpan.TicksPerSecond)); // Rounds down to the nearest second.
            start = start < EpochTime ? EpochTime : start;

            if (Logger.IsDebugEnabled) Logger.Debug(Messages.GettingAllCommitsFrom, start, bucketId);
            return ExecuteQuery(query =>
                {
                    string statement = _dialect.GetCommitsFromInstant;
                    query.AddParameter(_dialect.BucketId, bucketId, DbType.AnsiString);
                    query.AddParameter(_dialect.CommitStamp, start);
                    return query.ExecutePagedQuery(statement, (q, r) => { })
                            .Select(x => x.GetCommit(_serializer, _dialect));

                });
        }

        public virtual IEnumerable<ICommit> GetFromTo(string bucketId, DateTime start, DateTime end)
        {
            start = start.AddTicks(-(start.Ticks % TimeSpan.TicksPerSecond)); // Rounds down to the nearest second.
            start = start < EpochTime ? EpochTime : start;
            end = end < EpochTime ? EpochTime : end;

            if (Logger.IsDebugEnabled) Logger.Debug(Messages.GettingAllCommitsFromTo, start, end);
            return ExecuteQuery(query =>
                {
                    string statement = _dialect.GetCommitsFromToInstant;
                    query.AddParameter(_dialect.BucketId, bucketId, DbType.AnsiString);
                    query.AddParameter(_dialect.CommitStampStart, start);
                    query.AddParameter(_dialect.CommitStampEnd, end);
                    return query.ExecutePagedQuery(statement, (q, r) => { })
                        .Select(x => x.GetCommit(_serializer, _dialect));
                });
        }

        public virtual ICommit Commit(CommitAttempt attempt)
        {
            ICommit commit;
            try
            {
                commit = PersistCommit(attempt);
                if (Logger.IsDebugEnabled) Logger.Debug(Messages.CommitPersisted, attempt.CommitId);
            }
            catch (Exception e)
            {
                if (!(e is UniqueKeyViolationException))
                {
                    throw;
                }

                if (DetectDuplicate(attempt))
                {
                    if (Logger.IsInfoEnabled) Logger.Info(Messages.DuplicateCommit);
                    throw new DuplicateCommitException(e.Message, e);
                }

                if (Logger.IsInfoEnabled) Logger.Info(Messages.ConcurrentWriteDetected);
                throw new ConcurrencyException(e.Message, e);
            }
            return commit;
        }

        public virtual IEnumerable<IStreamHead> GetStreamsToSnapshot(string bucketId, int maxThreshold)
        {
            if (Logger.IsDebugEnabled) Logger.Debug(Messages.GettingStreamsToSnapshot);
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
            if (Logger.IsDebugEnabled) Logger.Debug(Messages.GettingRevision, streamId, maxRevision);
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
            if (Logger.IsDebugEnabled) Logger.Debug(Messages.AddingSnapshot, snapshot.StreamId, snapshot.StreamRevision);
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
            if (Logger.IsWarnEnabled) Logger.Warn(Messages.PurgingStorage);
            ExecuteCommand(cmd => cmd.ExecuteNonQuery(_dialect.PurgeStorage));
        }

        public void Purge(string bucketId)
        {
            if (Logger.IsWarnEnabled) Logger.Warn(Messages.PurgingBucket, bucketId);
            ExecuteCommand(cmd =>
                {
                    cmd.AddParameter(_dialect.BucketId, bucketId, DbType.AnsiString);
                    return cmd.ExecuteNonQuery(_dialect.PurgeBucket);
                });
        }

        public void Drop()
        {
            if (Logger.IsWarnEnabled) Logger.Warn(Messages.DroppingTables);
            ExecuteCommand(cmd => cmd.ExecuteNonQuery(_dialect.Drop));
        }

        public void DeleteStream(string bucketId, string streamId)
        {
            if (Logger.IsWarnEnabled) Logger.Warn(Messages.DeletingStream, streamId, bucketId);
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
            if (Logger.IsDebugEnabled) Logger.Debug(Messages.GettingAllCommitsFromBucketAndCheckpoint, bucketId, checkpointToken);
            return ExecuteQuery(query =>
            {
                string statement = _dialect.GetCommitsFromBucketAndCheckpoint;
                query.AddParameter(_dialect.BucketId, bucketId, DbType.AnsiString);
                query.AddParameter(_dialect.CheckpointNumber, checkpointToken);
                return query.ExecutePagedQuery(statement, (q, r) => { })
                    .Select(x => x.GetCommit(_serializer, _dialect));
            });
        }

        public IEnumerable<ICommit> GetFrom(Int64 checkpointToken)
        {
            if (Logger.IsDebugEnabled) Logger.Debug(Messages.GettingAllCommitsFromCheckpoint, checkpointToken);
            return ExecuteQuery(query =>
            {
                string statement = _dialect.GetCommitsFromCheckpoint;
                query.AddParameter(_dialect.CheckpointNumber, checkpointToken);
                return query.ExecutePagedQuery(statement, (q, r) => { })
                    .Select(x => x.GetCommit(_serializer, _dialect));
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

            if (Logger.IsDebugEnabled) Logger.Debug(Messages.ShuttingDownPersistence);
            _disposed = true;
        }

        protected virtual void OnPersistCommit(IDbStatement cmd, CommitAttempt attempt)
        { }

        private ICommit PersistCommit(CommitAttempt attempt)
        {
            if (Logger.IsDebugEnabled) Logger.Debug(Messages.AttemptingToCommit, attempt.Events.Count, attempt.StreamId, attempt.CommitSequence, attempt.BucketId);
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
                cmd.AddParameter(_dialect.CommitStamp, attempt.CommitStamp);
                cmd.AddParameter(_dialect.Headers, _serializer.Serialize(attempt.Headers));
                _dialect.AddPayloadParamater(_connectionFactory, connection, cmd, _serializer.Serialize(attempt.Events.ToList()));
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
                    return (value is long ? (long)value : (int)value) > 0;
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

                if (Logger.IsVerboseEnabled) Logger.Verbose(Messages.ExecutingQuery);
                return query(statement);
            }
            catch (Exception e)
            {
                if (statement != null)
                {
                    statement.Dispose();
                }
                if (transaction != null)
                {
                    transaction.Dispose();
                }
                if (connection != null)
                {
                    connection.Dispose();
                }
                if (scope != null)
                {
                    scope.Dispose();
                }

                if (Logger.IsDebugEnabled) Logger.Debug(Messages.StorageThrewException, e.GetType());
                if (e is StorageUnavailableException)
                {
                    throw;
                }

                throw new StorageException(e.Message, e);
            }
        }

        protected virtual TransactionScope OpenQueryScope()
        {
            return OpenCommandScope() ?? new TransactionScope(TransactionScopeOption.Suppress);
        }

        private void ThrowWhenDisposed()
        {
            if (!_disposed)
            {
                return;
            }

            if (Logger.IsWarnEnabled) Logger.Warn(Messages.AlreadyDisposed);
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
                    if (Logger.IsVerboseEnabled) Logger.Verbose(Messages.ExecutingCommand);
                    T rowsAffected = command(connection, statement);
                    if (Logger.IsVerboseEnabled) Logger.Verbose(Messages.CommandExecuted, rowsAffected);

                    if (transaction != null)
                    {
                        transaction.Commit();
                    }

                    if (scope != null)
                    {
                        scope.Complete();
                    }

                    return rowsAffected;
                }
                catch (Exception e)
                {
                    if (Logger.IsDebugEnabled) Logger.Debug(Messages.StorageThrewException, e.GetType());
                    if (!RecoverableException(e))
                    {
                        throw new StorageException(e.Message, e);
                    }

                    if (Logger.IsInfoEnabled) Logger.Info(Messages.RecoverableExceptionCompletesScope);
                    if (scope != null)
                    {
                        scope.Complete();
                    }

                    throw;
                }
            }
        }

        protected virtual TransactionScope OpenCommandScope()
        {
            if (Transaction.Current == null)
            {
                return new TransactionScope(_scopeOption, new TransactionOptions
                {
                    IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted
                });
            }
            // todo: maybe add a warning for the isolation level
            /*
            if (Transaction.Current.IsolationLevel == System.Transactions.IsolationLevel.Serializable)
            {
                if (Logger.IsWarnEnabled) Logger.Warn("Serializable can be troublesome");
            }
            */
            return new TransactionScope(_scopeOption);
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
                if (streamIdHasher == null)
                {
                    throw new ArgumentNullException("streamIdHasher");
                }
                _streamIdHasher = streamIdHasher;
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