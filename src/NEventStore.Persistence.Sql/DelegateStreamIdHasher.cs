namespace NEventStore.Persistence.Sql
{
	using System;

	/// <summary>
	/// Defines a method to generate a hash of a stream ID.
	/// </summary>
	public class DelegateStreamIdHasher : IStreamIdHasher
	{
		private readonly Func<string, string> _getHash;

		/// <summary>
		/// Initializes a new instance of the <see cref="DelegateStreamIdHasher"/> class.
		/// </summary>
		/// <exception cref="ArgumentNullException"></exception>
		public DelegateStreamIdHasher(Func<string, string> getHash)
		{
			_getHash = getHash ?? throw new ArgumentNullException(nameof(getHash));
		}
		/// <inheritdoc/>
		public string GetHash(string streamId)
		{
			return _getHash(streamId);
		}
	}
}