namespace NEventStore.Persistence.Sql.SqlDialects
{
    using System;

    public class SqliteDialect : CommonSqlDialect
    {
        public override string InitializeStorage
        {
            get { return SqliteStatements.InitializeStorage; }
        }

        // Sqlite wants all parameters to be a part of the query
        public override string GetCommitsFromStartingRevision
        {
            get { return base.GetCommitsFromStartingRevision.Replace("\n ORDER BY ", "\n  AND @Skip = @Skip\nORDER BY "); }
        }

        public override string PersistCommit
        {
            get { return SqliteStatements.PersistCommit; }
        }

        public override bool IsDuplicate(Exception exception)
        {
            string message = exception.Message.ToUpperInvariant();
            return message.Contains("DUPLICATE") || message.Contains("UNIQUE") || message.Contains("CONSTRAINT");

        }

        public override DateTime ToDateTime(object value)
        {
#if !NETSTANDARD2_0
            return ((DateTime) value).ToUniversalTime();
#else
            // original code
            // return ((DateTime) value).ToUniversalTime();

            // not working, 'value' is already an utc value in ISO86001 format
            // Convert.ToDateTime() will return an unspecified kind
            // return Convert.ToDateTime(value).ToUniversalTime();

            // CommitStap is always an UTC value
            return DateTime.SpecifyKind(Convert.ToDateTime(value), DateTimeKind.Utc);
#endif
        }
    }
}