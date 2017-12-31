﻿// TestCacheManager.cs
//
// Author:
//       Ricky Curtice <ricky@rwcproductions.com>
//
// Copyright (c) 2017 Richard Curtice
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using System.IO;
using System.Text;
using InWorldz.Data.Assets.Stratus;
using LibWhipLru.Cache;
using NUnit.Framework;

#pragma warning disable RECS0026 // Possible unassigned object created by 'new'

namespace LibWhipLruTests.Cache {
	[TestFixture]
	public class TestCacheManager {
		private readonly string DATABASE_FOLDER_PATH = $"{TestContext.CurrentContext.TestDirectory}/test_cm";
		private const ulong DATABASE_MAX_SIZE_BYTES = uint.MaxValue/*Min value to get tests to run*/;
		private readonly string WRITE_CACHE_FILE_PATH = $"{TestContext.CurrentContext.TestDirectory}/test_cm.whipwcache";
		private const uint WRITE_CACHE_MAX_RECORD_COUNT = 8;
		private readonly byte[] WRITE_CACHE_MAGIC_NUMBER = Encoding.ASCII.GetBytes("WHIPLRU1");

		[SetUp]
		public void BeforeEveryTest() {
			Directory.CreateDirectory(DATABASE_FOLDER_PATH);
		}

		[TearDown]
		public void CleanupAfterEveryTest() {
			File.Delete(WRITE_CACHE_FILE_PATH);
			Directory.Delete(DATABASE_FOLDER_PATH, true);
		}

		#region Ctor

		[Test]
		public void TestCtorDoesNotThrow() {
			Assert.DoesNotThrow(() => new CacheManager(
				DATABASE_FOLDER_PATH,
				DATABASE_MAX_SIZE_BYTES,
				WRITE_CACHE_FILE_PATH,
				WRITE_CACHE_MAX_RECORD_COUNT,
				TimeSpan.FromMinutes(2)
			));
		}

		[Test]
		public void TestCtorDBPathBlankThrowsArgNullException() {
			Assert.Throws<ArgumentNullException>(() => new CacheManager(
				"",
				DATABASE_MAX_SIZE_BYTES,
				WRITE_CACHE_FILE_PATH,
				WRITE_CACHE_MAX_RECORD_COUNT,
				TimeSpan.FromMinutes(2)
			));
		}

		[Test]
		public void TestCtorDBPathNullThrowsArgNullException() {
			Assert.Throws<ArgumentNullException>(() => new CacheManager(
				null,
				DATABASE_MAX_SIZE_BYTES,
				WRITE_CACHE_FILE_PATH,
				WRITE_CACHE_MAX_RECORD_COUNT,
				TimeSpan.FromMinutes(2)
			));
		}

		[Test]
		public void TestCtorCreatesWriteCacheFile() {
			new CacheManager(
				DATABASE_FOLDER_PATH,
				DATABASE_MAX_SIZE_BYTES,
				WRITE_CACHE_FILE_PATH,
				WRITE_CACHE_MAX_RECORD_COUNT,
				TimeSpan.FromMinutes(2)
			);

			FileAssert.Exists(WRITE_CACHE_FILE_PATH);
		}

		[Test]
		public void TestCtorCreatesWriteCacheFileWithCorrectMagicNumber() {
			new CacheManager(
				DATABASE_FOLDER_PATH,
				DATABASE_MAX_SIZE_BYTES,
				WRITE_CACHE_FILE_PATH,
				WRITE_CACHE_MAX_RECORD_COUNT,
				TimeSpan.FromMinutes(2)
			);

			var buffer = new byte[WRITE_CACHE_MAGIC_NUMBER.Length];
			using (var fs = new FileStream(WRITE_CACHE_FILE_PATH, FileMode.Open, FileAccess.Read)) {
				fs.Read(buffer, 0, buffer.Length);
				fs.Close();
			}

			Assert.AreEqual(WRITE_CACHE_MAGIC_NUMBER, buffer);
		}

