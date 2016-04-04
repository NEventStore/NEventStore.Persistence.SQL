using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;
using FluentAssertions;
using NEventStore.Diagnostics;
using NEventStore.Persistence.AcceptanceTests.BDD;
using NEventStore.Persistence.Sql;
using NEventStore.Persistence.Sql.SqlDialects;
using NEventStore.Persistence.Sql.Tests;
using NEventStore.Serialization;
using Xunit;
using IsolationLevel = System.Data.IsolationLevel;

namespace NEventStore.Persistence.AcceptanceTests
{
    // This test proves that the bug still exists.
    public class when_reusing_a_connection_from_the_connection_pool_without_a_transaction_scope_and_not_circumventing_the_bug :
        IsolationLevelConcern
    {
        protected override bool FixIsolationLevelBug
        {
            get { return false; }
        }

        protected override void Because()
        {
            using (var conn = ConnectionFactory.Open())
            {
                conn.BeginTransaction(IsolationLevel.RepeatableRead);
            }

            Recorder.IsRecording = true;
            Persistence.GetFrom();
            Recorder.IsRecording = false;
        }

        [Fact]
        public void should_run_command_in_non_default_isolation_level()
        {
            Recorder.StatementsWithIsolationLevels.Select(i => i.IsolationLevel)
                .ShouldAllBeEquivalentTo(new[] {IsolationLevel.RepeatableRead});
        }
    }

    public class when_reusing_a_connection_from_the_connection_pool_without_a_transaction_scope :
        IsolationLevelConcern
    {
        protected override bool FixIsolationLevelBug
        {
            get { return true; }
        }

        protected override void Because()
        {
            using (var conn = ConnectionFactory.Open())
            {
                conn.BeginTransaction(IsolationLevel.RepeatableRead);
            }

            Recorder.IsRecording = true;
            Persistence.GetFrom();
            Recorder.IsRecording = false;
        }

        [Fact]
        public void should_run_command_in_non_default_isolation_level()
        {
            Recorder.StatementsWithIsolationLevels.Select(i => i.IsolationLevel)
                .ShouldAllBeEquivalentTo(new[] {IsolationLevel.ReadCommitted});
        }
    }

    public abstract class IsolationLevelConcern : SpecificationBase, IUseFixture<IsolationLevelPersistenceEngineFixture>
    {
        private IsolationLevelPersistenceEngineFixture _fixture;
        private IPersistStreams _persistence;

        protected IPersistStreams Persistence
        {
            get
            {
                if(_persistence == null)
                {
                    
                }
                return _fixture.Persistence;
            }
        }

        protected IsolationLevelRecorder Recorder
        {
            get { return _fixture.Recorder; }
        }

        protected IConnectionFactory ConnectionFactory
        {
            get { return _fixture.ConnectionFactory; }
        }

        protected abstract bool FixIsolationLevelBug { get; }

        public void SetFixture(IsolationLevelPersistenceEngineFixture data)
        {
            _fixture = data;
            _fixture.Initialize(FixIsolationLevelBug);
        }
    }

    public class IsolationLevelPersistenceEngineFixture
    {
        private readonly IsolationLevelRecorder _recorder;
        private readonly IConnectionFactory _connectionFactory;
        private readonly Func<bool, IPersistStreams> _createPersistence;
        private IPersistStreams _persistence;

        public IsolationLevelPersistenceEngineFixture()
        {
            _recorder = new IsolationLevelRecorder();
            _connectionFactory = new EnviromentConnectionFactory("MsSql", "System.Data.SqlClient");
            _createPersistence = fixIsolationLevelBug =>
                new SqlPersistenceFactory(_connectionFactory,
                    new BinarySerializer(),
                    new IsolationLevelRecordingSqlDialect(_recorder, fixIsolationLevelBug)).Build();
        }

        public void Initialize(bool fixIsolationLevelBug)
        {
            if (_persistence != null && !_persistence.IsDisposed)
            {
                _persistence.Drop();
                _persistence.Dispose();
            }
            _persistence = new PerformanceCounterPersistenceEngine(_createPersistence(fixIsolationLevelBug), "tests");
            _persistence.Initialize();
        }

        public IPersistStreams Persistence
        {
            get { return _persistence; }
        }

        public IsolationLevelRecorder Recorder
        {
            get { return _recorder; }
        }

        public IConnectionFactory ConnectionFactory
        {
            get { return _connectionFactory; }
        }

        public void Dispose()
        {
            if (_persistence != null && !_persistence.IsDisposed)
            {
                _persistence.Drop();
                _persistence.Dispose();
            }
        }
    }

    public class StatementAndIsolationLevel
    {
        public string Statement { get; private set; }
        public IsolationLevel IsolationLevel { get; private set; }

