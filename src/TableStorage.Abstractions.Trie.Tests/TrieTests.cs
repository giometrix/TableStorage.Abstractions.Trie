using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TableStorage.Abstractions.Trie.Tests
{
    [TestClass]
    public class TrieTests
    {
        private TrieSearch<Customer> _fullNameIndex;
        private TrieSearch<Customer> _lastNameIndex;
        private TrieSearch<Customer> _emailIndex;
        private MultiIndexTrieSearch<Customer> _multiIndexSearch;
        private Func<IEnumerable<Customer>, IEnumerable<Customer>> _dedupeFunction;

        [TestInitialize]
        public async Task Init()
        {
            _fullNameIndex = new TrieSearch<Customer>("trieUnitTestFullNameIndex", "UseDevelopmentStorage=true", c => c.Id, 10);
            _lastNameIndex = new TrieSearch<Customer>("trieUnitTestLastNameIndex", "UseDevelopmentStorage=true", c => c.Id, 10);
            _emailIndex = new TrieSearch<Customer>("trieUnitTestEmailIndex", "UseDevelopmentStorage=true", c => c.Id, 10,
                new TrieSearchOptions
                {
                    MinimumIndexLength = 3
                });
            _multiIndexSearch = new MultiIndexTrieSearch<Customer>(new Dictionary<ITrieSearch<Customer>, Func<Customer, string>>
            {
                [_fullNameIndex] = c => c.FirstName + " " + c.LastName,
                [_lastNameIndex] = c => c.LastName,
                [_emailIndex] = c => c.Email
            });

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

            await CreateTestData();
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await _multiIndexSearch.DropIndexAsync();
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void minimum_length_cannot_be_more_than_maximum_length()
        {
            var options = new TrieSearchOptions
            {
                MinimumIndexLength = 5,
                MaximumIndexLength = 2
            };

            new TrieSearch<Customer>("test", "test", c => c.Id, options: options);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void index_name_cannot_contain_special_characters()
        {
            var options = new TrieSearchOptions
            {
                MinimumIndexLength = 5,
                MaximumIndexLength = 2
            };

            new TrieSearch<Customer>("#hello?!", "test", c => c.Id, options: options);
        }

        [TestMethod]
        public async Task search_index_with_minimum_of_one_character_returns_expected_results()
        {
            var results = await _fullNameIndex.FindAsync("j");
            Assert.AreEqual(2, results.Count());
            Assert.IsTrue(results.Any(c => c.Id == "42"));
            Assert.IsTrue(results.Any(c => c.Id == "43"));
            Assert.IsFalse(results.Any(c => c.Id == "44"));
        }

        [TestMethod]
        public async Task search_index_honors_page_size()
        {
            var results = await _fullNameIndex.FindAsync("j", 1);
            Assert.AreEqual(1, results.Count());
        }

        [TestMethod]
        public async Task search_index_with_minimum_of_three_characters_returns_no_results_when_searching_less_than_three_characters()
        {
            var results = await _emailIndex.FindAsync("j");
            Assert.AreEqual(0, results.Count());
        }

        [TestMethod]
        public async Task search_index_with_minimum_of_three_characters_returns_expected_results_when_searching_more_than_three_characters()
        {
            var results = await _emailIndex.FindAsync("jsmit");
            Assert.AreEqual(1, results.Count());
        }

        [TestMethod]
        public async Task add_entry_to_index_succeeds()
        {
            var customer = new Customer
            {
                Email = "billg@test.com",
                FirstName = "Bill",
                LastName = "Gates",
                Id = "2000"
            };

            await _fullNameIndex.IndexAsync(customer, "Bill Gates");
            var results = await _fullNameIndex.FindAsync("Bill");
            Assert.AreEqual(1, results.Count());
            Assert.IsTrue(results.Any(c => c.Id == "2000"));
        }

        [TestMethod]
        public async Task add_entry_to_index_succeeds_using_expression_syntax()
        {
            var customer = new Customer
            {
                Email = "billg@test.com",
                FirstName = "Bill",
                LastName = "Gates",
                Id = "2000"
            };

            await _fullNameIndex.IndexAsync(customer, c => c.FirstName + " " + c.LastName);
            var results = await _fullNameIndex.FindAsync("Bill");
            Assert.AreEqual(1, results.Count());
            Assert.IsTrue(results.Any(c => c.Id == "2000"));
        }


        [TestMethod]
        public async Task add_entry_to_index_fails_silently_when_minimum_length_not_met_and_exceptions_are_disabled()
        {
            var customer = new Customer
            {
                Email = "billg@test.com",
                FirstName = "Bill",
                LastName = "Gates",
                Id = "2000"
            };

            await _emailIndex.IndexAsync(customer, "b");
            var results = await _fullNameIndex.FindAsync("Bill");
            Assert.AreEqual(0, results.Count());
        }

        [TestMethod]
        public async Task add_entry_to_index_throws_exception_when_minumum_not_met_and_exceptions_are_enabled()
        {
            var customer = new Customer
            {
                Email = "billg@test.com",
                FirstName = "Bill",
                LastName = "Gates",
                Id = "2000"
            };

            var failIndex = new TrieSearch<Customer>("trieUnitTestsFailTest", "UseDevelopmentStorage=true", c => c.Id, options:
                new TrieSearchOptions { ThrowOnMinimumIndexLengthNotMet = true, MinimumIndexLength = 3 });

            bool exceptionThrown = false;
            try
            {
                await failIndex.IndexAsync(customer, "b");
            }
            catch (ArgumentException e)
            {
                exceptionThrown = true;
            }
            finally
            {
                await failIndex.DropIndexAsync();
            }

            Assert.IsTrue(exceptionThrown);
        }

        [TestMethod]
        public async Task add_entry_to_index_beyond_maximum_length_does_not_get_indexed_beyond_maximum_if_exceptions_are_disabled()
        {
            var customer = new Customer
            {
                Email = "billg@test.com",
                FirstName = "Bill",
                LastName = "Gates",
                Id = "2000"
            };

            var index = new TrieSearch<Customer>("trieUnitTestsMaxLenTest", "UseDevelopmentStorage=true", c => c.Id, options:
                new TrieSearchOptions { MaximumIndexLength = 3 });

            try
            {
                await index.IndexAsync(customer, "Gates");

                var results = await index.FindAsync("gat");
                Assert.AreEqual(1, results.Count());

                results = await index.FindAsync("gates");
                Assert.AreEqual(0, results.Count());
            }
            finally
            {
                await index.DropIndexAsync();
            }
        }

        [TestMethod]
        public async Task add_entry_to_index_beyond_maximum_length_throws_exception_if_exceptions_enabled()
        {
            var customer = new Customer
            {
                Email = "billg@test.com",
                FirstName = "Bill",
                LastName = "Gates",
                Id = "2000"
            };

            var index = new TrieSearch<Customer>("trieUnitTestsMaxLenTest", "UseDevelopmentStorage=true", c => c.Id, options:
                new TrieSearchOptions { MaximumIndexLength = 3, ThrowOnMaximumIndexLengthExceeded = true });

            bool exceptionThrown = false;
            try
            {
                await index.IndexAsync(customer, "Gates");
            }
            catch (ArgumentException ex)
            {
                exceptionThrown = true;
            }
            finally
            {
                await index.DropIndexAsync();
            }

            Assert.IsTrue(exceptionThrown);
        }

        [TestMethod]
        public async Task add_entry_to_index_that_is_in_conflict_fails_silently_if_exceptions_disabled()
        {
            var customer = new Customer
            {
                FirstName = "John",
                LastName = "Smith",
                Email = "jsmith@test.com",
                Id = "42",
                Phone = "917-555-1234"
            };

            await _fullNameIndex.IndexAsync(customer, "John Smith");
        }

        [TestMethod]
        public async Task add_entry_to_index_that_is_in_conflict_throws_exception_if_exception_enabled()
        {
            var customer = new Customer
            {
                Email = "billg@test.com",
                FirstName = "Bill",
                LastName = "Gates",
                Id = "2000"
            };

            var index = new TrieSearch<Customer>("trieUnitTestsConflictTest", "UseDevelopmentStorage=true", c => c.Id, options:
                new TrieSearchOptions { ThrowOnConflict = true });

            bool exceptionThrown = false;
            try
            {
                await index.IndexAsync(customer, "Gates");
                await index.IndexAsync(customer, "Gates");
            }
            catch (Exception ex)
            {
                exceptionThrown = true;
            }
            finally
            {
                await index.DropIndexAsync();
            }

            Assert.IsTrue(exceptionThrown);
        }

        [TestMethod]
        public async Task reindex_succeeds()
        {
            var customer = new Customer
            {
                FirstName = "John",
                LastName = "Smith",
                Email = "jsmith@test.com",
                Id = "42",
                Phone = "917-555-1234"
            };

            await _emailIndex.ReindexAsync(customer, "jsmith@test.com", "johnsmith@test.com");

            var results = await _emailIndex.FindAsync("jsmith@test.com");
            Assert.AreEqual(0, results.Count());
            results = await _emailIndex.FindAsync("johnsmith@test.com");
            Assert.AreEqual(1, results.Count());
        }

        [TestMethod]
        public async Task delete_from_index_succeeds()
        {
            var results = await _emailIndex.FindAsync("jsmith@test.com");
            Assert.AreEqual(1, results.Count());

            await _emailIndex.DeleteAsync("jsmith@test.com", "42");

            results = await _emailIndex.FindAsync("jsmith@test.com");
            Assert.AreEqual(0, results.Count());
        }

        [TestMethod]
        public async Task delete_does_not_fail_if_key_does_not_exist()
        {
            await _emailIndex.DeleteAsync("aaah@test.com", "999");
        }

        [TestMethod]
        public async Task multi_index_search_returns_results_from_multiple_indexes()
        {
            var results = await _multiIndexSearch.FindAsync("j", _dedupeFunction);
            Assert.AreEqual(3, results.Count());
        }

        [TestMethod]
        public async Task multi_index_search_honors_page_size()
        {
            var results = await _multiIndexSearch.FindAsync("j", _dedupeFunction, pageSize: 2);
            Assert.AreEqual(2, results.Count());
        }

        [TestMethod]
        public async Task multi_index_search_returns_deduped_results()
        {
            var results = await _multiIndexSearch.FindAsync("smith", _dedupeFunction);
            Assert.AreEqual(1, results.Count());
        }

        [TestMethod]
        public async Task add_entry_to_multi_index_adds_to_all_indexes()
        {
            var customer = new Customer
            {
                FirstName = "Benjamin",
                LastName = "Franklin",
                Id = "22",
                Email = "BenF@test.com",
                Phone = "111-111-1111"
            };
            await _multiIndexSearch.IndexAsync(customer);

            var results1 = await _fullNameIndex.FindAsync("Benjamin Franklin");
            var results2 = await _lastNameIndex.FindAsync("Franklin");
            var results3 = await _emailIndex.FindAsync("BenF@test.com");

            Assert.AreEqual(1, results1.Count());
            Assert.AreEqual(1, results2.Count());
            Assert.AreEqual(1, results3.Count());
        }

        [TestMethod]
        public async Task delete_entry_from_multi_index_removes_from_all_indexes()
        {
            var customer = new Customer
            {
                FirstName = "John",
                LastName = "Smith",
                Email = "jsmith@test.com",
                Id = "42",
                Phone = "917-555-1234"
            };

            await _multiIndexSearch.DeleteAsync(customer);

            var results1 = await _fullNameIndex.FindAsync("John Smith");
            var results2 = await _lastNameIndex.FindAsync("Smith");
            var results3 = await _emailIndex.FindAsync("jsmith@test.com");

            Assert.AreEqual(0, results1.Count());
            Assert.AreEqual(0, results2.Count());
            Assert.AreEqual(0, results3.Count());
        }

        [TestMethod]
        public async Task reindex_entry_in_multi_index_reindexes_in_all_indexes()
        {
            var customer = new Customer
            {
                FirstName = "John",
                LastName = "Smith",
                Email = "jsmith@test.com",
                Id = "42",
                Phone = "917-555-1234"
            };

            var newCustomer = new Customer
            {
                FirstName = "John",
                LastName = "Smith",
                Email = "jsmith@test.com",
                Id = "42",
                Phone = "917-555-9999"
            };

            await _multiIndexSearch.ReindexAsync(customer, newCustomer);

            var results1 = await _fullNameIndex.FindAsync("John Smith");
            var results2 = await _lastNameIndex.FindAsync("Smith");
            var results3 = await _emailIndex.FindAsync("jsmith@test.com");

            Assert.AreEqual("917-555-9999", results1.Single().Phone);
            Assert.AreEqual("917-555-9999", results2.Single().Phone);
            Assert.AreEqual("917-555-9999", results3.Single().Phone);
        }

        private async Task CreateTestData()
        {
            var customers = new List<Customer>
            {
                new Customer
                {
                    FirstName = "John",
                    LastName = "Smith",
                    Email = "jsmith@test.com",
                    Id = "42",
                    Phone = "917-555-1234"
                },
                new Customer
                {
                    FirstName = "Jenny",
                    LastName = "Jones",
                    Email = "jj@test.com",
                    Id = "43",
                    Phone = "212-555-1234"
                }
                ,
                new Customer
                {
                    FirstName = "Tom",
                    LastName = "Jones",
                    Email = "zeus@test.com",
                    Id = "44",
                    Phone = "213-555-1234"
                }
            };

            foreach (var customer in customers)
            {
                await _multiIndexSearch.IndexAsync(customer);
            }
        }
    }
}