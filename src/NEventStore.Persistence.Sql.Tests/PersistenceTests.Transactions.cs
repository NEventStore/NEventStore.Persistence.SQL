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
    using System.Diagnostics;
#endif
#if XUNIT
    using Xunit;
    using Xunit.Should;
#endif

    public enum TransactionScopeConcern
    {
        NoTransaction = 0,
        SuppressAmbientTransaction = 1,
        EnlistInAmbientTransaction = 2
    }

    // todo: add single thread tests....
    // https://github.com/NEventStore/NEventStore/issues/465

    /// <summary>
    /// This testing concern simulated the TrsanctionSuppression and/or re-enlisting in Ambient transaction behavior
    /// </summary>
    public abstract class TransactionConcern : PersistenceEngineConcern
    {
        protected void Reinitialize(TransactionScopeConcern enlistInAmbientTransation)
        {
            switch (enlistInAmbientTransation)
            {
                case TransactionScopeConcern.NoTransaction:
                    Fixture.ScopeOption = null;
                    break;
                case TransactionScopeConcern.SuppressAmbientTransaction:
                    Fixture.ScopeOption = TransactionScopeOption.Suppress;
                    break;
                case TransactionScopeConcern.EnlistInAmbientTransaction:
                    Fixture.ScopeOption = TransactionScopeOption.Required;
                    break;
            }
            Fixture.Initialize(ConfiguredPageSizeForTesting);
        }
    }

    public abstract class MultipleSequentialConnectionsWithSingleTransactionScope : TransactionConcern
    {
        protected ICommit[] _commits;
        protected const int Loop = 2;
        protected const int StreamsPerTransaction = 20;
        protected readonly IsolationLevel _transationIsolationLevel;
        protected readonly TransactionScopeConcern _enlistInAmbientTransaction;
        protected Exception _thrown;
        protected readonly bool _completeTransaction;

        protected MultipleSequentialConnectionsWithSingleTransactionScope(
            TransactionScopeConcern enlistInAmbientTransaction,
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
                // multiple connections on a single thread
                var eventStore = new OptimisticEventStore(Persistence, null);

                // Single transaction scope
                using (var scope = new TransactionScope(TransactionScopeOption.Required,
                        new TransactionOptions { IsolationLevel = _transationIsolationLevel }
                    , TransactionScopeAsyncFlowOption.Enabled
                ))
                {
                    for (int i = 0; i < Loop; i++)
                    {
                        int j;
                        for (j = 0; j < StreamsPerTransaction; j++)
                        {
                            var streamId = i.ToString() + "-" + j.ToString();
                            using (var stream = eventStore.OpenStream(streamId))
                            {
                                for (int k = 0; k < 10; k++)
                                {
                                    stream.Add(new EventMessage { Body = "body" + k });
                                }
                                Debug.WriteLine("Committing Stream: " + streamId);
                                stream.CommitChanges(Guid.NewGuid());
                            }
                        }
                    }
                    Debug.WriteLine("Completing transaction");
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
    [TestFixture(TransactionScopeConcern.SuppressAmbientTransaction, IsolationLevel.Serializable)]
    [TestFixture(TransactionScopeConcern.SuppressAmbientTransaction, IsolationLevel.ReadCommitted)]
    [TestFixture(TransactionScopeConcern.NoTransaction, IsolationLevel.Serializable)]
    [TestFixture(TransactionScopeConcern.NoTransaction, IsolationLevel.ReadCommitted)]
    [TestFixture(TransactionScopeConcern.EnlistInAmbientTransaction, IsolationLevel.Serializable)] // supported: no distributed transaction
    [TestFixture(TransactionScopeConcern.EnlistInAmbientTransaction, IsolationLevel.ReadCommitted)] // supported: no distributed transaction
#endif
    public class SingleThread_Single_Completing_TransactionScope_When_EnlistInAmbientTransaction_is_and_IsolationLevel_is
        : MultipleSequentialConnectionsWithSingleTransactionScope
    {
        public SingleThread_Single_Completing_TransactionScope_When_EnlistInAmbientTransaction_is_and_IsolationLevel_is(
            TransactionScopeConcern enlistInAmbientTransaction,
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
            _commits = Persistence.GetFrom(0).ToArray();
            _commits.Length.Should().Be(Loop * StreamsPerTransaction);
        }
    }

#if MSTEST
    [TestClass]
#endif
#if NUNIT
    [TestFixture(TransactionScopeConcern.SuppressAmbientTransaction, IsolationLevel.Serializable)]
    [TestFixture(TransactionScopeConcern.SuppressAmbientTransaction, IsolationLevel.ReadCommitted)]
#endif
    public class SingleThread_Single_Failing_TransactionScope_When_SuppressAmbientTransaction_is_and_IsolationLevel_is
        : MultipleSequentialConnectionsWithSingleTransactionScope
    {
        public SingleThread_Single_Failing_TransactionScope_When_SuppressAmbientTransaction_is_and_IsolationLevel_is(
            TransactionScopeConcern enlistInAmbientTransaction,
            IsolationLevel transationIsolationLevel
            ) : base(enlistInAmbientTransaction, transationIsolationLevel, completeTransaction: false)
        { }

        [Fact]
        public void should_not_throw_an_Exception()
        {
            _thrown.Should().BeNull();
        }

        [Fact]
        public void Should_have_expected_number_of_commits()
        {
            _commits = Persistence.GetFrom(0).ToArray();
            _commits.Length.Should().Be(Loop * StreamsPerTransaction);
        }
    }

#if MSTEST
    [TestClass]
#endif
#if NUNIT
    [TestFixture(TransactionScopeConcern.NoTransaction, IsolationLevel.Serializable)]
    [TestFixture(TransactionScopeConcern.NoTransaction, IsolationLevel.ReadCommitted)]
    [TestFixture(TransactionScopeConcern.EnlistInAmbientTransaction, IsolationLevel.Serializable)] // supported: no distributed transaction
    [TestFixture(TransactionScopeConcern.EnlistInAmbientTransaction, IsolationLevel.ReadCommitted)] // supported: no distributed transaction
#endif
    public class SingleThread_Single_Failing_TransactionScope_When_EnlistInAmbientTransaction_is_and_IsolationLevel_is
        : MultipleSequentialConnectionsWithSingleTransactionScope
    {
        public SingleThread_Single_Failing_TransactionScope_When_EnlistInAmbientTransaction_is_and_IsolationLevel_is(
            TransactionScopeConcern enlistInAmbientTransaction,
            IsolationLevel transationIsolationLevel
            ) : base(enlistInAmbientTransaction, transationIsolationLevel, completeTransaction: false)
        { }

        [Fact]
        public void should_not_throw_an_Exception()
        {
            _thrown.Should().BeNull();
        }

        [Fact]
        public void Should_have_no_commits()
        {
            _commits = Persistence.GetFrom(0).ToArray();
            _commits.Length.Should().Be(0);
        }
    }

    public abstract class MultipleParallelConnectionsWithMultipleTransactionScopes : TransactionConcern
    {
        protected ICommit[] _commits;
        protected const int Loop = 2;
        protected const int StreamsPerTransaction = 20;
        protected readonly IsolationLevel _transationIsolationLevel;
        protected readonly TransactionScopeConcern _enlistInAmbientTransaction;
        protected Exception _thrown;
        protected readonly bool _completeTransaction;

        protected MultipleParallelConnectionsWithMultipleTransactionScopes(
            TransactionScopeConcern enlistInAmbientTransaction,
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
            Parallel.For(0, Loop, i =>
            {
                // multiple parallel connections (open stream is called inside the for loop)
                var eventStore = new OptimisticEventStore(Persistence, null);

                // multiple transaction scopes: 1 for each connection
                using (var scope = new TransactionScope(TransactionScopeOption.Required,
                    new TransactionOptions { IsolationLevel = _transationIsolationLevel }
                , TransactionScopeAsyncFlowOption.Enabled
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
                    if (_completeTransaction)
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
    [TestFixture(TransactionScopeConcern.NoTransaction, IsolationLevel.ReadCommitted)]
    [TestFixture(TransactionScopeConcern.SuppressAmbientTransaction, IsolationLevel.Serializable)]
    [TestFixture(TransactionScopeConcern.SuppressAmbientTransaction, IsolationLevel.ReadCommitted)]
    [TestFixture(TransactionScopeConcern.EnlistInAmbientTransaction, IsolationLevel.ReadCommitted)]
#endif
    public class Multiple_Completing_TransactionScopes_When_EnlistInAmbientTransaction_is_and_IsolationLevel_is :
        MultipleParallelConnectionsWithMultipleTransactionScopes
    {
        public Multiple_Completing_TransactionScopes_When_EnlistInAmbientTransaction_is_and_IsolationLevel_is(
            TransactionScopeConcern enlistInAmbientTransaction,
            IsolationLevel transationIsolationLevel
            ) : base(enlistInAmbientTransaction, transationIsolationLevel, true)
        { }

        [Fact]
        public void should_not_throw_an_Exception()
        {
            _thrown.Should().BeNull();
        }

        [Fact]
        public void Should_have_expected_number_of_commits()
        {
            _commits = Persistence.GetFrom(0).ToArray();
            _commits.Length.Should().Be(Loop * StreamsPerTransaction);
        }
    }

#if MSTEST
    [TestClass]
#endif
#if NUNIT
    [TestFixture(TransactionScopeConcern.NoTransaction, IsolationLevel.Serializable)] // this will always fail! Serializable prevents multiple transation to perform insert queries simultaneously
    [TestFixture(TransactionScopeConcern.EnlistInAmbientTransaction, IsolationLevel.Serializable)] // this will always fail! Serializable prevents multiple transation to perform insert queries simultaneously
#endif
    public class Unsupported_Multiple_Completing_TransactionScopes_When_EnlistInAmbientTransaction_is_and_IsolationLevel_is :
        MultipleParallelConnectionsWithMultipleTransactionScopes
    {
        public Unsupported_Multiple_Completing_TransactionScopes_When_EnlistInAmbientTransaction_is_and_IsolationLevel_is(
            TransactionScopeConcern enlistInAmbientTransaction,
            IsolationLevel transationIsolationLevel
            ) : base(enlistInAmbientTransaction, transationIsolationLevel, true)
        { }

        [Fact]
        public void should_throw_an_Exception_only_if_no_transaction_or_enlist_in_ambient_transaction_and_IsolationLevel_is_Serializable()
        {
            _thrown.Should().BeOfType<AggregateException>();
            _thrown.InnerException.Should().BeOfType<StorageException>();
            // two serializable transactions on the same connection can result in deadlocks.
            _thrown.InnerException.Message.Should().Contain("deadlock");
        }

        [Fact]
        public void Should_have_unexpected_number_of_commits_instead_of_zero()
        {
            // unpredictable results: some transactions might succeed, other will deadlock
            _commits = Persistence.GetFrom(0).ToArray();
            _commits.Length.Should().BeGreaterThan(0);
            _commits.Length.Should().BeLessThan(Loop * StreamsPerTransaction);
        }
    }

#if MSTEST
    [TestClass]
#endif
#if NUNIT
    [TestFixture(TransactionScopeConcern.NoTransaction, IsolationLevel.ReadCommitted)]
    [TestFixture(TransactionScopeConcern.SuppressAmbientTransaction, IsolationLevel.Serializable)]
    [TestFixture(TransactionScopeConcern.SuppressAmbientTransaction, IsolationLevel.ReadCommitted)]
    [TestFixture(TransactionScopeConcern.EnlistInAmbientTransaction, IsolationLevel.ReadCommitted)]
#endif
    public class Multiple_Failing_TransactionScopes_When_EnlistInAmbientTransaction_is_and_IsolationLevel_is :
        MultipleParallelConnectionsWithMultipleTransactionScopes
    {
        public Multiple_Failing_TransactionScopes_When_EnlistInAmbientTransaction_is_and_IsolationLevel_is(
            TransactionScopeConcern enlistInAmbientTransaction,
            IsolationLevel transationIsolationLevel
            ) : base(enlistInAmbientTransaction, transationIsolationLevel, completeTransaction: false)
        { }

        [Fact]
        public void should_not_throw_an_Exception()
        {
            _thrown.Should().BeNull();
        }

        [Fact]
        public void Should_have_expected_number_of_commits()
        {
            _commits = Persistence.GetFrom(0).ToArray();
            if (_enlistInAmbientTransaction == TransactionScopeConcern.SuppressAmbientTransaction)
            {
                _commits.Length.Should().Be(Loop * StreamsPerTransaction);
            }
            else
            {
                _commits.Length.Should().Be(0);
            }
        }
    }

#if MSTEST
    [TestClass]
#endif
#if NUNIT
    [TestFixture(TransactionScopeConcern.NoTransaction, IsolationLevel.Serializable)] // this will always fail! Serializable prevents multiple transation to perform insert queries simultaneously
    [TestFixture(TransactionScopeConcern.EnlistInAmbientTransaction, IsolationLevel.Serializable)] // this will always fail! Serializable prevents multiple transation to perform insert queries simultaneously
#endif
    public class Unsupported_Multiple_Failing_TransactionScopes_When_EnlistInAmbientTransaction_is_and_IsolationLevel_is :
        MultipleParallelConnectionsWithMultipleTransactionScopes
    {
        public Unsupported_Multiple_Failing_TransactionScopes_When_EnlistInAmbientTransaction_is_and_IsolationLevel_is(
            TransactionScopeConcern enlistInAmbientTransaction,
            IsolationLevel transationIsolationLevel
            ) : base(enlistInAmbientTransaction, transationIsolationLevel, completeTransaction: false)
        { }

        [Fact]
        public void should_throw_an_Exception()
        {
            _thrown.Should().BeOfType<AggregateException>();
            _thrown.InnerException.Should().BeOfType<StorageException>();
            // two serializable transactions on the same connection can result in deadlocks.
            _thrown.InnerException.Message.Should().Contain("deadlock");
        }

        [Fact]
        public void Should_have_zero_commits()
        {
            _commits = Persistence.GetFrom(0).ToArray();
            _commits.Length.Should().Be(0);
        }
    }

    public abstract class MultipleParallelConnectionsWithSingleTransactionScope : TransactionConcern
    {
        protected ICommit[] _commits;
        protected const int Loop = 2;
        protected const int StreamsPerTransaction = 20;
        protected readonly IsolationLevel _transationIsolationLevel;
        protected readonly TransactionScopeConcern _enlistInAmbientTransaction;
        protected Exception _thrown;
        protected readonly bool _completeTransaction;

        protected MultipleParallelConnectionsWithSingleTransactionScope(
            TransactionScopeConcern enlistInAmbientTransaction,
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
                // multiple parallel connections (OpenStream is called inside the parallel for)
                var eventStore = new OptimisticEventStore(Persistence, null);

                // Single transaction scope
                using (var scope = new TransactionScope(TransactionScopeOption.Required,
                        new TransactionOptions { IsolationLevel = _transationIsolationLevel }
                    , TransactionScopeAsyncFlowOption.Enabled
                ))
                {
                    var res = Parallel.For(0, Loop, i =>
                    {
                        int j;
                        for (j = 0; j < StreamsPerTransaction; j++)
                        {
                            var streamId = i.ToString() + "-" + j.ToString();
                            using (var stream = eventStore.OpenStream(streamId))
                            {
                                for (int k = 0; k < 10; k++)
                                {
                                    stream.Add(new EventMessage { Body = "body" + k });
                                }
                                Debug.WriteLine("Committing Stream: " + streamId);
                                stream.CommitChanges(Guid.NewGuid());
                            }
                        }
                    });
                    Debug.WriteLine("Completing transaction");
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
    [TestFixture(TransactionScopeConcern.SuppressAmbientTransaction, IsolationLevel.Serializable)]
    [TestFixture(TransactionScopeConcern.SuppressAmbientTransaction, IsolationLevel.ReadCommitted)]
#endif
    public class Single_Completing_TransactionScope_When_EnlistInAmbientTransaction_is_and_IsolationLevel_is
        : MultipleParallelConnectionsWithSingleTransactionScope
    {
        public Single_Completing_TransactionScope_When_EnlistInAmbientTransaction_is_and_IsolationLevel_is(
            TransactionScopeConcern enlistInAmbientTransaction,
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
            _commits = Persistence.GetFrom(0).ToArray();
            _commits.Length.Should().Be(Loop * StreamsPerTransaction);
        }
    }

#if MSTEST
[TestClass]
#endif
#if NUNIT
    [TestFixture(TransactionScopeConcern.SuppressAmbientTransaction, IsolationLevel.Serializable)]
    [TestFixture(TransactionScopeConcern.SuppressAmbientTransaction, IsolationLevel.ReadCommitted)]
#endif
    public class Single_Failing_TransactionScope_When_EnlistInAmbientTransaction_is_and_IsolationLevel_is
        : MultipleParallelConnectionsWithSingleTransactionScope
    {
        public Single_Failing_TransactionScope_When_EnlistInAmbientTransaction_is_and_IsolationLevel_is(
            TransactionScopeConcern enlistInAmbientTransaction,
            IsolationLevel transationIsolationLevel
            ) : base(enlistInAmbientTransaction, transationIsolationLevel, completeTransaction: false)
        { }

        [Fact]
        public void should_not_throw_an_Exception()
        {
            _thrown.Should().BeNull();
        }

        [Fact]
        public void Should_have_expected_number_of_commits()
        {
            _commits = Persistence.GetFrom(0).ToArray();
            _commits.Length.Should().Be(Loop * StreamsPerTransaction);
        }
    }

    // the following scenarios are not supported and behave differently in several versions of the framework
    // these tests also cause problems with other tests with transactions being locked

#if MSTEST
[TestClass]
#endif
#if NUNIT
    [TestFixture(TransactionScopeConcern.NoTransaction, IsolationLevel.Serializable)]
    [TestFixture(TransactionScopeConcern.NoTransaction, IsolationLevel.ReadCommitted)]
    [TestFixture(TransactionScopeConcern.EnlistInAmbientTransaction, IsolationLevel.Serializable)] // unsupported: This platform does not support distributed transactions
    [TestFixture(TransactionScopeConcern.EnlistInAmbientTransaction, IsolationLevel.ReadCommitted)] // unsupported: This platform does not support distributed transactions
    [Explicit]
#endif
    public class Unsupported_Single_Completing_TransactionScope_When_EnlistInAmbientTransaction_is_and_IsolationLevel_is
           : MultipleParallelConnectionsWithSingleTransactionScope
    {
        public Unsupported_Single_Completing_TransactionScope_When_EnlistInAmbientTransaction_is_and_IsolationLevel_is(
            TransactionScopeConcern enlistInAmbientTransaction,
            IsolationLevel transationIsolationLevel
            ) : base(enlistInAmbientTransaction, transationIsolationLevel, completeTransaction: true)
        { }

#if NET462
        // some of these tests fails with a local instance of sql sever
        [Fact]
        public void should_throw_an_StorageUnavailableException()
        {
            Console.WriteLine($"net462 {_enlistInAmbientTransaction} {_transationIsolationLevel}");
            _thrown.Should().BeOfType<AggregateException>();
            AggregateException aex = _thrown as AggregateException;
            aex.InnerExceptions
                .Any(e => e.GetType().IsAssignableFrom(typeof(StorageUnavailableException)))
                .Should().BeTrue();

            //var storageExceptions = aex.InnerExceptions
            //    .Where(e => e.GetType().IsAssignableFrom(typeof(StorageUnavailableException)))
            //    .Select(e => e.Message);
            //storageExceptions.Should()
            //    .Match(c =>
            //        c.Contains("This platform does not support distributed transactions.")
            //        || c.Contains("The Promote method returned an invalid value for the distributed transaction.")
            //    );

            //.Contain("This platform does not support distributed transactions.");
            // the following error means the transaction is being promoted to a distributed one, and still not supported
            //    "The Promote method returned an invalid value for the distributed transaction.");
        }

        /* these tests works with a local instance of sql server
        [Fact]
        public void should_throw_an_StorageUnavailableException()
        {
            Console.WriteLine("net45");
            if (_transationIsolationLevel == IsolationLevel.Serializable) 
            {
                _thrown.Should().BeOfType<AggregateException>();
                AggregateException aex = _thrown as AggregateException;
                aex.InnerExceptions
                    .Any(e => e.GetType().IsAssignableFrom(typeof(StorageUnavailableException)))
                    .Should().BeTrue();
            } 
            else
            {
                Assert.Inconclusive();
            }
        }

        [Fact]
        public void WARNING_should_throw_an_StorageUnavailableException_but_it_works()
        {
            Console.WriteLine("net45 {_enlistInAmbientTransaction} {_transationIsolationLevel}");
            if (_transationIsolationLevel == IsolationLevel.ReadCommitted) {
                // this is actually very strange because I expected an exception
                // but with ReadCommitted and net45 it seems to work, it does
                // not with net451 or netstandard2.0.
                // for good measure we'll consider this an unsupported scenario
                _thrown.Should().BeNull();
            }
            else
            {
                Assert.Inconclusive();
            }
        }
        */
#else
        [Fact]
        public void should_throw_an_StorageUnavailableException()
        {
            Console.WriteLine("netstandard2.0 {_enlistInAmbientTransaction} {_transationIsolationLevel}");
            _thrown.Should().BeOfType<AggregateException>();
            AggregateException aex = _thrown as AggregateException;
            aex.InnerExceptions
                .Any(e => e.GetType().IsAssignableFrom(typeof(StorageUnavailableException)))
                .Should().BeTrue();

            //var storageExceptions = aex.InnerExceptions
            //    .Where(e => e.GetType().IsAssignableFrom(typeof(StorageUnavailableException)))
            //    .Select(e => e.Message);
            //storageExceptions.Should()
            //    .Match(c =>
            //        c.Contains("This platform does not support distributed transactions.")
            //        || c.Contains("The Promote method returned an invalid value for the distributed transaction.")
            //    );

            //.Contain("This platform does not support distributed transactions.");
            // the following error means the transaction is being promoted to a distributed one, and still not supported
            //    "The Promote method returned an invalid value for the distributed transaction.");
        }
#endif
    }

    // the following scenarios are not supported and behave differently in several versions of the framework
    // these tests also cause problems with other tests with transactions remaining locked

}

#pragma warning restore S101 // Types should be named in PascalCase
#pragma warning restore 169 // ReSharper disable InconsistentNaming
#pragma warning restore IDE1006 // Naming Styles
