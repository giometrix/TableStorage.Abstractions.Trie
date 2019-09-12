namespace TableStorage.Abstractions.Trie.Core
{
	public class TrieSearchOptions
	{
		/// <summary>
		///     The number of retries upon a network failure with Azure Table Storage
		/// </summary>
		/// <value>The number of retries.</value>
		public int NumberOfRetries { get; set; } = 3;

		/// <summary>
		///     The number of seconds in between retries
		/// </summary>
		/// <see cref="NumberOfRetries" />
		/// <value>The retry wait time in seconds.</value>
		public int RetryWaitTimeInSeconds { get; set; } = 5;

		/// <summary>
		///     The maximum index length.  Set a maximum index length to save space.
		/// </summary>
		/// <remarks>The maximum length allowed is 255 due to Azure Table Storage restrictions.</remarks>
		/// <value>The maximum length of the index.</value>
		public byte MaximumIndexLength { get; set; } = 20;

		/// <summary>
		///     The minimum index length.  Set a minumum to save space.  Use a minimum if you do not attempt to search until a
		///     minumum set of characters are typed before searching
		/// </summary>
		/// <value>The minimum length of the index.</value>
		public byte MinimumIndexLength { get; set; } = 1;

		/// <summary>
		///     If set to true, an exception will be thrown if indexing encounters a 409 error from azure.
		/// </summary>
		public bool ThrowOnConflict { get; set; } = false;

		/// <summary>
		///     If set to true, an exception will be thrown if the index is too short, else the entity will simply not be indexed
		/// </summary>
		public bool ThrowOnMinimumIndexLengthNotMet { get; set; } = false;

		/// <summary>
		///     If set to true, an exception will be thrown if the index is too long, else the entity will simply not be indexed
		///     beyond the max
		/// </summary>
		public bool ThrowOnMaximumIndexLengthExceeded { get; set; } = false;

		/// <summary>
		///     Set to true for case sensitive search
		/// </summary>
		/// <value><c>true</c> if this instance is case sensitive; otherwise, <c>false</c>.</value>
		public bool IsCaseSensitive { get; set; } = false;
	}
}