		[Test]
		public void TestCtorCreatesWriteCacheFileWithCorrectRecordCount() {
			new CacheManager(
				DATABASE_FOLDER_PATH,
				DATABASE_MAX_SIZE_BYTES,
				WRITE_CACHE_FILE_PATH,
				WRITE_CACHE_MAX_RECORD_COUNT,
				TimeSpan.FromMinutes(2)
			);

			var dataLength = new FileInfo(WRITE_CACHE_FILE_PATH).Length - WRITE_CACHE_MAGIC_NUMBER.Length;
			var recordCount = dataLength / IdWriteCacheNode.BYTE_SIZE;

			Assert.AreEqual(WRITE_CACHE_MAX_RECORD_COUNT, recordCount);
		}

		[Test]
		public void TestCtorCreatesWriteCacheFileWithRecordsAllAvailable() {
			new CacheManager(
				DATABASE_FOLDER_PATH,
				DATABASE_MAX_SIZE_BYTES,
				WRITE_CACHE_FILE_PATH,
				WRITE_CACHE_MAX_RECORD_COUNT,
				TimeSpan.FromMinutes(2)
			);

			using (var fs = new FileStream(WRITE_CACHE_FILE_PATH, FileMode.Open, FileAccess.Read)) {
				try {
					// Skip the header
					fs.Seek(WRITE_CACHE_MAGIC_NUMBER.Length, SeekOrigin.Begin);

					// Check each row.
					for (var recordIndex = 0; recordIndex < WRITE_CACHE_MAX_RECORD_COUNT; ++recordIndex) {
						var buffer = new byte[IdWriteCacheNode.BYTE_SIZE];
						fs.Read(buffer, 0, buffer.Length);
						Assert.AreEqual(0, buffer[0], $"Record #{recordIndex + 1} is not marked as available!");
					}
				}
				finally {
					fs.Close();
				}
			}
		}

		[Test]
		public void TestCtorUpdatesWriteCacheFileWithCorrectRecordCount() {
			new CacheManager(
				DATABASE_FOLDER_PATH,
				DATABASE_MAX_SIZE_BYTES,
				WRITE_CACHE_FILE_PATH,
				WRITE_CACHE_MAX_RECORD_COUNT / 2,
				TimeSpan.FromMinutes(2)
			);

			new CacheManager(
				DATABASE_FOLDER_PATH,
				DATABASE_MAX_SIZE_BYTES,
				WRITE_CACHE_FILE_PATH,
				WRITE_CACHE_MAX_RECORD_COUNT,
				TimeSpan.FromMinutes(2)
			);

			var dataLength = new FileInfo(WRITE_CACHE_FILE_PATH).Length - WRITE_CACHE_MAGIC_NUMBER.Length;
			var recordCount = dataLength / IdWriteCacheNode.BYTE_SIZE;

			Assert.AreEqual(WRITE_CACHE_MAX_RECORD_COUNT, recordCount);
		}

		[Test]
		public void TestCtorUpdatesWriteCacheFileWithRecordsAllAvailable() {
			new CacheManager(
				DATABASE_FOLDER_PATH,
				DATABASE_MAX_SIZE_BYTES,
				WRITE_CACHE_FILE_PATH,
				WRITE_CACHE_MAX_RECORD_COUNT / 2,
				TimeSpan.FromMinutes(2)
			);

			new CacheManager(
				DATABASE_FOLDER_PATH,
				DATABASE_MAX_SIZE_BYTES,
				WRITE_CACHE_FILE_PATH,
				WRITE_CACHE_MAX_RECORD_COUNT,
				TimeSpan.FromMinutes(2)
			);

			using (var fs = new FileStream(WRITE_CACHE_FILE_PATH, FileMode.Open, FileAccess.Read)) {
				try {
					// Skip the header
					fs.Seek(WRITE_CACHE_MAGIC_NUMBER.Length, SeekOrigin.Begin);

					// Check each row.
					for (var recordIndex = 0; recordIndex < WRITE_CACHE_MAX_RECORD_COUNT; ++recordIndex) {
						var buffer = new byte[IdWriteCacheNode.BYTE_SIZE];
						fs.Read(buffer, 0, buffer.Length);
						Assert.AreEqual(0, buffer[0], $"Record #{recordIndex + 1} is not marked as available!");
					}
				}
				finally {
					fs.Close();
				}
			}
		}

