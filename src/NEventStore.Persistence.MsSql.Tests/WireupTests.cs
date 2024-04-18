using NEventStore.Persistence.Sql.Tests;

namespace NEventStore.Persistence.AcceptanceTests
{
	using System;
	using NEventStore.Persistence.AcceptanceTests.BDD;
	using NEventStore.Persistence.Sql;
	using NEventStore.Persistence.Sql.SqlDialects;
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

#if MSTEST
    [TestClass]
#endif
	public class When_specifying_a_hasher : SpecificationBase
	{
		private bool _hasherInvoked;
		private IStoreEvents _eventStore;

		protected override void Context()
		{
			_eventStore = Wireup
				.Init()
				.UsingSqlPersistence(new EnviromentConnectionFactory("MsSql", Microsoft.Data.SqlClient.SqlClientFactory.Instance))
				.WithDialect(new MsSqlDialect())
				.WithStreamIdHasher(streamId =>
				{
					_hasherInvoked = true;
					return new Sha1StreamIdHasher().GetHash(streamId);
				})
				// .EnableTransactionSuppression()
				// .EnlistInAmbientTransaction()
				.InitializeStorageEngine()
				.UsingBinarySerialization()
				.Build();
		}

		protected override void Cleanup()
		{
			if (_eventStore != null)
			{
				_eventStore.Advanced.Drop();
				_eventStore.Dispose();
			}
		}

		protected override void Because()
		{
			using (var stream = _eventStore.OpenStream(Guid.NewGuid()))
			{
				stream.Add(new EventMessage { Body = "Message" });
				stream.CommitChanges(Guid.NewGuid());
			}
		}

		[Fact]
		public void Should_invoke_hasher()
		{
			_hasherInvoked.Should().BeTrue();
		}
	}
}