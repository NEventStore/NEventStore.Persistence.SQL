using System.Data;
using System.Transactions;
using IsolationLevel = System.Data.IsolationLevel;

namespace NEventStore.Persistence.Sql.SqlDialects {
    using System;
    using System.Data.SqlClient;

    public class MsSqlDialect : CommonSqlDialect {
        private const int UniqueIndexViolation = 2601;
        private const int UniqueKeyViolation = 2627;

        /// <summary>
        /// Add "WITH (READCOMMITTEDLOCK)" hint to any "FROM Commits" clause
        /// (#31) Make MsSqlDialect compatible with AzureSql and READ COMMITTED SNAPSHOT
        /// </summary>
        private readonly bool _addReadCommittedLockToFromCommits;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="addReadCommittedLockToFromCommits">Add "WITH (READCOMMITTEDLOCK)" hint to any "FROM Commits" clause, can have an impact on multiple transactions scenarios</param>
        public MsSqlDialect(bool addReadCommittedLockToFromCommits = false) {
            _addReadCommittedLockToFromCommits = addReadCommittedLockToFromCommits;
        }

        public override string InitializeStorage {
            get { return MsSqlStatements.InitializeStorage; }
        }

        public override string GetSnapshot {
            get { return "SET ROWCOUNT 1;\n" + base.GetSnapshot.Replace("LIMIT 1;", ";"); }
        }

        public override string GetCommitsFromStartingRevision {
            get { return AddReadCommittedLockToFromCommits(NaturalPaging(base.GetCommitsFromStartingRevision)); }
        }

        public override string GetCommitsFromInstant {
            get { return AddReadCommittedLockToFromCommits(CommonTableExpressionPaging(base.GetCommitsFromInstant)); }
        }

        public override string GetCommitsFromToInstant {
            get { return AddReadCommittedLockToFromCommits(CommonTableExpressionPaging(base.GetCommitsFromToInstant)); }
        }

        public override string PersistCommit {
            get { return MsSqlStatements.PersistCommits; }
        }

        public override string GetCommitsFromCheckpoint {
            get { return AddReadCommittedLockToFromCommits(CommonTableExpressionPaging(base.GetCommitsFromCheckpoint)); }
        }

        public override string GetCommitsFromToCheckpoint {
            get { return AddReadCommittedLockToFromCommits(CommonTableExpressionPaging(base.GetCommitsFromToCheckpoint)); }
        }

        public override string GetCommitsFromBucketAndCheckpoint {
            get { return AddReadCommittedLockToFromCommits(CommonTableExpressionPaging(base.GetCommitsFromBucketAndCheckpoint)); }
        }

        public override string GetCommitsFromToBucketAndCheckpoint {
            get { return AddReadCommittedLockToFromCommits(CommonTableExpressionPaging(base.GetCommitsFromToBucketAndCheckpoint)); }
        }

        public override string GetStreamsRequiringSnapshots {
            get { return NaturalPaging(base.GetStreamsRequiringSnapshots); }
        }

        private static string NaturalPaging(string query) {
            return "SET ROWCOUNT @Limit;\n" + RemovePaging(query);
        }

        private static string CommonTableExpressionPaging(string query) {
            query = RemovePaging(query);
            int orderByIndex = query.IndexOf("ORDER BY");
            string orderBy = query.Substring(orderByIndex).Replace(";", string.Empty);
            query = query.Substring(0, orderByIndex);

            int fromIndex = query.IndexOf("FROM ");
            string from = query.Substring(fromIndex);
            string select = query.Substring(0, fromIndex);

            return MsSqlStatements.PagedQueryFormat.FormatWith(select, orderBy, from);
        }

        private static string RemovePaging(string query) {
            return query
                .Replace("\n LIMIT @Limit OFFSET @Skip;", ";")
                .Replace("\n LIMIT @Limit;", ";");
        }

        public override bool IsDuplicate(Exception exception) {
            var dbException = exception as SqlException;
            return dbException != null
                   && (dbException.Number == UniqueIndexViolation || dbException.Number == UniqueKeyViolation);
        }

        public override IDbTransaction OpenTransaction(IDbConnection connection) {
            if (Transaction.Current == null)
                return connection.BeginTransaction(IsolationLevel.ReadCommitted);

            return base.OpenTransaction(connection);
        }

        /// <summary>
        /// (#31) Add 'WITH (READCOMMITTEDLOCK)' to all 'FROM Commits' statements
        /// </summary>
        /// <param name="query"></param>
        private string AddReadCommittedLockToFromCommits(string query) {
            if (!_addReadCommittedLockToFromCommits) {
                return query;
            }
            return query.Replace("FROM Commits", "FROM Commits WITH (READCOMMITTEDLOCK)");
        }
    }

    public class MsSql2005Dialect : MsSqlDialect {
        public MsSql2005Dialect(bool addReadCommittedLockToFromCommits = false) : base(addReadCommittedLockToFromCommits) {
        }

        public override DbType GetDateTimeDbType() {
            return DbType.DateTime;
        }
    }
}