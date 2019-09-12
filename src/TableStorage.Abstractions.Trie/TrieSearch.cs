using System;
using System.Collections.Async;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using TableStorage.Abstractions.Store;
using TableStorage.Abstractions.TableEntityConverters;

namespace TableStorage.Abstractions.Trie
{
	/// <summary>
	/// Manages a single index using a trie-like strategy to accomplish "begins with" searching in Azure Table Storage
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <seealso cref="TableStorage.Abstractions.Trie.ITrieSearch{T}" />
	public class TrieSearch<T> : ITrieSearch<T> where T : class, new()
	{
		public const int DefaultMaxNumberConnections = 100;

		private readonly TrieSearchOptions _options;
		private readonly PropertyInfo _rowKeyProperty;
		private readonly TableStore<DynamicTableEntity> _tableStore;

		/// <summary>
		/// Initializes a new instance of the <see cref="TrieSearch{T}"/> class.
		/// </summary>
		/// <param name="indexName">Name of the index.  Index name must follow all Azure Table Storage rules for table name</param>
		/// <param name="storageConnectionString">The Azure storage connection string.</param>
		/// <param name="rowKeyProperty">The row key property.</param>
		/// <param name="maxNumberOfConnections">The maximum number of connections.  Analysis using test data showed 100 connections to 
		/// be the sweet spot, but you are encouraged to test this yourself.  Set this to null to use the default value set by <see cref="ServicePointManager"/>
		/// </param>
		/// <param name="options">The options.</param>
		/// <exception cref="System.ArgumentException">
		/// indexName
		/// or
		/// MinimumIndexLength
		/// or
		/// RowKey must be string
		/// or
		/// maxNumberOfConnections
		/// </exception>
		public TrieSearch(string indexName, string storageConnectionString, Expression<Func<T, object>> rowKeyProperty, int? maxNumberOfConnections = DefaultMaxNumberConnections, TrieSearchOptions options = null)
		{
			if (!Regex.IsMatch(indexName, "^[A-Za-z][A-Za-z0-9]{2,62}$")) throw new ArgumentException(nameof(indexName));
			_options = options ?? new TrieSearchOptions();

			if(_options.MinimumIndexLength > _options.MaximumIndexLength)
				throw new ArgumentException(nameof(options.MinimumIndexLength));

			var propertyName = GetPropertyNameFromExpression(rowKeyProperty);
			_rowKeyProperty = typeof(T).GetProperty(propertyName);
			if (_rowKeyProperty.PropertyType != typeof(string))
				throw new ArgumentException("RowKey must be string");

			if (maxNumberOfConnections.HasValue)
			{
				if(maxNumberOfConnections < 1)
					throw new ArgumentException(nameof(maxNumberOfConnections));

				var account = CloudStorageAccount.Parse(storageConnectionString);
				var tableServicePoint = ServicePointManager.FindServicePoint(account.TableEndpoint);
				if (tableServicePoint.ConnectionLimit != maxNumberOfConnections)
					tableServicePoint.ConnectionLimit = maxNumberOfConnections.Value;
			}

            _tableStore =
                new TableStore<DynamicTableEntity>(indexName, storageConnectionString, new Models.TableStorageOptions { Retries = _options.NumberOfRetries, RetryWaitTimeInSeconds = _options.RetryWaitTimeInSeconds });
		}

		/// <inheritdoc />
		public Task IndexAsync(T data, Func<T, string> getIndex)
		{
			return IndexAsync(data, getIndex(data));
		}

		/// <inheritdoc />
		public Task IndexAsync(T data, string index)
		{
			if (string.IsNullOrEmpty(index))
				throw new ArgumentException(nameof(index));

			if (index.Length > 255)
				throw new ArgumentException(nameof(index));

			if (index.Length > _options.MaximumIndexLength && _options.ThrowOnMaximumIndexLengthExceeded)
			{
				throw new ArgumentException(nameof(index));
			}

			if (!_options.IsCaseSensitive)
				index = index.ToLowerInvariant();

			if (index.Length < _options.MinimumIndexLength)
			{
				if (_options.ThrowOnMinimumIndexLengthNotMet)
					throw new ArgumentException(nameof(index));

				return Task.FromResult(0);
			}

			var rowKeyValue = GetRowKeyValue(data).ToString();

			var terms = CreateIndexTerms(index);

			return terms.ParallelForEachAsync(async term =>
			{
				try
				{
					var entry = new TrieEntry<T> {Data = data, Index = term, Key = GetRowKeyValue(data).ToString()};

					await _tableStore.InsertAsync(entry.ToTableEntity(term, rowKeyValue, x => x.Index, x => x.Key))
						.ConfigureAwait(false);
				}
				catch (StorageException e) when (e.RequestInformation.HttpStatusCode == 409)
				{
					if (_options.ThrowOnConflict)
						throw;
				}
			});
		}

