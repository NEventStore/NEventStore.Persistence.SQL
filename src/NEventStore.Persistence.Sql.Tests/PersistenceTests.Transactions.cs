#pragma warning disable 169 // ReSharper disable InconsistentNaming
#pragma warning disable IDE1006 // Naming Styles
#pragma warning disable S101 // Types should be named in PascalCase

namespace NEventStore.Persistence.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NEventStore.Persistence.AcceptanceTests.BDD;
    using FluentAssertions;
#if MSTEST
    using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif
#if NUNIT
    using NUnit.Framework;
    using System.Threading.Tasks;
    using System.Transactions;
    using System.Threading;
    using System.Globalization;
#endif
#if XUNIT
    using Xunit;
    using Xunit.Should;
#endif

    public abstract class TransactionConcern : PersistenceEngineConcern
    {
        protected void Reinitialize(bool includeInAmbientTransation)
        {
            if (includeInAmbientTransation)
            {
                Fixture.ScopeOption = TransactionScopeOption.Required;
            }
            else
            {
                Fixture.ScopeOption = TransactionScopeOption.Suppress;
            }
            Fixture.Initialize(ConfiguredPageSizeForTesting);
        }
    }

#if MSTEST
    [TestClass]
#endif
#if NUNIT
    [TestFixture(false, IsolationLevel.Serializable)]
    [TestFixture(false, IsolationLevel.ReadCommitted)]
    [TestFixture(true, IsolationLevel.Serializable)] // this will always fail! Serializable prevents multiple transation to insert simultaneously
    [TestFixture(true, IsolationLevel.ReadCommitted)]
#endif
    public class When_enlist_in_ambient_transaction_and_multiple_parallel_outer_transaction_with_isolationlevel_that_complete : TransactionConcern
    {
        private ICommit[] _commits;
        private const int Loop = 2;
        private const int StreamsPerTransaction = 20;
        private readonly IsolationLevel _transationIsolationLevel;
        private readonly bool _enlistInAmbientTransaction;
        private Exception _thrown;

        public When_enlist_in_ambient_transaction_and_multiple_parallel_outer_transaction_with_isolationlevel_that_complete(
            bool enlistInAmbientTransaction,
            IsolationLevel transationIsolationLevel
            )
        {
            Reinitialize(enlistInAmbientTransaction);
            _transationIsolationLevel = transationIsolationLevel;
            _enlistInAmbientTransaction = enlistInAmbientTransaction;
        }

        protected override void Because()
        {
            _thrown = Catch.Exception(() =>
            Parallel.For(0, Loop, i =>
            {
                var eventStore = new OptimisticEventStore(Persistence, null);

                using (var scope = new TransactionScope(TransactionScopeOption.Required,
                    new TransactionOptions { IsolationLevel = _transationIsolationLevel }
#if NET451 || NETSTANDARD2_0
                , TransactionScopeAsyncFlowOption.Enabled
#endif
                    ))
                {
                    int j;
                    for (j = 0; j < StreamsPerTransaction; j++)
                    {
                        using (var stream = eventStore.OpenStream(i.ToString() + "-" + j.ToString()))
                        {
                            for (int k = 0; k < 10; k++)
                            {
                                stream.Add(new EventMessage { Body = "body" + k });
                            }
                            stream.CommitChanges(Guid.NewGuid());
                        }
                    }
                    scope.Complete();
                }
            })
            );
        }

        [Fact]
        public void should_throw_an_Exception_if_enlist_in_ambient_transaction_and_IsolationLevel_is_Serializable()
        {
            if (_enlistInAmbientTransaction && _transationIsolationLevel == IsolationLevel.Serializable)
            {
                _thrown.Should().BeOfType<AggregateException>();
            }
            else
            {
                _thrown.Should().BeNull();
            }
        }

        [Fact]
        public void Should_have_expected_number_of_commits()
        {
            if (_enlistInAmbientTransaction && _transationIsolationLevel == IsolationLevel.Serializable)
            {
                // nothing should be tested we never reach this point due to exception
            }
            else
            {
                _commits = Persistence.GetFrom().ToArray();
                _commits.Length.Should().Be(Loop * StreamsPerTransaction);
            }
        }
    }

