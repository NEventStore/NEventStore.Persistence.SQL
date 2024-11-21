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

        public DefaultEventSerializer(ISerialize serializer)
        {
            _serializer = serializer;
        }

        public ICollection<EventMessage> DeserializeEventMessages(byte[] input, string bucketId, string streamId,
            int streamRevision, Guid commitId,
            int commitSequence, DateTime commitStamp, long checkpoint, IReadOnlyDictionary<string, object> headers)
        {
            return _serializer.Deserialize<List<EventMessage>>(input);
        }
    }
}