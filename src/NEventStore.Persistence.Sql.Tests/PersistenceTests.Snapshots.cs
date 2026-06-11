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

namespace NEventStore.Persistence.AcceptanceTests
{
#if MSTEST
	[TestClass]
#endif
	public class when_getting_streams_to_snapshot_for_a_bucket_that_shares_stream_ids_with_other_buckets : PersistenceEngineConcern
	{
		private const string BucketAId = "a";
		private const string BucketBId = "b";
		private const int Threshold = 2;
		private string? _streamId;
		private IStreamHead[]? _streamsToSnapshot;

		protected override void Context()
		{
			_streamId = Guid.NewGuid().ToString();

			var bucketAFirst = _streamId.BuildAttempt(bucketId: BucketAId);
			Persistence.Commit(bucketAFirst);

			var bucketASecond = bucketAFirst.BuildNextAttempt();
			Persistence.Commit(bucketASecond);

			Persistence.Commit(_streamId.BuildAttempt(bucketId: BucketBId));
			Persistence.AddSnapshot(new Snapshot(BucketBId, _streamId, bucketASecond.StreamRevision, "SnapshotB"));
		}

		protected override void Because()
		{
			_streamsToSnapshot = Persistence.GetStreamsToSnapshot(BucketAId, Threshold).ToArray();
		}

		[Fact]
		public void should_return_only_bucket_a_stream_heads()
		{
			_streamsToSnapshot.Should().ContainSingle(x => x.BucketId == BucketAId && x.StreamId == _streamId);
			_streamsToSnapshot.Should().NotContain(x => x.BucketId == BucketBId);
		}

		[Fact]
		public void should_not_let_a_bucket_b_snapshot_hide_bucket_a_snapshot_eligibility()
		{
			_streamsToSnapshot.Should().ContainSingle(x => x.StreamId == _streamId && x.SnapshotRevision == 0);
		}
	}
}

#pragma warning restore IDE1006 // Naming Styles
