// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using Beztek.Facade.Storage.Providers;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    public class GoogleCloudStorageProviderTests
    {
        private readonly GoogleCloudStorageProviderConfig _config = new("My-Bucket");

        [Test]
        public void GetName_ReturnsLowercasedGsUri()
        {
            using var provider = new GoogleCloudStorageProvider(_config, Mock.Of<IGcsObjectClient>());
            Assert.That(provider.GetName(), Is.EqualTo("gs://my-bucket"));
            Assert.That(provider.GetType(), Is.EqualTo(StorageFacadeType.GoogleCloudStorageStore));
        }

        [Test]
        public void EnumerateStorageInfo_Recursive_ReturnsAllObjects()
        {
            var mock = new Mock<IGcsObjectClient>();
            mock.Setup(c => c.ListObjects("a")).Returns(new[]
            {
                new GcsListedObject("a/file1.txt", DateTimeOffset.UtcNow, 3),
                new GcsListedObject("a/b/file2.txt", DateTimeOffset.UtcNow, 4),
            });

            using var provider = new GoogleCloudStorageProvider(_config, mock.Object);
            var results = new List<StorageInfo>(provider.EnumerateStorageInfo("gs://my-bucket/a", isRecursive: true));

            Assert.That(results, Has.Count.EqualTo(2));
        }

        [Test]
        public void EnumerateStorageInfo_NonRecursive_ReturnsOnlyDirectChildren()
        {
            var mock = new Mock<IGcsObjectClient>();
            mock.Setup(c => c.ListObjects("a")).Returns(new[]
            {
                new GcsListedObject("a/file1.txt", DateTimeOffset.UtcNow, 3),
                new GcsListedObject("a/nested/file2.txt", DateTimeOffset.UtcNow, 4),
            });

            using var provider = new GoogleCloudStorageProvider(_config, mock.Object);
            var results = new List<StorageInfo>(provider.EnumerateStorageInfo("gs://my-bucket/a", isRecursive: false));

            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0].Name, Is.EqualTo("file1.txt"));
            Assert.That(results[0].LogicalPath, Is.EqualTo("gs://my-bucket/a/file1.txt"));
        }

        [Test]
        public void GetStorageInfo_ReturnsMetadataFromClient()
        {
            var mock = new Mock<IGcsObjectClient>();
            mock.Setup(c => c.GetObject("docs/readme.txt"))
                .Returns(new GcsListedObject(
                    "docs/readme.txt",
                    new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero),
                    42));

            using var provider = new GoogleCloudStorageProvider(_config, mock.Object);
            StorageInfo info = provider.GetStorageInfo("gs://my-bucket/docs/readme.txt");

            Assert.That(info.Name, Is.EqualTo("readme.txt"));
            Assert.That(info.SizeBytes, Is.EqualTo(42));
            Assert.That(info.LogicalPath, Is.EqualTo("gs://my-bucket/docs/readme.txt"));
        }

        [Test]
        public async Task ComputeMD5Checksum_ReturnsBase64Hash()
        {
            var mock = new Mock<IGcsObjectClient>();
            mock.Setup(c => c.DownloadObjectAsync("a/file.txt", It.IsAny<Stream>()))
                .Returns((string _, Stream dest) =>
                {
                    byte[] bytes = Encoding.UTF8.GetBytes("hash-me");
                    dest.Write(bytes);
                    return Task.CompletedTask;
                });

            using var provider = new GoogleCloudStorageProvider(_config, mock.Object);
            string checksum = await provider.ComputeMD5Checksum("gs://my-bucket/a/file.txt");

            Assert.That(checksum, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task ReadWriteDelete_RoundTripViaMockedClient()
        {
            var store = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            var mock = new Mock<IGcsObjectClient>();

            mock.Setup(c => c.UploadObjectAsync(It.IsAny<string>(), It.IsAny<Stream>()))
                .Returns((string key, Stream content) =>
                {
                    using var ms = new MemoryStream();
                    content.CopyTo(ms);
                    store[key] = ms.ToArray();
                    return Task.CompletedTask;
                });

            mock.Setup(c => c.DownloadObjectAsync(It.IsAny<string>(), It.IsAny<Stream>()))
                .Returns((string key, Stream dest) =>
                {
                    if (!store.TryGetValue(key, out byte[] data))
                        throw new StorageNotFoundException(key);
                    dest.Write(data);
                    return Task.CompletedTask;
                });

            mock.Setup(c => c.DeleteObjectAsync(It.IsAny<string>()))
                .Returns((string key) =>
                {
                    store.Remove(key);
                    return Task.CompletedTask;
                });

            using var provider = new GoogleCloudStorageProvider(_config, mock.Object);
            string path = "gs://my-bucket/out/data.bin";
            byte[] payload = Encoding.UTF8.GetBytes("gcs-payload");

            using (var writeStream = new MemoryStream(payload))
                await provider.WriteStorageAsync(path, writeStream);

            using (var readStream = await provider.ReadStorageAsync(new StorageInfo { LogicalPath = path }))
            using (var reader = new StreamReader(readStream))
                Assert.That(reader.ReadToEnd(), Is.EqualTo("gcs-payload"));

            await provider.DeleteStorageAsync(path);
            Assert.That(store.ContainsKey("out/data.bin"), Is.False);
        }
    }
}
