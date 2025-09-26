// These Tests have to be back ported to NEventStore core because they test the IPersistStreams interface
// You can safely remove them once we update NEventStore.

#pragma warning disable 169 // ReSharper disable InconsistentNaming
#pragma warning disable IDE1006 // Naming Styles

using NEventStore.Persistence.AcceptanceTests.BDD;
using FluentAssertions;
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

namespace NEventStore.Persistence.AcceptanceTests.Async
{

    /// <summary>
    /// <para>
    /// NEventStore.Persistence.Sql issue #54
    /// GetStreamsToSnapshot fails when number of streams exceeds PageSize.
    /// </para>
    /// <para>
    /// Query implementation was wrong: checking how the query and the pagination were implemented
    /// it was lacking to check for C.StreamId > @StreamId.
    /// which is needed to page through the results (similar to @Coalesce in GetCommitsFromStartingRevision)
    /// </para>
    /// </summary>
#if MSTEST
    [TestClass]
#endif
    public class when_getting_streams_to_snapshot_amount_exceeds_PageSize : PersistenceEngineConcernAsync
    {
        private int _moreThanPageSize;
        private Guid _lastStreamId;

        protected override async Task BecauseAsync()
        {
            _moreThanPageSize = ConfiguredPageSizeForTesting + 1;
            var eventStore = new OptimisticEventStore(Persistence, null, null);
            for (int i = 0; i < _moreThanPageSize; i++)
            {
                _lastStreamId = Guid.NewGuid();
                using IEventStream stream = await eventStore.OpenStreamAsync(_lastStreamId).ConfigureAwait(false);
                stream.Add(new EventMessage { Body = new TestEvent() { S = "Hi " + i } });
                await stream.CommitChangesAsync(Guid.NewGuid(), CancellationToken.None).ConfigureAwait(false);
                stream.Add(new EventMessage { Body = new TestEvent() { S = "Hi " + i } });
                await stream.CommitChangesAsync(Guid.NewGuid(), CancellationToken.None).ConfigureAwait(false);
                stream.Add(new EventMessage { Body = new TestEvent() { S = "Hi " + i } });
                await stream.CommitChangesAsync(Guid.NewGuid(), CancellationToken.None).ConfigureAwait(false);
            }
        }

        [Fact]
        public void GetStreamsToSnapshot_does_not_crash_and_returns_all_the_streams()
        {
            Assert.DoesNotThrowAsync(async () =>
            {
                var observer = new StreamHeadObserver();
                await Persistence.GetStreamsToSnapshotAsync(0, observer, CancellationToken.None).ConfigureAwait(false);
                observer.StreamHeads.Count.Should().Be(_moreThanPageSize);
            });
        }

        [Fact]
        public void GetFrom_does_not_crash_and_returns_all_the_streams()
        {
            Assert.DoesNotThrowAsync(async () =>
            {
                var observer = new CommitStreamObserver();
                await Persistence.GetFromAsync(Bucket.Default, _lastStreamId.ToString(), 0, int.MaxValue, observer, CancellationToken.None).ConfigureAwait(false);
                observer.Commits.Count.Should().Be(3);
            });
        }
    }

}

#pragma warning restore 169 // ReSharper disable InconsistentNaming
#pragma warning restore IDE1006 // Naming Styles
