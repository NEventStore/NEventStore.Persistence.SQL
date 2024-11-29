using System;

namespace NEventStore.Persistence.Sql
{
	/// <summary>
	/// Configuration errors exception.
	/// </summary>
	[Serializable]
	public class ConfigurationErrorsException : Exception
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="ConfigurationErrorsException"/> class.
		/// </summary>
		public ConfigurationErrorsException() { }
		/// <summary>
		/// Initializes a new instance of the <see cref="ConfigurationErrorsException"/> class.
		/// </summary>
		public ConfigurationErrorsException(string message) : base(message) { }
		/// <summary>
		/// Initializes a new instance of the <see cref="ConfigurationErrorsException"/> class.
		/// </summary>
		public ConfigurationErrorsException(string message, Exception inner) : base(message, inner) { }
		/// <summary>
		/// Initializes a new instance of the <see cref="ConfigurationErrorsException"/> class.
		/// </summary>
		protected ConfigurationErrorsException(
		  System.Runtime.Serialization.SerializationInfo info,
		  System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
	}
}