#if MSTEST
    [TestClass]
#endif
#if NUNIT
    [TestFixture(false, IsolationLevel.Serializable)]
    [TestFixture(false, IsolationLevel.ReadCommitted)]
    [TestFixture(true, IsolationLevel.Serializable)] // this will always fail! Serializable prevents multiple transation to insert simultaneously
    [TestFixture(true, IsolationLevel.ReadCommitted)]
#endif
    public class When_enlist_in_ambient_transaction_and_multiple_parallel_outer_transaction_with_isolationlevel_that_DO_NOT_complete : TransactionConcern
    {
        private ICommit[] _commits;
        private const int Loop = 2;
        private const int StreamsPerTransaction = 20;
        private readonly IsolationLevel _transationIsolationLevel;
        private readonly bool _enlistInAmbientTransaction;
        private Exception _thrown;

        public When_enlist_in_ambient_transaction_and_multiple_parallel_outer_transaction_with_isolationlevel_that_DO_NOT_complete(
            bool enlistInAmbientTransaction,
            IsolationLevel transationIsolationLevel
            )
        {
            Reinitialize(enlistInAmbientTransaction);
            _transationIsolationLevel = transationIsolationLevel;
            _enlistInAmbientTransaction = enlistInAmbientTransaction;
        }

        protected override void Because()
        {
            _thrown = Catch.Exception(() =>
            Parallel.For(0, Loop, i =>
            {
                var eventStore = new OptimisticEventStore(Persistence, null);

                using (var scope = new TransactionScope(TransactionScopeOption.Required,
                    new TransactionOptions { IsolationLevel = _transationIsolationLevel }
#if NET451 || NETSTANDARD2_0
                , TransactionScopeAsyncFlowOption.Enabled
#endif
                    ))
                {
                    int j;
                    for (j = 0; j < StreamsPerTransaction; j++)
                    {
                        using (var stream = eventStore.OpenStream(i.ToString() + "-" + j.ToString()))
                        {
                            for (int k = 0; k < 10; k++)
                            {
                                stream.Add(new EventMessage { Body = "body" + k });
                            }
                            stream.CommitChanges(Guid.NewGuid());
                        }
                    }
                    // DO NOT COMPLETE THE TRANSACTIONS
                    // scope.Complete();
                }
            })
            );
        }

        [Fact]
        public void should_throw_an_Exception_if_enlist_in_ambient_transaction_and_IsolationLevel_is_Serializable()
        {
            if (_enlistInAmbientTransaction && _transationIsolationLevel == IsolationLevel.Serializable)
            {
                _thrown.Should().BeOfType<AggregateException>();
            }
            else
            {
                _thrown.Should().BeNull();
            }
        }

        [Fact]
        public void Should_have_expected_number_of_commits()
        {
            _commits = Persistence.GetFrom().ToArray();
            if (_enlistInAmbientTransaction && _transationIsolationLevel == IsolationLevel.Serializable)
            {
                // nothing should be tested we never reach this point due to exception
            }
            else if (_enlistInAmbientTransaction && _transationIsolationLevel == IsolationLevel.ReadCommitted)
            {
                _commits.Length.Should().Be(0);
            }
            else
            {
                _commits.Length.Should().Be(Loop * StreamsPerTransaction);
            }
        }
    }

#if MSTEST
    [TestClass]
#endif
#if NUNIT
    [TestFixture(false, IsolationLevel.Serializable)]
    [TestFixture(false, IsolationLevel.ReadCommitted)]
    [TestFixture(true, IsolationLevel.Serializable)] // this will always fail! Serializable prevents multiple transation to insert simultaneously
    [TestFixture(true, IsolationLevel.ReadCommitted)]
