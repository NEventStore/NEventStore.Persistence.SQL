// ReSharper disable once CheckNamespace
namespace NEventStore
{
    using System;
    using System.Transactions;
    using NEventStore.Logging;
    using NEventStore.Persistence.Sql;
    using NEventStore.Serialization;

    public class SqlPersistenceWireup : PersistenceWireup
    {
        private const int DefaultPageSize = 512;
        private static readonly ILog Logger = LogFactory.BuildLogger(typeof(SqlPersistenceWireup));
        private int _pageSize = DefaultPageSize;

        public SqlPersistenceWireup(Wireup wireup, IConnectionFactory connectionFactory)
            : base(wireup)
        {
            Container.Register(TransactionScopeOption.Suppress);

            if (Logger.IsDebugEnabled) Logger.Debug(Messages.ConnectionFactorySpecified, connectionFactory);

            if (Logger.IsVerboseEnabled) Logger.Verbose(Messages.AutoDetectDialect);
            Container.Register<ISqlDialect>(_ => null); // auto-detect
            Container.Register<IStreamIdHasher>(_ => new Sha1StreamIdHasher());

            Container.Register(c => new SqlPersistenceFactory(
                connectionFactory,
                c.Resolve<ISerialize>(),
                c.Resolve<ISqlDialect>(),
                c.Resolve<IStreamIdHasher>(),
                c.Resolve<TransactionScopeOption>(),
                _pageSize).Build());
        }

        public virtual SqlPersistenceWireup WithDialect(ISqlDialect instance)
        {
            if (Logger.IsDebugEnabled) Logger.Debug(Messages.DialectSpecified, instance.GetType());
            Container.Register(instance);
            return this;
        }

        public virtual SqlPersistenceWireup PageEvery(int records)
        {
            if (Logger.IsDebugEnabled) Logger.Debug(Messages.PagingSpecified, records);
            _pageSize = records;
            return this;
        }

        public virtual SqlPersistenceWireup WithStreamIdHasher(IStreamIdHasher instance)
        {
            if (Logger.IsDebugEnabled) Logger.Debug(Messages.StreamIdHasherSpecified, instance.GetType());
            Container.Register(instance);
            return this;
        }

        public virtual SqlPersistenceWireup WithStreamIdHasher(Func<string, string> getStreamIdHash)
        {
            return WithStreamIdHasher(new DelegateStreamIdHasher(getStreamIdHash));
        }

        /// <summary>
        /// Enables two-phase commit.
        /// By default NEventStore will suppress surrounding TransactionScopes 
        /// (All the Persistence drivers that support transactions will create a 
        /// private nested TransactionScope with <see cref="TransactionScopeOption.Suppress"/> for each operation)
        /// so that all of its operations run in a dedicated, separate transaction.
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
        public virtual PersistenceWireup EnlistInAmbientTransaction()
        {
            if (Logger.IsInfoEnabled) Logger.Info("Configuring persistence engine to enlist in ambient transactions using TransactionScope.");
            Container.Register(TransactionScopeOption.Required);
            return this;
        }
    }
}