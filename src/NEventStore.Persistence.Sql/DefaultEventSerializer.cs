using System;
using System.Collections.Generic;
using NEventStore.Serialization;

namespace NEventStore.Persistence.Sql
{
	/// <summary>
	/// Default implementation of <see cref="ISerializeEvents"/>.
	/// </summary>
	public class DefaultEventSerializer : ISerializeEvents
	{
		private readonly ISerialize _serializer;

		/// <summary>
		/// Initializes a new instance of the <see cref="DefaultEventSerializer"/> class.
		/// </summary>
		public DefaultEventSerializer(ISerialize serializer)
		{
			_serializer = serializer;
		}

		/// <inheritdoc/>
		public byte[] SerializeEventMessages(IReadOnlyList<EventMessage> eventMessages)
		{
			return _serializer.Serialize(eventMessages);
		}

		/// <inheritdoc/>
		public ICollection<EventMessage> DeserializeEventMessages(byte[] input, string bucketId, string streamId,
			int streamRevision, Guid commitId,
			int commitSequence, DateTime commitStamp, long checkpoint, IReadOnlyDictionary<string, object> headers)
		{
			return _serializer.Deserialize<List<EventMessage>>(input);
		}
	}
}