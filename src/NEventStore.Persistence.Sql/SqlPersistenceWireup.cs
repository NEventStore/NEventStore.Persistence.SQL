// ReSharper disable once CheckNamespace
namespace NEventStore
{
    using System;
    using System.Transactions;
    using Microsoft.Extensions.Logging;
    using NEventStore.Logging;
    using NEventStore.Persistence.Sql;
    using NEventStore.Serialization;

    public class SqlPersistenceWireup : PersistenceWireup
    {
        private const int DefaultPageSize = 512;
        private static readonly ILogger Logger = LogFactory.BuildLogger(typeof(SqlPersistenceWireup));
        private int _pageSize = DefaultPageSize;

        public SqlPersistenceWireup(Wireup wireup, IConnectionFactory connectionFactory)
            : base(wireup)
        {
            Container.Register(TransactionSuppressionBehavior.Disabled);

            Logger.LogDebug(Messages.ConnectionFactorySpecified, connectionFactory);

            Logger.LogTrace(Messages.AutoDetectDialect);
            Container.Register<ISqlDialect>(_ => null); // auto-detect
            Container.Register<IStreamIdHasher>(_ => new Sha1StreamIdHasher());

            Container.Register(c =>
            {
                TransactionScopeOption? scopeOptions = null;
                if (c.Resolve<TransactionSuppressionBehavior>() == TransactionSuppressionBehavior.Enabled)
                {
                    scopeOptions = c.Resolve<TransactionScopeOption>();
                }
                return new SqlPersistenceFactory(
                    connectionFactory,
                    c.Resolve<ISerialize>(),
                    c.Resolve<ISqlDialect>(),
                    c.Resolve<IStreamIdHasher>(),
                    scopeOptions,
                    _pageSize).Build();
            });
        }

        public virtual SqlPersistenceWireup WithDialect(ISqlDialect instance)
        {
            Logger.LogDebug(Messages.DialectSpecified, instance.GetType());
            Container.Register(instance);
            return this;
        }

        public virtual SqlPersistenceWireup PageEvery(int records)
        {
            Logger.LogDebug(Messages.PagingSpecified, records);
            _pageSize = records;
            return this;
        }

        public virtual SqlPersistenceWireup WithStreamIdHasher(IStreamIdHasher instance)
        {
            Logger.LogDebug(Messages.StreamIdHasherSpecified, instance.GetType());
            Container.Register(instance);
            return this;
        }

        public virtual SqlPersistenceWireup WithStreamIdHasher(Func<string, string> getStreamIdHash)
        {
            return WithStreamIdHasher(new DelegateStreamIdHasher(getStreamIdHash));
        }

        /// <summary>
        /// This option will restore the previous NEventStore SQL Persistence behavior of suppressing
        /// surrounding transactions.
        /// The Persistence driver will create a private nested TransactionScope with <see cref="TransactionScopeOption.Suppress"/> 
        /// for each operation so that all of its operations run in a dedicated, separated transaction.
        /// </summary>
        /// <returns></returns>
        // [Obsolete("It Will be removed in a future release. Transaction management should be handled manually by the user.")]
        public SqlPersistenceWireup SuppressAmbientTransaction()
        {
            Logger.LogInformation("Enabling Suppress Ambient Transaction behavior (a TransactionScope with TransactionScopeOption.Suppress option will surround any operation).");
            Container.Register(TransactionSuppressionBehavior.Enabled);
            Container.Register(TransactionScopeOption.Suppress);
            return this;
        }

        /// <summary>
        /// Enables two-phase commit.
        /// NEventStore SQL Persistence can optionally suppress surrounding TransactionScopes 
        /// (The Persistence driver will create a 
        /// private nested TransactionScope with <see cref="TransactionScopeOption.Suppress"/> for each operation)
        /// so that all of its operations run in a dedicated, separated transaction.
        /// This option changes the behavior so that NEventStore enlists in a surrounding TransactionScope,
        /// if there is any (All the Persistence drivers that support transactions will create a 
        /// private nested TransactionScope with <see cref="TransactionScopeOption.Required"/> for each operation).
        /// </summary>
        /// <remarks>
        /// When two-phase commit is enabled you should also disable the <see cref="OptimisticPipelineHook"/>
        /// that provide some additionl concurrency checks to avoid useless roundtrips to the databases, because it
        /// is based on in-memory shared cached stream state that might not be valid if transactions rollback.
        /// </remarks>
        /// <returns></returns>
        // [Obsolete("It Will be removed in a future release. Transaction management should be handled manually by the user.")]
        public virtual SqlPersistenceWireup EnlistInAmbientTransaction()
        {
            Logger.LogInformation("Configuring persistence engine to enlist in ambient transactions using TransactionScope.");
            Container.Register(TransactionSuppressionBehavior.Enabled);
            Container.Register(TransactionScopeOption.Required);
            return this;
            /*
            // check if EnableTransactionSuppression was previously called.
            if (Container.Resolve<DeprecatedTransactionSuppressionBehavior>() == DeprecatedTransactionSuppressionBehavior.Enabled)
            {
                Logger.LogInformation("Configuring persistence engine to enlist in ambient transactions using TransactionScope.");
                Container.Register(TransactionScopeOption.Required);
                return this;
            }
            throw new NotSupportedException($"Cannot enlist in Ambient transaction if .{nameof(EnableTransactionSuppression)} is not called before.");
            */
        }
    }
}