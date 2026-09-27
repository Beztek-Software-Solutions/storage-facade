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
    public class AlibabaOssStorageProviderTests
    {
        private readonly AlibabaOssStorageProviderConfig _config =
            new("oss-us-west-1.aliyuncs.com", "access", "secret", "Orders");

        [Test]
        public void GetName_ReturnsLowercasedOssUri()
        {
            using var provider = new AlibabaOssStorageProvider(_config, Mock.Of<IOssObjectClient>());
            Assert.That(provider.GetName(), Is.EqualTo("oss://orders"));
            Assert.That(provider.GetType(), Is.EqualTo(StorageFacadeType.AlibabaOssStore));
        }

        [Test]
        public void EnumerateStorageInfo_Recursive_ReturnsAllObjects()
        {
            var mock = new Mock<IOssObjectClient>();
            mock.Setup(c => c.ListObjects("a")).Returns(new[]
            {
                new OssListedObject("a/file1.txt", DateTime.UtcNow, 3),
                new OssListedObject("a/b/file2.txt", DateTime.UtcNow, 4),
            });

            using var provider = new AlibabaOssStorageProvider(_config, mock.Object);
            var results = new List<StorageInfo>(provider.EnumerateStorageInfo("oss://orders/a", isRecursive: true));

            Assert.That(results, Has.Count.EqualTo(2));
        }

        [Test]
        public void EnumerateStorageInfo_NonRecursive_ReturnsOnlyDirectChildren()
        {
            var mock = new Mock<IOssObjectClient>();
            mock.Setup(c => c.ListObjects("a")).Returns(new[]
            {
                new OssListedObject("a/file1.txt", DateTime.UtcNow, 3),
                new OssListedObject("a/nested/file2.txt", DateTime.UtcNow, 4),
            });

            using var provider = new AlibabaOssStorageProvider(_config, mock.Object);
            var results = new List<StorageInfo>(provider.EnumerateStorageInfo("oss://orders/a", isRecursive: false));

            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0].Name, Is.EqualTo("file1.txt"));
            Assert.That(results[0].LogicalPath, Is.EqualTo("oss://orders/a/file1.txt"));
        }

        [Test]
        public void GetStorageInfo_ReturnsMetadataFromClient()
        {
            var mock = new Mock<IOssObjectClient>();
            mock.Setup(c => c.GetObjectMetadata("docs/readme.txt"))
                .Returns(new OssObjectInfo(new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc), 42));

            using var provider = new AlibabaOssStorageProvider(_config, mock.Object);
            StorageInfo info = provider.GetStorageInfo("oss://orders/docs/readme.txt");

            Assert.That(info.Name, Is.EqualTo("readme.txt"));
            Assert.That(info.SizeBytes, Is.EqualTo(42));
            Assert.That(info.LogicalPath, Is.EqualTo("oss://orders/docs/readme.txt"));
        }

        [Test]
        public async Task ComputeMD5Checksum_ReturnsBase64Hash()
        {
            var mock = new Mock<IOssObjectClient>();
            mock.Setup(c => c.GetObjectStream("a/file.txt"))
                .Returns(new MemoryStream(Encoding.UTF8.GetBytes("hash-me")));

            using var provider = new AlibabaOssStorageProvider(_config, mock.Object);
            string checksum = await provider.ComputeMD5Checksum("oss://orders/a/file.txt");

            Assert.That(checksum, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task ReadWriteDelete_RoundTripViaMockedClient()
        {
            var store = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            var mock = new Mock<IOssObjectClient>();

            mock.Setup(c => c.PutObject(It.IsAny<string>(), It.IsAny<Stream>()))
                .Callback((string key, Stream content) =>
                {
                    using var ms = new MemoryStream();
                    content.CopyTo(ms);
                    store[key] = ms.ToArray();
                });

            mock.Setup(c => c.GetObjectStream(It.IsAny<string>()))
                .Returns((string key) =>
                {
                    if (!store.TryGetValue(key, out byte[] data))
                        throw new StorageNotFoundException(key);
                    return new MemoryStream(data, writable: false);
                });

            mock.Setup(c => c.DeleteObject(It.IsAny<string>()))
                .Callback((string key) => store.Remove(key));

            using var provider = new AlibabaOssStorageProvider(_config, mock.Object);
            string path = "oss://orders/out/data.bin";
            byte[] payload = Encoding.UTF8.GetBytes("oss-payload");

            using (var writeStream = new MemoryStream(payload))
                await provider.WriteStorageAsync(path, writeStream);

            using (var readStream = await provider.ReadStorageAsync(new StorageInfo { LogicalPath = path }))
            using (var reader = new StreamReader(readStream))
                Assert.That(reader.ReadToEnd(), Is.EqualTo("oss-payload"));

            await provider.DeleteStorageAsync(path);
            Assert.That(store.ContainsKey("out/data.bin"), Is.False);
        }
    }
}