        public StatementAndIsolationLevel(string statement, IsolationLevel isolationLevel)
        {
            Statement = statement;
            IsolationLevel = isolationLevel;
        }
    }

    public class IsolationLevelRecorder
    {
        public bool IsRecording { get; set; }

        public List<StatementAndIsolationLevel> StatementsWithIsolationLevels { get; private set; }

        public IsolationLevelRecorder()
        {
            StatementsWithIsolationLevels = new List<StatementAndIsolationLevel>();
        }

        public void RecordIsolationLevel(string statement, IsolationLevel isolationLevel)
        {
            if (IsRecording)
                StatementsWithIsolationLevels.Add(new StatementAndIsolationLevel(statement, isolationLevel));
        }
    }

    internal class IsolationLevelRecordingSqlDialect : MsSqlDialect
    {
        private readonly IsolationLevelRecorder _recorder;
        private readonly bool _fixIsolationLevelBug;

        public IsolationLevelRecordingSqlDialect(IsolationLevelRecorder recorder, bool fixIsolationLevelBug)
        {
            _recorder = recorder;
            _fixIsolationLevelBug = fixIsolationLevelBug;
        }

        public override IDbStatement BuildStatement(
            TransactionScope scope,
            IDbConnection connection,
            IDbTransaction transaction)
        {
            return new TransactionLevelRecordingStatement(base.BuildStatement(scope, connection, transaction), _recorder);
        }

        public override IDbTransaction OpenTransaction(IDbConnection connection)
        {
            if (_fixIsolationLevelBug)
                return base.OpenTransaction(connection);

            return null;
        }

        private class TransactionLevelRecordingStatement : IDbStatement
        {
            private readonly IDbStatement _innerStatement;
            private readonly IsolationLevelRecorder _recorder;

            public List<StatementAndIsolationLevel> StatementsWithIsolationLevels { get; private set; }

            public TransactionLevelRecordingStatement(IDbStatement innerStatement, IsolationLevelRecorder recorder)
            {
                StatementsWithIsolationLevels = new List<StatementAndIsolationLevel>();
                _innerStatement = innerStatement;
                _recorder = recorder;
            }

            public void Dispose()
            {
                _innerStatement.Dispose();
            }

            private IsolationLevel GetCurrentIsolationLevel()
            {
                return
                    (IsolationLevel)
                        _innerStatement.ExecuteScalar(
                            string.Format(@"
SELECT CASE transaction_isolation_level 
  WHEN 0 THEN {0}
  WHEN 1 THEN {1}
  WHEN 2 THEN {2}
  WHEN 3 THEN {3}
  WHEN 4 THEN {4}
  WHEN 5 THEN {5}
END AS TRANSACTION_ISOLATION_LEVEL 
FROM sys.dm_exec_sessions 
where session_id = @@SPID",
                                (int) System.Data.IsolationLevel.Unspecified,
                                (int) System.Data.IsolationLevel.ReadUncommitted,
                                (int) System.Data.IsolationLevel.ReadCommitted,
                                (int) System.Data.IsolationLevel.RepeatableRead,
                                (int) System.Data.IsolationLevel.Serializable,
                                (int) System.Data.IsolationLevel.Snapshot));
            }

            public void AddParameter(string name, object value, DbType? parameterType = null)
            {
                _innerStatement.AddParameter(name, value, parameterType);
            }

            public int ExecuteNonQuery(string commandText)
            {
                _recorder.RecordIsolationLevel(commandText, GetCurrentIsolationLevel());
                return _innerStatement.ExecuteNonQuery(commandText);
            }

            public int ExecuteWithoutExceptions(string commandText)
            {
                _recorder.RecordIsolationLevel(commandText, GetCurrentIsolationLevel());
                return _innerStatement.ExecuteWithoutExceptions(commandText);
            }

            public object ExecuteScalar(string commandText)
            {
                _recorder.RecordIsolationLevel(commandText, GetCurrentIsolationLevel());
                return _innerStatement.ExecuteScalar(commandText);
            }

            public IEnumerable<IDataRecord> ExecuteWithQuery(string queryText)
            {
                _recorder.RecordIsolationLevel(queryText, GetCurrentIsolationLevel());
                return _innerStatement.ExecuteWithQuery(queryText);
            }

            public IEnumerable<IDataRecord> ExecutePagedQuery(string queryText, NextPageDelegate nextpage)
            {
                _recorder.RecordIsolationLevel(queryText, GetCurrentIsolationLevel());
                return _innerStatement.ExecutePagedQuery(queryText, nextpage);
            }

            public int PageSize
            {
                get { return _innerStatement.PageSize; }
                set { _innerStatement.PageSize = value; }
            }
        }
    }
}