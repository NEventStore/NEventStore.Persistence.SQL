namespace NEventStore.Persistence.Sql.SqlDialects
{
    using System;

    public class PostgreSqlDialect : CommonSqlDialect
    {
        public override string InitializeStorage
        {
            get { return PostgreSqlStatements.InitializeStorage; }
        }

        public override string PersistCommit
        {
            get { return PostgreSqlStatements.PersistCommits; }
        }

        public override bool IsDuplicate(Exception exception)
        {
            string message = exception.Message.ToUpperInvariant();
            return message.Contains("23505") || message.Contains("IX_COMMITS_COMMITSEQUENCE");
        }
    }
}