#endif
    public class When_enlist_in_ambient_transaction_and_single_outer_transaction_with_isolationlevel_that_complete : TransactionConcern
    {
        private ICommit[] _commits;
        private const int Loop = 2;
        private const int StreamsPerTransaction = 20;
        private readonly IsolationLevel _transationIsolationLevel;
        private readonly bool _enlistInAmbientTransaction;
        private Exception _thrown;

        public When_enlist_in_ambient_transaction_and_single_outer_transaction_with_isolationlevel_that_complete(
            bool enlistInAmbientTransaction,
            IsolationLevel transationIsolationLevel
            )
        {
            Reinitialize(enlistInAmbientTransaction);
            _transationIsolationLevel = transationIsolationLevel;
            _enlistInAmbientTransaction = enlistInAmbientTransaction;
        }

        protected override void Because()
        {
            _thrown = Catch.Exception(() =>
            {
                using (var scope = new TransactionScope(TransactionScopeOption.Required,
                        new TransactionOptions { IsolationLevel = _transationIsolationLevel }
#if NET451 || NETSTANDARD2_0
                , TransactionScopeAsyncFlowOption.Enabled
#endif
                    ))
                {
                    var res = Parallel.For(0, Loop, i =>
                    {
                        var eventStore = new OptimisticEventStore(Persistence, null);

                        int j;
                        for (j = 0; j < StreamsPerTransaction; j++)
                        {
                            using (var stream = eventStore.OpenStream(i.ToString() + "-" + j.ToString()))
                            {
                                for (int k = 0; k < 10; k++)
                                {
                                    stream.Add(new EventMessage { Body = "body" + k });
                                }
                                stream.CommitChanges(Guid.NewGuid());
                            }
                        }
                    });
                    Assert.IsTrue(res.IsCompleted);
                    scope.Complete();
                }
            });
        }

        [Fact]
        public void should_throw_an_Exception_if_enlist_in_ambient_transaction_and_IsolationLevel_is_Serializable()
        {
            //if (_enlistInAmbientTransaction && _transationIsolationLevel == IsolationLevel.Serializable)
            //{
            //    _thrown.Should().BeOfType<AggregateException>();
            //}
            //else
            //{
            _thrown.Should().BeNull();
            //}
        }

        [Fact]
        public void Should_have_expected_number_of_commits()
        {
            //if (_enlistInAmbientTransaction && _transationIsolationLevel == IsolationLevel.Serializable)
            //{
            //    // nothing should be tested we never reach this point due to exception
            //}
            //else
            //{
            _commits = Persistence.GetFrom().ToArray();
            _commits.Length.Should().Be(Loop * StreamsPerTransaction);
            //}
        }
    }

    /* Transactions support must be investigated, it should be valid only for Databases that supports it (InMemoryPersistence will not). */
#if MSTEST
    [TestClass]
