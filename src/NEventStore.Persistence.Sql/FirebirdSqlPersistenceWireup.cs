namespace NEventStore
{
    using System.Transactions;
    using System;

    using NEventStore.Logging;
    using NEventStore.Persistence.Sql;
    using NEventStore.Serialization;

    /// <summary>
    /// Class FirebirdSqlPersistenceWireup. Allows the usage of the FirebirdSqlPersistenceFactory which sends differente statements in different commands to the database.
    /// This is due to a problem with the .NET Provider.
    /// </summary>
    public class FirebirdSqlPersistenceWireup: PersistenceWireup
    {
        private const int DefaultPageSize = 512;
        private static readonly ILog Logger = LogFactory.BuildLogger(typeof(SqlPersistenceWireup));
        private int _pageSize = DefaultPageSize;

        /// <summary>
        /// Initializes a new instance of the <see cref="FirebirdSqlPersistenceWireup"/> class.
        /// </summary>
        /// <param name="wireup">The wireup.</param>
        /// <param name="connectionFactory">The connection factory.</param>
        public FirebirdSqlPersistenceWireup(Wireup wireup, IConnectionFactory connectionFactory)
            : base(wireup)
        {
            Container.Register<ISqlDialect>(c => null); // auto-detect
            Container.Register<IStreamIdHasher>(c => new Sha1StreamIdHasher());

            Container.Register(c => new FirebirdSqlPersistenceFactory(
                connectionFactory,
                c.Resolve<ISerialize>(),
                c.Resolve<ISqlDialect>(),
                c.Resolve<IStreamIdHasher>(),
                c.Resolve<TransactionScopeOption>(),
                _pageSize).Build());
        }

        /// <summary>
        /// Withes the dialect.
        /// </summary>
        /// <param name="instance">The instance.</param>
        /// <returns>FirebirdSqlPersistenceWireup.</returns>
        public virtual FirebirdSqlPersistenceWireup WithDialect(ISqlDialect instance)
        {
            Container.Register(instance);
            return this;
        }

        /// <summary>
        /// Pages the every.
        /// </summary>
        /// <param name="records">The records.</param>
        /// <returns>FirebirdSqlPersistenceWireup.</returns>
        public virtual FirebirdSqlPersistenceWireup PageEvery(int records)
        {
            _pageSize = records;
            return this;
        }

        /// <summary>
        /// Withes the stream identifier hasher.
        /// </summary>
        /// <param name="instance">The instance.</param>
        /// <returns>FirebirdSqlPersistenceWireup.</returns>
        public virtual FirebirdSqlPersistenceWireup WithStreamIdHasher(IStreamIdHasher instance)
        {
            Container.Register(instance);
            return this;
        }

        /// <summary>
        /// Withes the stream identifier hasher.
        /// </summary>
        /// <param name="getStreamIdHash">The get stream identifier hash.</param>
        /// <returns>FirebirdSqlPersistenceWireup.</returns>
        public virtual FirebirdSqlPersistenceWireup WithStreamIdHasher(Func<string, string> getStreamIdHash)
        {
            return WithStreamIdHasher(new DelegateStreamIdHasher(getStreamIdHash));
        }
    }
}
