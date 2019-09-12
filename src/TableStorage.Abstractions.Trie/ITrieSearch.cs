using System;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace TableStorage.Abstractions.Trie.Core
{
	public interface ITrieSearch<T> where T : new()
	{
		/// <summary>
		/// Indexes the data.
		/// </summary>
		/// <param name="data">The data.</param>
		/// <param name="getIndex">Index of the get.</param>
		/// <returns>Task.</returns>
		Task IndexAsync(T data, Func<T, string> getIndex);

		/// <summary>
		/// Indexes the entity.
		/// </summary>
		/// <param name="data">The data.</param>
		/// <param name="index">The index.</param>
		/// <returns>Task.</returns>
		Task IndexAsync(T data, string index);

		/// <summary>
		/// Reindexes the entity.
		/// </summary>
		/// <param name="data">The data.</param>
		/// <param name="oldIndex">The old index.</param>
		/// <param name="newIndex">The new index.</param>
		/// <returns>Task.</returns>
		Task ReindexAsync(T data, string oldIndex, string newIndex);
		
		/// <summary>
		/// Finds entities matching the term using a 'begins with' search
		/// </summary>
		/// <param name="term">The search term.</param>
		/// <param name="pageSize">Size of the page.</param>
		/// <returns>Task&lt;IEnumerable&lt;T&gt;&gt;.</returns>
		Task<IEnumerable<T>> FindAsync(string term, int pageSize = 10);

		/// <summary>
		/// Gets the index size.
		/// </summary>
		/// <returns>Task&lt;System.Int32&gt;.</returns>
		/// <remarks>Do not use this operation for regular requests, since it will scan through many partitions</remarks>
		Task<int> GetIndexSizeAsync();

		/// <summary>
		/// Gets all index entries.
		/// </summary>
		/// <returns>Task&lt;System.Int32&gt;.</returns>
		/// <remarks>Do not use this operation for regular requests, since it will scan through many partitions</remarks>
		Task<IEnumerable<T>> GetAllIndexEntriesAsync();

		/// <summary>
		/// Drops the index
		/// </summary>
		/// <returns>Task</returns>
		Task DropIndexAsync();

		/// <summary>
		/// Deletes the index entry.
		/// </summary>
		/// <param name="index">The index</param>
		/// <param name="key">The entity key.</param>
		/// <returns>Task</returns>
		Task DeleteAsync(string index, string key);
		
		/// <summary>
		/// Deletes the index entry.
		/// </summary>
		/// <param name="index">The index.</param>
		/// <param name="data">The indexex data.</param>
		/// <returns>Task</returns>
		Task DeleteAsync(string index, T data);
	}
}