#endif
    public class TransactionConcern2 : PersistenceEngineConcern
    {
        private ICommit[] _commits;
        private const int Loop = 2;
        private const int StreamsPerTransaction = 20;

        protected override void Because()
        {
            Parallel.For(0, Loop, i =>
            {
                var eventStore = new OptimisticEventStore(Persistence, null);
                using (var scope = new TransactionScope(TransactionScopeOption.Required,
                    new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted }
#if NET451 || NETSTANDARD2_0
                , TransactionScopeAsyncFlowOption.Enabled
#endif
                    ))
                {
                    int j;
                    for (j = 0; j < StreamsPerTransaction; j++)
                    {
                        using (var stream = eventStore.OpenStream(i.ToString() + "-" + j.ToString()))
                        {
                            for (int k = 0; k < 10; k++)
                            {
                                stream.Add(new EventMessage { Body = "body" + k });
                            }
                            stream.CommitChanges(Guid.NewGuid());
                        }
                    }
                    scope.Complete();
                }
            });
            _commits = Persistence.GetFrom().ToArray();
        }

        [Fact]
        public void Should_have_expected_number_of_commits()
        {
            _commits.Length.Should().Be(Loop * StreamsPerTransaction);
        }

        [Fact]
        public void ScopeCompleteAndSerializable()
        {
            Reinitialize();
            const int loop = 10;
            using (var scope = new TransactionScope(
                TransactionScopeOption.Required,
                new TransactionOptions
                {
                    IsolationLevel = IsolationLevel.Serializable
                }
#if NET451 || NETSTANDARD2_0
                , TransactionScopeAsyncFlowOption.Enabled
#endif
                ))
            {
                Parallel.For(0, loop, i =>
                {
                    Console.WriteLine("Creating stream {0} on thread {1}", i, Thread.CurrentThread.ManagedThreadId);
                    var eventStore = new OptimisticEventStore(Persistence, null);
                    string streamId = i.ToString(CultureInfo.InvariantCulture);
                    using (var stream = eventStore.OpenStream(streamId))
                    {
                        stream.Add(new EventMessage { Body = "body1" });
                        stream.Add(new EventMessage { Body = "body2" });
                        stream.CommitChanges(Guid.NewGuid());
                    }
                });
                scope.Complete();
            }
            ICommit[] commits = Persistence.GetFrom(0).ToArray();
            commits.Length.Should().Be(loop);
        }

        [Fact]
        public void ScopeNotCompleteAndReadCommitted()
        {
            Reinitialize();
            const int loop = 10;
            using (new TransactionScope(
                TransactionScopeOption.Required,
                new TransactionOptions
                {
                    IsolationLevel = IsolationLevel.ReadCommitted
                }
#if NET451 || NETSTANDARD2_0
                , TransactionScopeAsyncFlowOption.Enabled
#endif
                ))
            {
                Parallel.For(0, loop, i =>
                {
                    Console.WriteLine("Creating stream {0} on thread {1}", i, Thread.CurrentThread.ManagedThreadId);
                    var eventStore = new OptimisticEventStore(Persistence, null);
                    string streamId = i.ToString(CultureInfo.InvariantCulture);
                    using (var stream = eventStore.OpenStream(streamId))
                    {
                        stream.Add(new EventMessage { Body = "body1" });
                        stream.Add(new EventMessage { Body = "body2" });
                        stream.CommitChanges(Guid.NewGuid());
                    }
                });
            }
            ICommit[] commits = Persistence.GetFrom(0).ToArray();
            commits.Length.Should().Be(0);
        }

        [Fact]
        public void ScopeNotCompleteAndSerializable()
        {
            Reinitialize();
            const int loop = 10;
            using (new TransactionScope(
                TransactionScopeOption.Required,
                new TransactionOptions
                {
                    IsolationLevel = IsolationLevel.Serializable
                }
#if NET451 || NETSTANDARD2_0
                , TransactionScopeAsyncFlowOption.Enabled
#endif
                ))
            {
                Parallel.For(0, loop, i =>
                {
                    Console.WriteLine("Creating stream {0} on thread {1}", i, Thread.CurrentThread.ManagedThreadId);
                    var eventStore = new OptimisticEventStore(Persistence, null);
                    string streamId = i.ToString(CultureInfo.InvariantCulture);
                    using (var stream = eventStore.OpenStream(streamId))
                    {
                        stream.Add(new EventMessage { Body = "body1" });
                        stream.Add(new EventMessage { Body = "body2" });
                        stream.CommitChanges(Guid.NewGuid());
                    }
                });
            }
            ICommit[] commits = Persistence.GetFrom(0).ToArray();
            commits.Length.Should().Be(0);
        }
    }
}

#pragma warning restore S101 // Types should be named in PascalCase
#pragma warning restore 169 // ReSharper disable InconsistentNaming
#pragma warning restore IDE1006 // Naming Styles
