namespace NEventStore.Persistence.Sql
{
    using System.Transactions;

    using NEventStore.Serialization;

    public class FirebirdSqlPersistenceFactory : SqlPersistenceFactory
    {
        private readonly TransactionScopeOption _transactionScopeOption;

        public FirebirdSqlPersistenceFactory(string connectionName, ISerialize serializer, ISqlDialect dialect = null)
            : base(connectionName, serializer, dialect)
        {}

        public FirebirdSqlPersistenceFactory(IConnectionFactory factory, ISerialize serializer, ISqlDialect dialect, IStreamIdHasher streamIdHasher = null, TransactionScopeOption scopeOption = TransactionScopeOption.Suppress, int pageSize = 128)
            : base(factory, serializer, dialect, streamIdHasher, scopeOption, pageSize)
        {
            this._transactionScopeOption = scopeOption;
        }

        public override IPersistStreams Build()
        {
            return new FirebirdPersistenceEngine(base.ConnectionFactory, base.Dialect, base.Serializer, _transactionScopeOption, base.PageSize, base.StreamIdHasher);
        }
    }
}
