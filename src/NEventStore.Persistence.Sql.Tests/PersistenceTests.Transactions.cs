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

    public abstract class MultipleConnectionsWithMultipleTransactionScopes : TransactionConcern
    {
        protected ICommit[] _commits;
        protected const int Loop = 2;
        protected const int StreamsPerTransaction = 20;
        protected readonly IsolationLevel _transationIsolationLevel;
        protected readonly bool _enlistInAmbientTransaction;
        protected Exception _thrown;
        protected readonly bool _completeTRansaction;

        public MultipleConnectionsWithMultipleTransactionScopes(
            bool enlistInAmbientTransaction,
            IsolationLevel transationIsolationLevel,
            bool completeTRansaction
            )
        {
            _transationIsolationLevel = transationIsolationLevel;
            _enlistInAmbientTransaction = enlistInAmbientTransaction;
            _completeTRansaction = completeTRansaction;
            Reinitialize(enlistInAmbientTransaction);
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
                    if (_completeTRansaction)
                    {
                        scope.Complete();
                    }
                }
            })
            );
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
    public class Multiple_Completing_TransactionScopes_When_EnlistInAmbientTransaction_is_and_IsolationLevel_is :
        MultipleConnectionsWithMultipleTransactionScopes
    {
        public Multiple_Completing_TransactionScopes_When_EnlistInAmbientTransaction_is_and_IsolationLevel_is(
            bool enlistInAmbientTransaction,
            IsolationLevel transationIsolationLevel
            ) : base(enlistInAmbientTransaction, transationIsolationLevel, true)
        { }

        [Fact]
        public void should_throw_an_Exception_only_if_enlist_in_ambient_transaction_and_IsolationLevel_is_Serializable()
        {
            if (_enlistInAmbientTransaction && _transationIsolationLevel == IsolationLevel.Serializable)
            {
                _thrown.Should().BeOfType<AggregateException>();
                _thrown.InnerException.Should().BeOfType<StorageException>();
                // two serializable transactions on the same connection can result in deadlocks.
                _thrown.InnerException.Message.Should().Contain("deadlock");
            }
            else
            {
                _thrown.Should().BeNull();
            }
        }

        [Fact]
        public void Should_have_no_commits_at_all()
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
    public class Multiple_Failing_TransactionScopes_When_EnlistInAmbientTransaction_is_and_IsolationLevel_is :
        MultipleConnectionsWithMultipleTransactionScopes
    {
        public Multiple_Failing_TransactionScopes_When_EnlistInAmbientTransaction_is_and_IsolationLevel_is(
            bool enlistInAmbientTransaction,
            IsolationLevel transationIsolationLevel
            ) : base(enlistInAmbientTransaction, transationIsolationLevel, completeTRansaction: false)
        { }

        [Fact]
        public void should_throw_an_Exception_only_if_enlist_in_ambient_transaction_and_IsolationLevel_is_Serializable()
        {
            if (_enlistInAmbientTransaction && _transationIsolationLevel == IsolationLevel.Serializable)
            {
                _thrown.Should().BeOfType<AggregateException>();
                _thrown.InnerException.Should().BeOfType<StorageException>();
                // two serializable transactions on the same connection can result in deadlocks.
                _thrown.InnerException.Message.Should().Contain("deadlock");
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
                if (!_enlistInAmbientTransaction)
                {
                    _commits.Length.Should().Be(Loop * StreamsPerTransaction);
                }
                else
                {
                    _commits.Length.Should().Be(0);
                }
            }
        }
    }

    public abstract class MultipleConnectionsWithSingleTransactionScope : TransactionConcern
    {
        protected ICommit[] _commits;
        protected const int Loop = 2;
        protected const int StreamsPerTransaction = 20;
        protected readonly IsolationLevel _transationIsolationLevel;
        protected readonly bool _enlistInAmbientTransaction;
        protected Exception _thrown;
        protected readonly bool _completeTransaction;

        public MultipleConnectionsWithSingleTransactionScope(
            bool enlistInAmbientTransaction,
            IsolationLevel transationIsolationLevel,
            bool completeTransaction
            )
        {
            _transationIsolationLevel = transationIsolationLevel;
            _enlistInAmbientTransaction = enlistInAmbientTransaction;
            _completeTransaction = completeTransaction;
            Reinitialize(enlistInAmbientTransaction);
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
                    if (_completeTransaction)
                    {
                        scope.Complete();
                    }
                }
            });
        }
    }

#if MSTEST
    [TestClass]
#endif
#if NUNIT
    [TestFixture(false, IsolationLevel.Serializable)]
    [TestFixture(false, IsolationLevel.ReadCommitted)]
#endif
    public class Single_Completing_TransactionScope_When_EnlistInAmbientTransaction_is_and_IsolationLevel_is
        : MultipleConnectionsWithSingleTransactionScope
    {
        public Single_Completing_TransactionScope_When_EnlistInAmbientTransaction_is_and_IsolationLevel_is(
            bool enlistInAmbientTransaction,
            IsolationLevel transationIsolationLevel
            ) : base(enlistInAmbientTransaction, transationIsolationLevel, completeTransaction: true)
        { }

        [Fact]
        public void should_not_throw_an_Exception()
        {
            _thrown.Should().BeNull();
        }

        [Fact]
        public void Should_have_expected_number_of_commits()
        {
            _commits = Persistence.GetFrom().ToArray();
            _commits.Length.Should().Be(Loop * StreamsPerTransaction);
        }
    }

#if MSTEST
    [TestClass]
#endif
#if NUNIT
    [TestFixture(true, IsolationLevel.Serializable)] // unsupported: This platform does not support distributed transactions
    [TestFixture(true, IsolationLevel.ReadCommitted)] // unsupported: This platform does not support distributed transactions
#endif
    public class Unsupported_Single_Completing_TransactionScope_When_EnlistInAmbientTransaction_is_and_IsolationLevel_is
        : MultipleConnectionsWithSingleTransactionScope
    {
        public Unsupported_Single_Completing_TransactionScope_When_EnlistInAmbientTransaction_is_and_IsolationLevel_is(
            bool enlistInAmbientTransaction,
            IsolationLevel transationIsolationLevel
            ) : base(enlistInAmbientTransaction, transationIsolationLevel, completeTransaction: true)
        { }

        [Fact]
        public void should_throw_an_StorageUnavailableException()
        {
            _thrown.Should().BeOfType<AggregateException>();
            _thrown.InnerException.Should().BeOfType<StorageUnavailableException>();
            _thrown.InnerException.Message.Should().Contain("This platform does not support distributed transactions");
        }
    }

}

#pragma warning restore S101 // Types should be named in PascalCase
#pragma warning restore 169 // ReSharper disable InconsistentNaming
#pragma warning restore IDE1006 // Naming Styles
