// ReSharper disable once CheckNamespace
namespace NEventStore
{
    using System;
    using System.Transactions;
    using NEventStore.Logging;
    using NEventStore.Persistence.Sql;
    using NEventStore.Serialization;

    internal enum TransactionSuppressionBehavior
    {
        Disabled = 0,
        Enabled = 1
    }
}