		#endregion

		#region Putting assets

		[Test]
		public void TestPutAssetAssetNullThrowsArgNullException() {
			var mgr = new CacheManager(
				DATABASE_FOLDER_PATH,
				DATABASE_MAX_SIZE_BYTES,
				WRITE_CACHE_FILE_PATH,
				WRITE_CACHE_MAX_RECORD_COUNT,
				TimeSpan.FromMinutes(2)
			);

			Assert.Throws<ArgumentNullException>(() => mgr.PutAsset(null));
		}

		[Test]
		public void TestPutAssetEmptyIdThrowsArgException() {
			var mgr = new CacheManager(
				DATABASE_FOLDER_PATH,
				DATABASE_MAX_SIZE_BYTES,
				WRITE_CACHE_FILE_PATH,
				WRITE_CACHE_MAX_RECORD_COUNT,
				TimeSpan.FromMinutes(2)
			);

			var asset = new StratusAsset {
				Id = Guid.Empty,
			};

			Assert.Throws<ArgumentException>(() => mgr.PutAsset(asset));
		}

		[Test]
		public void TestPutAssetDoesntThrowFirstTime() {
			var mgr = new CacheManager(
				DATABASE_FOLDER_PATH,
				DATABASE_MAX_SIZE_BYTES,
				WRITE_CACHE_FILE_PATH,
				WRITE_CACHE_MAX_RECORD_COUNT,
				TimeSpan.FromMinutes(2)
			);

			var asset = new StratusAsset {
				Id = Guid.NewGuid(),
			};

			Assert.DoesNotThrow(() => mgr.PutAsset(asset));
		}

		[Test]
		public void TestPutAssetDoesntThrowDuplicate() {
			var mgr = new CacheManager(
				DATABASE_FOLDER_PATH,
				DATABASE_MAX_SIZE_BYTES,
				WRITE_CACHE_FILE_PATH,
				WRITE_CACHE_MAX_RECORD_COUNT,
				TimeSpan.FromMinutes(2)
			);

			var asset = new StratusAsset {
				Id = Guid.NewGuid(),
			};

			mgr.PutAsset(asset);
			Assert.DoesNotThrow(() => mgr.PutAsset(asset));
		}

		[Test]
		public void TestPutAssetDoesntThrowMultiple() {
			var mgr = new CacheManager(
				DATABASE_FOLDER_PATH,
				DATABASE_MAX_SIZE_BYTES,
				WRITE_CACHE_FILE_PATH,
				WRITE_CACHE_MAX_RECORD_COUNT,
				TimeSpan.FromMinutes(2)
			);

			Assert.DoesNotThrow(() => mgr.PutAsset(new StratusAsset {
				Id = Guid.NewGuid(),
			}));
			Assert.DoesNotThrow(() => mgr.PutAsset(new StratusAsset {
				Id = Guid.NewGuid(),
			}));
			Assert.DoesNotThrow(() => mgr.PutAsset(new StratusAsset {
				Id = Guid.NewGuid(),
			}));
			Assert.DoesNotThrow(() => mgr.PutAsset(new StratusAsset {
				Id = Guid.NewGuid(),
			}));
		}

		#endregion

		#region Get Assets

		[Test]
		public void TestGetAssetEmptyIdThrowsArgException() {
			var mgr = new CacheManager(
				DATABASE_FOLDER_PATH,
				DATABASE_MAX_SIZE_BYTES,
				WRITE_CACHE_FILE_PATH,
				WRITE_CACHE_MAX_RECORD_COUNT,
				TimeSpan.FromMinutes(2)
			);

			Assert.Throws<ArgumentException>(() => mgr.GetAsset(Guid.Empty));
		}

		[Test]
		public void TestGetAssetUnknownDoesntThrow() {
			var mgr = new CacheManager(
				DATABASE_FOLDER_PATH,
				DATABASE_MAX_SIZE_BYTES,
				WRITE_CACHE_FILE_PATH,
				WRITE_CACHE_MAX_RECORD_COUNT,
				TimeSpan.FromMinutes(2)
			);

			Assert.DoesNotThrow(() => mgr.GetAsset(Guid.NewGuid()));
		}

