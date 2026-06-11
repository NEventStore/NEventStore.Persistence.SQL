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
	public class when_reading_empty_async_pages_with_infinite_page_size : PersistenceEngineConcernAsync
	{
		private CommitStreamObserver? _checkpointObserver;
		private CommitStreamObserver? _bucketObserver;
		private Exception? _checkpointException;
		private Exception? _bucketException;

		protected override void Context()
		{
			Fixture!.Dispose();
			Fixture.Initialize(0);
		}

		protected override async Task BecauseAsync()
		{
			_checkpointObserver = new CommitStreamObserver();
			_bucketObserver = new CommitStreamObserver();

			_checkpointException = await Catch.ExceptionAsync(() =>
				ReadWithTimeout(token => Persistence.GetFromAsync(0, _checkpointObserver, token)));

			_bucketException = await Catch.ExceptionAsync(() =>
				ReadWithTimeout(token => Persistence.GetFromAsync(Bucket.Default, 0, _bucketObserver, token)));
		}

		[Fact]
		public void should_complete_checkpoint_reads_without_waiting_for_another_empty_page()
		{
			_checkpointException.Should().BeNull();
			_checkpointObserver!.Commits.Should().BeEmpty();
		}

		[Fact]
		public void should_complete_bucket_checkpoint_reads_without_waiting_for_another_empty_page()
		{
			_bucketException.Should().BeNull();
			_bucketObserver!.Commits.Should().BeEmpty();
		}

		private static async Task ReadWithTimeout(Func<CancellationToken, Task> read)
		{
			using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
			await read(timeout.Token);
		}
	}
}

#pragma warning restore IDE1006 // Naming Styles
