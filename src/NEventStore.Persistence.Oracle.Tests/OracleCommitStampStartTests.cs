#pragma warning disable IDE1006 // Naming Styles

using FluentAssertions;
using NEventStore.Persistence.AcceptanceTests;
using NEventStore.Persistence.AcceptanceTests.BDD;
using NEventStore.Persistence.Sql.SqlDialects;
using NUnit.Framework;

namespace NEventStore.Persistence.Oracle.Tests
{
	public class when_getting_the_oracle_commit_stamp_start_parameter
	{
		private Exception? _thrown;
		private string? _parameterName;

		[SetUp]
		public void SetUp()
		{
			_thrown = Catch.Exception(() => _parameterName = new OracleNativeDialect().CommitStampStart);
		}

		[Test]
		public void should_return_the_oracle_parameter_name_without_recursing()
		{
			_thrown.Should().BeNull();
			_parameterName.Should().Be(":CommitStampStart");
		}
	}
}

namespace NEventStore.Persistence.AcceptanceTests
{
	public class when_reading_oracle_commits_between_commit_stamps : PersistenceEngineConcern
	{
		private const string BucketId = "oracle-date-range";
		private CommitAttempt? _insideRange;
		private CommitAttempt? _outsideRange;
		private ICommit[]? _commits;
		private Exception? _thrown;

		protected override void Context()
		{
			DateTime start = SystemTime.UtcNow.AddYears(1);
			_insideRange = Guid.NewGuid().ToString().BuildAttempt(start.AddSeconds(1), BucketId);
			_outsideRange = Guid.NewGuid().ToString().BuildAttempt(start.AddMinutes(5), BucketId);

			Persistence.Commit(_insideRange);
			Persistence.Commit(_outsideRange);
		}

		protected override void Because()
		{
			DateTime startDate = _insideRange!.CommitStamp.AddSeconds(-1);
			DateTime endDate = _insideRange.CommitStamp.AddSeconds(1);

			_thrown = Catch.Exception(() =>
				_commits = Persistence.GetFromTo(BucketId, startDate, endDate).ToArray());
		}

		[Test]
		public void should_query_the_date_range_without_recursing()
		{
			_thrown.Should().BeNull();
		}

		[Test]
		public void should_return_only_commits_inside_the_requested_range()
		{
			_commits.Should().ContainSingle(x => x.CommitId == _insideRange!.CommitId);
			_commits.Should().NotContain(x => x.CommitId == _outsideRange!.CommitId);
		}
	}
}

#pragma warning restore IDE1006 // Naming Styles