		/// <inheritdoc />
		public async Task ReindexAsync(T data, string oldIndex, string newIndex)
		{
			if (newIndex.Length > 255)
				throw new ArgumentException(nameof(newIndex));

			await DeleteAsync(oldIndex, data).ConfigureAwait(false);
			await IndexAsync(data, newIndex).ConfigureAwait(false);
		}

		/// <inheritdoc />
		public async Task<IEnumerable<T>> FindAsync(string term, int pageSize = 10)
		{
			if (string.IsNullOrEmpty(term))
				throw new ArgumentException(nameof(term));

			if (pageSize < 0)
				throw new ArgumentException(nameof(pageSize));

			if (!_options.IsCaseSensitive)
				term = term.ToLowerInvariant();

			var results = await _tableStore.GetByPartitionKeyPagedAsync(term, pageSize).ConfigureAwait(false);
			return results.Items.Select(
				item => item.FromTableEntity<TrieEntry<T>, string, string>(x => x.Index, x => x.Key).Data);
		}

		/// <inheritdoc />
		public Task<int> GetIndexSizeAsync()
		{
			return _tableStore.GetRecordCountAsync();
		}

		/// <inheritdoc />
		public async Task<IEnumerable<T>> GetAllIndexEntriesAsync()
		{
			return (await _tableStore.GetAllRecordsAsync().ConfigureAwait(false)).Select(
				item => item.FromTableEntity<TrieEntry<T>, string, string>(x => x.Index, x => x.Key).Data);
		}

		/// <inheritdoc />
		public Task DropIndexAsync()
		{
			return _tableStore.DeleteTableAsync();
		}

		/// <inheritdoc />
		public Task DeleteAsync(string index, string key)
		{
			if (string.IsNullOrEmpty(index)) throw new ArgumentException(nameof(index));

			if (string.IsNullOrEmpty(key)) throw new ArgumentException(nameof(key));

			if (!_options.IsCaseSensitive) index = index.ToLowerInvariant();

			var terms = CreateIndexTerms(index, false);

			return terms.ParallelForEachAsync(async term =>
			{
				try
				{
					await _tableStore.DeleteAsync(new DynamicTableEntity(term, key) {ETag = "*"}).ConfigureAwait(false);
				}
				catch (StorageException e) when (e.RequestInformation.HttpStatusCode == 404)
				{
					// do nothing
				}
			});
		}

		/// <inheritdoc />
		public Task DeleteAsync(string index, T data)
		{
			var key = GetRowKeyValue(data).ToString();
			return DeleteAsync(index, key);
		}

		/// <summary>
		///     Creates the index terms.
		/// </summary>
		/// <param name="index">The index.</param>
		/// <param name="toMax">when true, only creates terms up to max</param>
		/// <returns>System.String[].</returns>
		private string[] CreateIndexTerms(string index, bool toMax = true)
		{
			var length = index.Length;

			if (toMax)
				if (length > _options.MaximumIndexLength)
					length = _options.MaximumIndexLength;

			var terms = new string[length - _options.MinimumIndexLength + 1];

			for (var i = 0; i < terms.Length; i++) terms[i] = index.Substring(0, i + _options.MinimumIndexLength);

			return terms;
		}

		/// <summary>
		///     Gets the row key value.
		/// </summary>
		/// <param name="data">The data.</param>
		/// <returns>System.Object.</returns>
		private object GetRowKeyValue(T data)
		{
			return _rowKeyProperty.GetValue(data);
		}

		private string GetPropertyNameFromExpression(Expression<Func<T, object>> exp)
		{
			string name;

			var body = exp.Body as MemberExpression;
			if (body == null)
			{
				var ubody = (UnaryExpression) exp.Body;
				body = ubody.Operand as MemberExpression;
				name = body.Member.Name;
			}
			else
			{
				name = body.Member.Name;
			}

			return name;
		}
	}
}