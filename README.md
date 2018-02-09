# TableStorage.Abstractions.Trie
This project creates an "index" in Azure Table Storage using a [trie](https://en.wikipedia.org/wiki/Trie)-like structure to enable type-ahead style "begins with" search scenarios, e.g. "Jo" will yield "John Smith, Joe Taylor, Josephine Baker."

The index takes full advantage of the scalability of Azure Table Storage by partitioning each term.  This allows for fast query time and not-great-but-not-too-terrible indexing time (benchmarks provided below), at the cost of space.  But let's face it, space in Azure Table Storage is _extremely_ cheap.  At time of this writing, ATS can be obtained for as little as $0.07 per gigabyte per month.

## Important Notes ##
1. This project runs terrible under the Azure Storage Emulator; probably because the emulator uses Sql Server LocalDB.  If you're curious how well this project will work for you, try it using the real thing.
2. This library uses an [underlying library](https://github.com/Tazmainiandevil/TableStorage.Abstractions) to access Azure Table Storage.  That library checks for table existence and creates it if it does not exist.  While convenient, this has a performance penalty, so you'll want to cache instances of `TrieSearch` and `MultiIndexTrieSearch`, or implement them as singletons if you plan to use them heavily.

## Features ##
* Works with POCOs (plain old CLR objects)
* Ability to index, reindex and remove from index your POCOs
* Ability to query the index
* `MultiIndexTrieSearch` manages multiple indexes, and allows you to query all of them at once and de-dupe the results.  For instance, suppose you want to index your customers by full name, last name, and email.  Let's use my name as an example (Giovanni Galbo) and suppose my email is gio@test.com.  Imagine we query "g".  This would yield results from all 3 indexes, but we'd only want one result to be presented.  This is accomplished by de-duping.  Let's use one more example, "Gina Smith."  Typing a "g" would yield Giovanni Galbo and Gina Smith.  Typing an "s" would yield just Gina Smith. The first results were 1 record from me (De-duped from 3 results) plus one for Gina, while the second result was returned from the last name index.  The beauty of the multi-index is that it's all handled for you.
* Ability to set minimum index lengths.  For instance, if you don't want type ahead for less than 3 characters, you can reduce the size of your index by limiting it to 3 characters.
* Ability to set a maximum index

## Usage
### Create a single index
```charp
_fullNameIndex = new TrieSearch<Customer>("trieUnitTestFullNameIndex", "UseDevelopmentStorage=true", c => c.Id);
```
Here we set the name of the index (which translates to an Azure Table Storage table name), connection string, and the identifier, which translates to the row key in Azure Table Storage.

### Creating a multi index
```charp
_multiIndexSearch = new MultiIndexTrieSearch<Customer>(new Dictionary<ITrieSearch<Customer>, Func<Customer, string>>
			{
				[_fullNameIndex] = c => c.FirstName + " " + c.LastName,
				[_lastNameIndex] = c => c.LastName,
				[_emailIndex] = c => c.Email
			});
```
Here we provide the multi index with a dictionary.  The dictionary keys are your single indexes, and their values are expressions on how to construct the index.  The first is for full name search, the second just for last name, and finally email.

### The de-dupe function
Currently you need to tell the multi index how to de-dupe the search results.  A future release may do this automatically for you, but for now you'll need to provide one.  Here is an example:

```charp
_dedupeFunction = customers =>
			{
				var uniqueIds = customers.Select(c => c.Id).Distinct();
				var uniqueCustomers = new List<Customer>();
				foreach (var id in uniqueIds)
				{
					uniqueCustomers.Add(customers.First(c => c.Id == id));
				}

				return uniqueCustomers;
			};
```
## Benchmarks 
Note 1: These benchmarks were performed on an 5960X 8 core intel CPU with 64GB of RAM.  The netork connection is 400mb download and 40mb upload.  Results will vary with hardware and size of payload.  

Note 2: One of the goals of these benchmarks was to find the best number for max connections, because the default number used by the Azure Table Storage SDK yields [terrible results](http://tk.azurewebsites.net/2012/12/10/greatly-increase-the-performance-of-azure-storage-cloudblobclient/).

Note 3: These tests were run in an actual instance of Azure Table Storage.  This project runs terrible under the Azure Storage Emulator; probably because the emulator uses Sql Server LocalDB.
