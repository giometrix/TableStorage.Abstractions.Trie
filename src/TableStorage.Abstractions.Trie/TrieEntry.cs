﻿namespace TableStorage.Abstractions.Trie
{
	internal class TrieEntry<T>
	{
		public string Index { get; set; }
		public string Key { get; set; }
		public T Data { get; set; }
	}
}