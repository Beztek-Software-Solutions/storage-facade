// Copyright (c) Beztek Software Solutions. All rights reserved.
namespace Beztek.Facade.Storage.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using NUnit.Framework;

    [TestFixture]
    public class StorageFacadeUnitTests
    {
        [Test]
        public async Task TestFileStorageEnumerateNonRecursive()
        {
            await TestStorageEnumerate(GetTestFileStorageFacade(), false);
        }

        [Test]
        public async Task TestFileStorageEnumerateRecursive()
        {
            await TestStorageEnumerate(GetTestFileStorageFacade(), true);
        }

        [Test]
        public async Task TestFileStorageStreamRead()
        {
            await TestStorageStreamRead(GetTestFileStorageFacade());
        }

        [Test]
        public async Task TestFileStorageDelete()
        {
            await TestStorageDelete(GetTestFileStorageFacade());
        }

        // Internal

        private async Task TestStorageEnumerate(IStorageFacade storageFacade, Boolean isRecursive)
        {
            // First write
            string path = $"{storageFacade.GetName()}/testa/test1.txt";
            byte[] byteArray = Encoding.ASCII.GetBytes("Test write stream 1a");
            using (MemoryStream writeStream = new(byteArray))
            {
                await storageFacade.WriteStorageAsync(path, writeStream, true);
            }
            // Second write
            path = $"{storageFacade.GetName()}/testa/testb/test1.txt";
            byteArray = Encoding.ASCII.GetBytes("Test write stream 1b");
            using (MemoryStream writeStream = new(byteArray))
            {
                await storageFacade.WriteStorageAsync(path, writeStream, true);
            }

            StorageFilter storageFilter = new()
            {
                Extensions = new List<string>() { ".txt" }
            };
            int index = 0;
            foreach (StorageInfo storageInfo in storageFacade.EnumerateStorageInfo($"{storageFacade.GetName()}/testa", isRecursive, storageFilter))
            {
                index++;
            }

            // Cleanup
            foreach (StorageInfo storageInfo in storageFacade.EnumerateStorageInfo($"{storageFacade.GetName()}/testa", true, storageFilter))
            {
                await storageFacade.DeleteStorageAsync(storageInfo.LogicalPath);
            }
            Assert.AreEqual(isRecursive?2:1, index);
        }

        private async Task TestStorageStreamRead(IStorageFacade storageFacade)
        {
            // First write
            string path = $"{storageFacade.GetName()}/testc/test2.txt";
            string contents = "Test write stream 2";
            byte[] byteArray = Encoding.ASCII.GetBytes(contents);
            using (MemoryStream writeStream = new MemoryStream(byteArray))
            {
                await storageFacade.WriteStorageAsync(path, writeStream, true);
            }

            // Then read
            StorageInfo storageInfo = storageFacade.GetStorageInfo(path);
            using (Stream readStream = await storageFacade.ReadStorageAsync(storageInfo))
            {
                using (StreamReader reader = new(readStream, Encoding.UTF8))
                {
                    Assert.AreEqual(contents, reader.ReadToEnd());
                }
            }

            // Cleanup
            await storageFacade.DeleteStorageAsync(path);
        }

        private async Task TestStorageDelete(IStorageFacade storageFacade)
        {
            string path = $"{storageFacade.GetName()}/testd/test3.txt";
            string contents = "Test write stream 3";
            // First write
            byte[] byteArray = Encoding.ASCII.GetBytes(contents);
            MemoryStream stream = new(byteArray);
            await storageFacade.WriteStorageAsync(path, stream, true);
            StorageInfo storageInfo = storageFacade.GetStorageInfo(path);
            using (Stream readStream = await storageFacade.ReadStorageAsync(storageInfo))
            {
                using (StreamReader reader = new(readStream, Encoding.UTF8))
                {
                    Assert.AreEqual(contents, reader.ReadToEnd());
                }
            }

            // Then delete
            await storageFacade.DeleteStorageAsync(path);
            try {
                storageInfo = storageFacade.GetStorageInfo(path);
            } catch {
                return; // Success case. We expected an exception to be thrown since the storage has been deleted 
            }
            throw new Exception("Expected the storage to have been deleted");
        }

        private IStorageFacade GetTestFileStorageFacade()
        {
            IStorageProviderConfig config = new FileStorageProviderConfig();
            return StorageFacadeFactory.GetStorageFacade(config);
        }
    }
}