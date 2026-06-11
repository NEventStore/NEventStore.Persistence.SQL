#pragma warning disable IDE1006 // Naming Styles

using FluentAssertions;
using NEventStore.Persistence.AcceptanceTests.BDD;

#if MSTEST
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif
#if NUNIT
using NUnit.Framework;
#endif
#if XUNIT
using Xunit;
#endif

namespace NEventStore.Persistence.AcceptanceTests.Async
{
#if MSTEST
	[TestClass]
#endif
	public class when_getting_streams_to_snapshot_for_a_bucket_that_shares_stream_ids_with_other_buckets : PersistenceEngineConcernAsync
	{
		private const string BucketAId = "a";
		private const string BucketBId = "b";
		private const int Threshold = 2;
		private string? _streamId;
		private StreamHeadObserver? _observer;

		protected override async Task ContextAsync()
		{
			_streamId = Guid.NewGuid().ToString();

			var bucketAFirst = _streamId.BuildAttempt(bucketId: BucketAId);
			await Persistence.CommitAsync(bucketAFirst, CancellationToken.None);

			var bucketASecond = bucketAFirst.BuildNextAttempt();
			await Persistence.CommitAsync(bucketASecond, CancellationToken.None);

			await Persistence.CommitAsync(_streamId.BuildAttempt(bucketId: BucketBId), CancellationToken.None);
			await Persistence.AddSnapshotAsync(new Snapshot(BucketBId, _streamId, bucketASecond.StreamRevision, "SnapshotB"), CancellationToken.None);
		}

		protected override async Task BecauseAsync()
		{
			_observer = new StreamHeadObserver();
			await Persistence.GetStreamsToSnapshotAsync(BucketAId, Threshold, _observer, CancellationToken.None);
		}

		[Fact]
		public void should_return_only_bucket_a_stream_heads()
		{
			_observer!.StreamHeads.Should().ContainSingle(x => x.BucketId == BucketAId && x.StreamId == _streamId);
			_observer.StreamHeads.Should().NotContain(x => x.BucketId == BucketBId);
		}

		[Fact]
		public void should_not_let_a_bucket_b_snapshot_hide_bucket_a_snapshot_eligibility()
		{
			_observer!.StreamHeads.Should().ContainSingle(x => x.StreamId == _streamId && x.SnapshotRevision == 0);
		}
	}
}

#pragma warning restore IDE1006 // Naming Styles
