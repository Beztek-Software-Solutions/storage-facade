// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using NUnit.Framework;

    /// <summary>
    /// Exercises read/write/enumerate/delete flows for non-local providers via Moq-backed
    /// <see cref="IStorageProvider"/> instances (S3, Azure Blob, SMB naming conventions).
    /// </summary>
    [TestFixture]
    public class RemoteStorageFacadeTests
    {
        private static IEnumerable<TestCaseData> RemoteFacades()
        {
            yield return new TestCaseData(MockStorageProviderBuilder.CreateS3Facade()).SetName("S3");
            yield return new TestCaseData(MockStorageProviderBuilder.CreateAzureFacade()).SetName("AzureBlob");
            yield return new TestCaseData(MockStorageProviderBuilder.CreateSmbFacade()).SetName("SMB");
        }

        [TestCaseSource(nameof(RemoteFacades))]
        public async Task Enumerate_NonRecursive_ReturnsDirectChildrenOnly(IStorageFacade facade)
        {
            await TestStorageEnumerate(facade, isRecursive: false, expectedCount: 1);
        }

        [TestCaseSource(nameof(RemoteFacades))]
        public async Task Enumerate_Recursive_ReturnsNestedFiles(IStorageFacade facade)
        {
            await TestStorageEnumerate(facade, isRecursive: true, expectedCount: 2);
        }

        [TestCaseSource(nameof(RemoteFacades))]
        public async Task StreamRead_RoundTripsWrittenContent(IStorageFacade facade)
        {
            await TestStorageStreamRead(facade);
        }

        [TestCaseSource(nameof(RemoteFacades))]
        public async Task Delete_RemovesObject(IStorageFacade facade)
        {
            await TestStorageDelete(facade);
        }

        private static async Task TestStorageEnumerate(IStorageFacade storageFacade, bool isRecursive, int expectedCount)
        {
            string path = $"{storageFacade.GetName()}/testa/test1.txt";
            await WriteText(storageFacade, path, "Test write stream 1a");

            path = $"{storageFacade.GetName()}/testa/testb/test1.txt";
            await WriteText(storageFacade, path, "Test write stream 1b");

            var storageFilter = new StorageFilter { Extensions = new List<string> { ".txt" } };
            int index = 0;
            foreach (StorageInfo _ in storageFacade.EnumerateStorageInfo($"{storageFacade.GetName()}/testa", isRecursive, storageFilter))
                index++;

            foreach (StorageInfo storageInfo in storageFacade.EnumerateStorageInfo($"{storageFacade.GetName()}/testa", true, storageFilter))
                await storageFacade.DeleteStorageAsync(storageInfo.LogicalPath);

            Assert.That(index, Is.EqualTo(expectedCount));
        }

        private static async Task TestStorageStreamRead(IStorageFacade storageFacade)
        {
            string path = $"{storageFacade.GetName()}/testc/test2.txt";
            string contents = "Test write stream 2";
            await WriteText(storageFacade, path, contents);

            StorageInfo storageInfo = storageFacade.GetStorageInfo(path);
            using Stream readStream = await storageFacade.ReadStorageAsync(storageInfo);
            using var reader = new StreamReader(readStream, Encoding.UTF8);
            Assert.That(reader.ReadToEnd(), Is.EqualTo(contents));

            await storageFacade.DeleteStorageAsync(path);
        }

        private static async Task TestStorageDelete(IStorageFacade storageFacade)
        {
            string path = $"{storageFacade.GetName()}/testd/test3.txt";
            string contents = "Test write stream 3";
            await WriteText(storageFacade, path, contents);

            StorageInfo storageInfo = storageFacade.GetStorageInfo(path);
            using (Stream readStream = await storageFacade.ReadStorageAsync(storageInfo))
            using (var reader = new StreamReader(readStream, Encoding.UTF8))
                Assert.That(reader.ReadToEnd(), Is.EqualTo(contents));

            await storageFacade.DeleteStorageAsync(path);
            Assert.Throws<StorageNotFoundException>(() => storageFacade.GetStorageInfo(path));
        }

        private static async Task WriteText(IStorageFacade facade, string path, string contents)
        {
            byte[] byteArray = Encoding.UTF8.GetBytes(contents);
            using var writeStream = new MemoryStream(byteArray);
            await facade.WriteStorageAsync(path, writeStream, createParentDirectories: true);
        }
    }
}