		[Test]
		public void TestGetAssetUnknownIsNull() {
			var mgr = new CacheManager(
				DATABASE_FOLDER_PATH,
				DATABASE_MAX_SIZE_BYTES,
				WRITE_CACHE_FILE_PATH,
				WRITE_CACHE_MAX_RECORD_COUNT,
				TimeSpan.FromMinutes(2)
			);

			Assert.IsNull(mgr.GetAsset(Guid.NewGuid()));
		}

		[Test]
		public void TestGetAssetKnownDoesntThrow() {
			var mgr = new CacheManager(
				DATABASE_FOLDER_PATH,
				DATABASE_MAX_SIZE_BYTES,
				WRITE_CACHE_FILE_PATH,
				WRITE_CACHE_MAX_RECORD_COUNT,
				TimeSpan.FromMinutes(2)
			);

			var id = Guid.NewGuid();

			mgr.PutAsset(new StratusAsset {
				Id = id,
			});

			Assert.DoesNotThrow(() => mgr.GetAsset(id));
		}

		[Test]
		public void TestGetAssetKnownIsNotNull() {
			var mgr = new CacheManager(
				DATABASE_FOLDER_PATH,
				DATABASE_MAX_SIZE_BYTES,
				WRITE_CACHE_FILE_PATH,
				WRITE_CACHE_MAX_RECORD_COUNT,
				TimeSpan.FromMinutes(2)
			);

			var id = Guid.NewGuid();

			mgr.PutAsset(new StratusAsset {
				Id = id,
			});

			mgr.GetAsset(id);

			Assert.IsNotNull(mgr.GetAsset(id));
		}

		[Test]
		public void TestGetAssetKnownHasSameId() {
			var mgr = new CacheManager(
				DATABASE_FOLDER_PATH,
				DATABASE_MAX_SIZE_BYTES,
				WRITE_CACHE_FILE_PATH,
				WRITE_CACHE_MAX_RECORD_COUNT,
				TimeSpan.FromMinutes(2)
			);

			var id = Guid.NewGuid();

			mgr.PutAsset(new StratusAsset {
				Id = id,
			});

			var resultAsset = mgr.GetAsset(id);

			Assert.AreEqual(id, resultAsset.Id);
		}

		[Test]
		public void TestGetAssetKnownIsIdentical() {
			var mgr = new CacheManager(
				DATABASE_FOLDER_PATH,
				DATABASE_MAX_SIZE_BYTES,
				WRITE_CACHE_FILE_PATH,
				WRITE_CACHE_MAX_RECORD_COUNT,
				TimeSpan.FromMinutes(2)
			);

			var baseAsset = new StratusAsset {
				CreateTime = DateTime.UtcNow,
				Data = new byte[] { 128, 42 },
				Description = RandomUtil.StringUTF8(128),
				Id = Guid.NewGuid(),
				Local = false,
				Name = RandomUtil.StringUTF8(32),
				StorageFlags = RandomUtil.NextUInt(),
				Temporary = false,
				Type = RandomUtil.NextSByte(),
			};

			mgr.PutAsset(baseAsset);

			var resultAsset = mgr.GetAsset(baseAsset.Id);

			Assert.AreEqual(baseAsset.CreateTime, resultAsset.CreateTime);
			Assert.AreEqual(baseAsset.Description, resultAsset.Description);
			Assert.AreEqual(baseAsset.Data, resultAsset.Data);
			Assert.AreEqual(baseAsset.Id, resultAsset.Id);
			Assert.AreEqual(baseAsset.Local, resultAsset.Local);
			Assert.AreEqual(baseAsset.Name, resultAsset.Name);
			Assert.AreEqual(baseAsset.StorageFlags, resultAsset.StorageFlags);
			Assert.AreEqual(baseAsset.Temporary, resultAsset.Temporary);
			Assert.AreEqual(baseAsset.Type, resultAsset.Type);
		}

		#endregion
	}
}
