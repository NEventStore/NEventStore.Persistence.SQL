namespace NEventStore.Persistence.Sql
{
	using System;
	using System.Security.Cryptography;
	using System.Text;

	/// <summary>
	/// Represents a SHA1 stream ID hasher.
	/// </summary>
	public class Sha1StreamIdHasher : IStreamIdHasher
	{
		/// <inheritdoc/>
		public string GetHash(string streamId)
		{
			byte[] hashBytes = SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(streamId));
			return BitConverter.ToString(hashBytes).Replace("-", "");
		}
	}
}