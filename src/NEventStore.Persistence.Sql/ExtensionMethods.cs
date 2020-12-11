namespace NEventStore.Persistence.Sql
{
    using System;
    using System.Data;
    using Microsoft.Extensions.Logging;
    using NEventStore.Logging;

    internal static class ExtensionMethods
    {
        private static readonly ILogger Logger = LogFactory.BuildLogger(typeof (ExtensionMethods));

        public static Guid ToGuid(this object value)
        {
            if (value is Guid guid)
            {
                return guid;
            }

            return value is byte[] bytes ? new Guid(bytes) : Guid.Empty;
        }

        public static int ToInt(this object value)
        {
            if (value is int x)
            {
                return x;
            }
            else if (value is long x2)
            {
                return (int)x2;
            }
            else if (value is decimal x3)
            {
                return (int)x3;
            }
            else
            {
                return Convert.ToInt32(value);
            }
        }

        public static long ToLong(this object value)
        {
            if (value is long x)
            {
                return x;
            }
            else if (value is int x2)
            {
                return x2;
            }
            else if (value is decimal x3)
            {
                return (long)x3;
            }
            else
            {
                return Convert.ToInt32(value);
            }
        }

        public static IDbCommand SetParameter(this IDbCommand command, string name, object value, DbType? parameterType = null)
        {
            Logger.LogTrace("Rebinding parameter '{0}' with value: {1}", name, value);
            var parameter = (IDataParameter) command.Parameters[name];
            parameter.Value = value;
            if (parameterType.HasValue)
              parameter.DbType = parameterType.Value;
            return command;
        }
    }
}