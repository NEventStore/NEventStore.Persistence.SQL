namespace NEventStore.Persistence.Sql
{
	using System;
	using System.Collections.Generic;
	using System.Data;
	using Microsoft.Extensions.Logging;
	using NEventStore.Logging;
	using NEventStore.Serialization;

	/// <summary>
	/// Contains extension methods for working with <see cref="IDataRecord"/> instances.
	/// </summary>
	public static class CommitExtensions
	{
		private const int BucketIdIndex = 0;
		private const int StreamIdIndex = 1;
		private const int StreamIdOriginalIndex = 2;
		private const int StreamRevisionIndex = 3;
		private const int CommitIdIndex = 4;
		private const int CommitSequenceIndex = 5;
		private const int CommitStampIndex = 6;
		private const int CheckpointIndex = 7;
		private const int HeadersIndex = 8;
		private const int PayloadIndex = 9;
		private static readonly ILogger Logger = LogFactory.BuildLogger(typeof(CommitExtensions));

		/// <summary>
		/// Deserializes a commit from the current record.
		/// </summary>
		public static ICommit GetCommit(this IDataRecord record, ISerialize serializer, ISerializeEvents eventSerializer, ISqlDialect sqlDialect)
		{
			Logger.LogTrace(Messages.DeserializingCommit, serializer.GetType());

			var bucketId = record[BucketIdIndex].ToString();
			var streamId = record[StreamIdOriginalIndex].ToString();
			var streamRevision = record[StreamRevisionIndex].ToInt();
			var commitId = record[CommitIdIndex].ToGuid();
			var commitSequence = record[CommitSequenceIndex].ToInt();
			var commitStamp = sqlDialect.ToDateTime(record[CommitStampIndex]);
			var checkpointToken = record.CheckpointNumber();
			var headers = serializer.Deserialize<Dictionary<string, object>>(record, HeadersIndex);
			var payloadBytes = record.GetByteArray(PayloadIndex);

			var events = eventSerializer.DeserializeEventMessages(payloadBytes, bucketId, streamId,
				streamRevision, commitId, commitSequence, commitStamp, checkpointToken, headers);

			return new Commit(bucketId,
				streamId,
				streamRevision,
				commitId,
				commitSequence,
				commitStamp,
				checkpointToken,
				headers,
				events);
		}
		/// <summary>
		/// Gets the StreamId from the current record.
		/// </summary>
		public static string StreamId(this IDataRecord record)
		{
			return record[StreamIdIndex].ToString();
		}
		/// <summary>
		/// Gets the Commit Sequence from the current record.
		/// </summary>
		public static int CommitSequence(this IDataRecord record)
		{
			return record[CommitSequenceIndex].ToInt();
		}
		/// <summary>
		/// Gets the Checkpoint from the current record.
		/// </summary>
		public static long CheckpointNumber(this IDataRecord record)
		{
			return record[CheckpointIndex].ToLong();
		}
		/// <summary>
		/// Deserializes the value at the specified index.
		/// </summary>
		public static T? Deserialize<T>(this ISerialize serializer, IDataRecord record, int index)
		{
			var bytes = record.GetByteArray(index);
			return bytes.Length == 0 ? default : serializer.Deserialize<T>(bytes);
		}

		internal static byte[] GetByteArray(this IDataRecord record, int index)
		{
			if (index >= record.FieldCount)
			{
				return [];
			}
			var value = record[index];

			if (value == null || value == DBNull.Value)
			{
				return [default];
			}
			return (byte[])value;
		}
	}
}