using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Transactions;
using FluentAssertions;
#if NET462
using NEventStore.Diagnostics;
using System.Data.SqlClient;
#endif
using NEventStore.Persistence.AcceptanceTests.BDD;
using NEventStore.Persistence.Sql;
using NEventStore.Persistence.Sql.SqlDialects;
using NEventStore.Persistence.Sql.Tests;
using NEventStore.Serialization;
using IsolationLevel = System.Data.IsolationLevel;
#if MSTEST
    using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif
#if NUNIT
using NUnit.Framework;
#endif
#if XUNIT
    using Xunit;
    using Xunit.Should;
#endif

namespace NEventStore.Persistence.AcceptanceTests
{
#if MSTEST
    [TestClass]
#endif
    public class When_reusing_a_connection_from_the_connection_pool_without_a_transaction_scope :
        IsolationLevelConcern
    {
        protected override void Because()
        {
            using (var conn = ConnectionFactory.Open())
            using (conn.BeginTransaction(IsolationLevel.RepeatableRead))
            {
            }

            Recorder.IsRecording = true;
            // Enumerate fully to make sure the underlying DB stuff (command/reader etc.) is disposed
            Persistence.GetFrom(0).ToArray();
            Recorder.IsRecording = false;
        }

        [Fact]
        public void Should_run_command_in_non_default_isolation_level()
        {
            Recorder.StatementsWithIsolationLevels.Select(i => i.IsolationLevel)
                .Should().BeEquivalentTo(new[] {IsolationLevel.ReadCommitted});
        }
    }

    public abstract class IsolationLevelConcern : SpecificationBase, IDisposable
    {
        private readonly IsolationLevelPersistenceEngineFixture _fixture;

        protected IPersistStreams Persistence
        {
            get { return _fixture.Persistence; }
        }

        protected IsolationLevelRecorder Recorder
        {
            get { return _fixture.Recorder; }
        }

        protected IConnectionFactory ConnectionFactory
        {
            get { return _fixture.ConnectionFactory; }
        }

        protected override void Cleanup()
        {
            _fixture?.Dispose();
        }

        public void Dispose()
        {
            _fixture?.Dispose();
        }

        /// <summary>
        /// <para>
        /// This code was meant to be run right before every test in the fixture to give time
        /// to do further initialization before the PersistenceEngineFixture was created.
        /// Unfortunately the 3 frameworks
        /// have very different ways of doing this:
        /// - NUnit: TestFixtureSetUp
        /// - MSTest: ClassInitialize (not inherited, will be ignored if defined on a base class)
        /// - xUnit: IUseFixture + SetFixture
        /// We need a way to also have some configuration before the PersistenceEngineFixture is created.
        /// </para>
        /// <para>
        /// We'de decided to use the test constructor to do the job, it's your responsibility to guarantee
        /// One time initialization (for anything that need it, if you have multiple tests on a fixture)
        /// depending on the framework you are using.
        /// </para>
        /// <para>We can solve the also adding an optional 'config' delegate to be executed as the first line in this base constructor.</para>
        /// <para>
        /// quick workaround:
        /// - the 'Reinitialize()' method can be called to rerun the initialization after we changed the configuration
        /// in the constructor
        /// </para>
        /// </summary>
        protected IsolationLevelConcern()
        {
            _fixture = new IsolationLevelPersistenceEngineFixture();
            _fixture.Initialize();
        }
    }

    public class IsolationLevelPersistenceEngineFixture
    {
        private readonly IsolationLevelRecorder _recorder;
        private readonly IConnectionFactory _connectionFactory;
        private readonly Func<IPersistStreams> _createPersistence;
        private IPersistStreams _persistence;

        public IsolationLevelPersistenceEngineFixture()
        {
            _recorder = new IsolationLevelRecorder();
#if NET462
            _connectionFactory = new EnviromentConnectionFactory("MsSql", "System.Data.SqlClient");
#else
            _connectionFactory = new EnviromentConnectionFactory("MsSql", Microsoft.Data.SqlClient.SqlClientFactory.Instance);
#endif
            _createPersistence = () =>
                new SqlPersistenceFactory(_connectionFactory,
                    new BinarySerializer(),
                    new IsolationLevelRecordingSqlDialect(_recorder)).Build();
        }

        public void Initialize()
        {
            if (_persistence?.IsDisposed == false)
            {
                _persistence.Drop();
                _persistence.Dispose();
            }
#if NET462
            _persistence = new PerformanceCounterPersistenceEngine(_createPersistence(), "tests");
#else
            _persistence = _createPersistence();
#endif
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
            if (_persistence?.IsDisposed == false)
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

        public IsolationLevelRecordingSqlDialect(IsolationLevelRecorder recorder)
        {
            _recorder = recorder;
        }

        public override IDbStatement BuildStatement(
            TransactionScope scope,
            IDbConnection connection,
            IDbTransaction transaction)
        {
            return new TransactionLevelRecordingStatement(base.BuildStatement(scope, connection, transaction), _recorder);
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
                                (int) IsolationLevel.Unspecified,
                                (int) IsolationLevel.ReadUncommitted,
                                (int) IsolationLevel.ReadCommitted,
                                (int) IsolationLevel.RepeatableRead,
                                (int) IsolationLevel.Serializable,
                                (int) IsolationLevel.Snapshot));
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