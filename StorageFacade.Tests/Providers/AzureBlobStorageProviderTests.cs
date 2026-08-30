// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using Azure.Storage.Blobs.Models;
    using Beztek.Facade.Storage;
    using Beztek.Facade.Storage.Providers;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    public class AzureBlobStorageProviderTests
    {
        private readonly AzureBlobStorageProviderConfig _flatConfig =
            new(new Uri("https://acct.blob.core.windows.net/data?sig=test"), isHierarchicalNamespace: false);

        private readonly AzureBlobStorageProviderConfig _hnsConfig =
            new(new Uri("https://acct.blob.core.windows.net/data?sig=test"), isHierarchicalNamespace: true);

        [Test]
        public void GetName_ReturnsContainerUri()
        {
            var provider = new AzureBlobStorageProvider(_flatConfig, Mock.Of<IAzureBlobContainerAdapter>());
            Assert.That(provider.GetName(), Is.EqualTo("https://acct.blob.core.windows.net/data"));
        }

        [Test]
        public void EnumerateStorageInfo_FlatPrefix_Recursive_IncludesNestedBlobs()
        {
            var mock = new Mock<IAzureBlobContainerAdapter>();
            mock.Setup(a => a.GetBlobs("reports/"))
                .Returns(new[]
                {
                    BlobsModelFactory.BlobItem("reports/summary.pdf"),
                    BlobsModelFactory.BlobItem("reports/2024/jan.pdf")
                });
            mock.Setup(a => a.GetBlobProperties(It.IsAny<string>()))
                .Returns(BlobsModelFactory.BlobProperties(contentLength: 10, lastModified: DateTimeOffset.UtcNow));

            var provider = new AzureBlobStorageProvider(_flatConfig, mock.Object);
            var results = new List<StorageInfo>(provider.EnumerateStorageInfo("https://acct.blob.core.windows.net/data/reports", true));

            Assert.That(results, Has.Count.EqualTo(2));
        }

        [Test]
        public void EnumerateStorageInfo_FlatPrefix_ReturnsDirectBlobs()
        {
            var mock = new Mock<IAzureBlobContainerAdapter>();
            mock.Setup(a => a.GetBlobs("reports/"))
                .Returns(new[]
                {
                    BlobsModelFactory.BlobItem("reports/summary.pdf")
                });
            mock.Setup(a => a.GetBlobProperties("reports/summary.pdf"))
                .Returns(BlobsModelFactory.BlobProperties(contentLength: 100, lastModified: DateTimeOffset.UtcNow));

            var provider = new AzureBlobStorageProvider(_flatConfig, mock.Object);
            var results = new List<StorageInfo>(provider.EnumerateStorageInfo("https://acct.blob.core.windows.net/data/reports", false));

            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0].Name, Is.EqualTo("summary.pdf"));
        }

        [Test]
        public void EnumerateStorageInfo_HierarchicalNamespace_ListsBlobItems()
        {
            var mock = new Mock<IAzureBlobContainerAdapter>();
            mock.Setup(a => a.GetBlobsByHierarchy("archive/"))
                .Returns(new[]
                {
                    BlobsModelFactory.BlobHierarchyItem(prefix: null, blob: BlobsModelFactory.BlobItem(name: "archive/photo.jpg"))
                });
            mock.Setup(a => a.GetBlobProperties("archive/photo.jpg"))
                .Returns(BlobsModelFactory.BlobProperties(contentLength: 50, lastModified: DateTimeOffset.UtcNow));

            var provider = new AzureBlobStorageProvider(_hnsConfig, mock.Object);
            var results = new List<StorageInfo>(provider.EnumerateStorageInfo("https://acct.blob.core.windows.net/data/archive", false));

            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0].Name, Is.EqualTo("photo.jpg"));
        }

        [Test]
        public async Task ReadWriteDelete_UsesMockedBlobAdapter()
        {
            var store = new Dictionary<string, byte[]>();
            var mock = new Mock<IAzureBlobContainerAdapter>();

            mock.Setup(a => a.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>()))
                .Returns((string name, Stream stream) =>
                {
                    using var ms = new MemoryStream();
                    stream.CopyTo(ms);
                    store[name] = ms.ToArray();
                    return Task.CompletedTask;
                });

            mock.Setup(a => a.BlobExistsAsync(It.IsAny<string>()))
                .ReturnsAsync((string name) => store.ContainsKey(name));

            mock.Setup(a => a.OpenReadAsync(It.IsAny<string>()))
                .ReturnsAsync((string name) => new MemoryStream(store[name], writable: false));

            mock.Setup(a => a.DeleteAsync(It.IsAny<string>()))
                .Returns((string name) =>
                {
                    store.Remove(name);
                    return Task.CompletedTask;
                });

            mock.Setup(a => a.GetBlobProperties("docs/note.txt"))
                .Returns(BlobsModelFactory.BlobProperties(contentLength: 4, lastModified: DateTimeOffset.UtcNow));

            var provider = new AzureBlobStorageProvider(_flatConfig, mock.Object);
            string path = "https://acct.blob.core.windows.net/data/docs/note.txt";

            using (var writeStream = new MemoryStream(Encoding.UTF8.GetBytes("note")))
                await provider.WriteStorageAsync(path, writeStream);

            StorageInfo info = provider.GetStorageInfo(path);
            Assert.That(info.SizeBytes, Is.EqualTo(4));

            using (var readStream = await provider.ReadStorageAsync(info))
            using (var reader = new StreamReader(readStream))
                Assert.That(reader.ReadToEnd(), Is.EqualTo("note"));

            await provider.DeleteStorageAsync(path);
            Assert.That(store.ContainsKey("docs/note.txt"), Is.False);
        }

        [Test]
        public void ReadStorageAsync_ThrowsWhenBlobMissing()
        {
            var mock = new Mock<IAzureBlobContainerAdapter>();
            mock.Setup(a => a.BlobExistsAsync(It.IsAny<string>())).ReturnsAsync(false);

            var provider = new AzureBlobStorageProvider(_flatConfig, mock.Object);
            Assert.ThrowsAsync<Exception>(async () =>
                await provider.ReadStorageAsync(new StorageInfo { LogicalPath = "https://acct.blob.core.windows.net/data/missing.txt" }));
        }
    }
}
