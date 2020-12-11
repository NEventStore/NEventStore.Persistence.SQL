namespace NEventStore.Persistence.Sql.SqlDialects
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Transactions;
    using Microsoft.Extensions.Logging;
    using NEventStore.Logging;
    using NEventStore.Persistence.Sql;

    public class CommonDbStatement : IDbStatement
    {
        private const int InfinitePageSize = 0;
        private static readonly ILogger Logger = LogFactory.BuildLogger(typeof(CommonDbStatement));
        private readonly IDbConnection _connection;
        private readonly ISqlDialect _dialect;
        private readonly TransactionScope _scope;
        private readonly IDbTransaction _transaction;

        public CommonDbStatement(
            ISqlDialect dialect,
            TransactionScope scope,
            IDbConnection connection,
            IDbTransaction transaction)
        {
            Parameters = new Dictionary<string, Tuple<object, DbType?>>();

            _dialect = dialect;
            _scope = scope;
            _connection = connection;
            _transaction = transaction;
        }

        protected IDictionary<string, Tuple<object, DbType?>> Parameters { get; }

        protected ISqlDialect Dialect
        {
            get { return _dialect; }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public virtual int PageSize { get; set; }

        public virtual void AddParameter(string name, object value, DbType? parameterType = null)
        {
            Logger.LogDebug(Messages.AddingParameter, name);
            Parameters[name] = Tuple.Create(_dialect.CoalesceParameterValue(value), parameterType);
        }

        public virtual int ExecuteWithoutExceptions(string commandText)
        {
            try
            {
                return ExecuteNonQuery(commandText);
            }
            catch (Exception)
            {
                Logger.LogDebug(Messages.ExceptionSuppressed);
                return 0;
            }
        }

        public virtual int ExecuteNonQuery(string commandText)
        {
            try
            {
                using (IDbCommand command = BuildCommand(commandText))
                {
                    return command.ExecuteNonQuery();
                }
            }
            catch (Exception e)
            {
                if (_dialect.IsDuplicate(e))
                {
                    throw new UniqueKeyViolationException(e.Message, e);
                }

                throw;
            }
        }

        public virtual object ExecuteScalar(string commandText)
        {
            try
            {
                using (IDbCommand command = BuildCommand(commandText))
                {
                    return command.ExecuteScalar();
                }
            }
            catch (Exception e)
            {
                if (_dialect.IsDuplicate(e))
                {
                    throw new UniqueKeyViolationException(e.Message, e);
                }
                throw;
            }
        }

        public virtual IEnumerable<IDataRecord> ExecuteWithQuery(string queryText)
        {
            return ExecuteQuery(queryText, (query, latest) => { }, InfinitePageSize);
        }

        public virtual IEnumerable<IDataRecord> ExecutePagedQuery(string queryText, NextPageDelegate nextpage)
        {
            int pageSize = _dialect.CanPage ? PageSize : InfinitePageSize;
            if (pageSize > 0)
            {
                Logger.LogTrace(Messages.MaxPageSize, pageSize);
                Parameters.Add(_dialect.Limit, Tuple.Create((object)pageSize, (DbType?)null));
            }

            return ExecuteQuery(queryText, nextpage, pageSize);
        }

        protected virtual void Dispose(bool disposing)
        {
            Logger.LogTrace(Messages.DisposingStatement);

            _transaction?.Dispose();

            _connection?.Dispose();

            _scope?.Dispose();
        }

        protected virtual IEnumerable<IDataRecord> ExecuteQuery(string queryText, NextPageDelegate nextpage, int pageSize)
        {
            Parameters.Add(_dialect.Skip, Tuple.Create((object)0, (DbType?)null));
            IDbCommand command = BuildCommand(queryText);

            try
            {
                return new PagedEnumerationCollection(_scope, _dialect, command, nextpage, pageSize, this);
            }
            catch (Exception)
            {
                command.Dispose();
                throw;
            }
        }

        protected virtual IDbCommand BuildCommand(string statement)
        {
            Logger.LogTrace(Messages.CreatingCommand);
            IDbCommand command = _connection.CreateCommand();

            if (Settings.CommandTimeout > 0)
            {
                command.CommandTimeout = Settings.CommandTimeout;
            }

            command.Transaction = _transaction;
            command.CommandText = statement;

            Logger.LogTrace(Messages.ClientControlledTransaction, _transaction != null);
            Logger.LogTrace(Messages.CommandTextToExecute, statement);

            BuildParameters(command);

            return command;
        }

        protected virtual void BuildParameters(IDbCommand command)
        {
            foreach (var item in Parameters)
            {
                BuildParameter(command, item.Key, item.Value.Item1, item.Value.Item2);
            }
        }

        protected virtual void BuildParameter(IDbCommand command, string name, object value, DbType? dbType)
        {
            IDbDataParameter parameter = command.CreateParameter();
            parameter.ParameterName = name;
            SetParameterValue(parameter, value, dbType);

            Logger.LogTrace(Messages.BindingParameter, name, parameter.Value);
            command.Parameters.Add(parameter);
        }

        protected virtual void SetParameterValue(IDataParameter param, object value, DbType? type)
        {
            param.Value = value ?? DBNull.Value;
            param.DbType = type ?? (value == null ? DbType.Binary : param.DbType);
        }
    }
}