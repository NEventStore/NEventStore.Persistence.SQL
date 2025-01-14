namespace NEventStore.Persistence.Sql
{
	using System.Data;
	using Microsoft.Extensions.Logging;
	using NEventStore.Logging;
	using NEventStore.Serialization;

	internal static class SnapshotExtensions
	{
		private const int BucketIdIndex = 0;
		private const int StreamRevisionIndex = 2;
		private const int PayloadIndex = 3;
		private static readonly ILogger Logger = LogFactory.BuildLogger(typeof(SnapshotExtensions));

		public static Snapshot GetSnapshot(this IDataRecord record, ISerialize serializer, string streamIdOriginal)
		{
			if (Logger.IsEnabled(LogLevel.Trace))
			{
				Logger.LogTrace(Messages.DeserializingSnapshot);
			}

			return new Snapshot(
				record[BucketIdIndex].ToString(),
				streamIdOriginal,
				record[StreamRevisionIndex].ToInt(),
				serializer.Deserialize<object>(record, PayloadIndex));
		}
	}
}