// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage.Tests
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    public class StorageFacadeTests
    {
        [Test]
        public void GetName_DelegatesToProvider()
        {
            var mock = new Mock<IStorageProvider>();
            mock.Setup(p => p.GetName()).Returns("s3://bucket");
            var facade = new StorageFacade(mock.Object);

            Assert.That(facade.GetName(), Is.EqualTo("s3://bucket"));
        }

        [Test]
        public void GetType_DelegatesToProvider()
        {
            var mock = new Mock<IStorageProvider>();
            mock.Setup(p => p.GetType()).Returns(StorageFacadeType.AmazonS3Store);
            var facade = new StorageFacade(mock.Object);

            Assert.That(facade.GetType(), Is.EqualTo(StorageFacadeType.AmazonS3Store));
        }

        [Test]
        public async Task WriteStorageAsync_ReturnsMd5Checksum()
        {
            IStorageFacade facade = MockStorageProviderBuilder.CreateS3Facade();
            string path = "s3://test-bucket/docs/file.txt";
            byte[] payload = Encoding.UTF8.GetBytes("checksum test");

            using var stream = new MemoryStream(payload);
            string checksum = await facade.WriteStorageAsync(path, stream);

            Assert.That(checksum, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task WriteStorageAsync_WithValidateChecksum_SucceedsWhenProviderMatches()
        {
            var (mock, store) = MockStorageProviderBuilder.Create("s3://bucket", StorageFacadeType.AmazonS3Store);
            string path = "s3://bucket/a.txt";
            var facade = new StorageFacade(mock.Object);

            byte[] payload = Encoding.UTF8.GetBytes("valid");
            using (var writeStream = new MemoryStream(payload))
            {
                string checksum = await facade.WriteStorageAsync(path, writeStream, validateChecksum: true);
                Assert.That(store.ContainsKey(path), Is.True);
                Assert.That(checksum, Is.Not.Empty);
            }
        }

        [Test]
        public void WriteStorageAsync_WithValidateChecksum_ThrowsWhenMismatch()
        {
            var mock = new Mock<IStorageProvider>();
            mock.Setup(p => p.GetName()).Returns("s3://bucket");
            mock.Setup(p => p.WriteStorageAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<bool>()))
                .Returns((string _, Stream inputStream, bool __) =>
                {
                    inputStream.CopyTo(Stream.Null);
                    return Task.CompletedTask;
                });
            mock.Setup(p => p.ComputeMD5Checksum(It.IsAny<string>()))
                .ReturnsAsync("different-checksum");

            var facade = new StorageFacade(mock.Object);
            byte[] payload = Encoding.UTF8.GetBytes("payload");

            using var stream = new MemoryStream(payload);
            var ex = Assert.ThrowsAsync<Exception>(async () =>
                await facade.WriteStorageAsync("s3://bucket/x.txt", stream, validateChecksum: true));

            Assert.That(ex!.Message, Does.Contain("does not match"));
        }

        [Test]
        public async Task ReadStorageAsync_DelegatesToProvider()
        {
            var mock = new Mock<IStorageProvider>();
            var info = new StorageInfo { LogicalPath = "s3://bucket/x.txt" };
            mock.Setup(p => p.ReadStorageAsync(info)).ReturnsAsync(new MemoryStream(Encoding.UTF8.GetBytes("data")));

            var facade = new StorageFacade(mock.Object);
            using Stream stream = await facade.ReadStorageAsync(info);
            using var reader = new StreamReader(stream);
            Assert.That(reader.ReadToEnd(), Is.EqualTo("data"));
        }

        [Test]
        public async Task DeleteStorageAsync_DelegatesToProvider()
        {
            var mock = new Mock<IStorageProvider>();
            mock.Setup(p => p.DeleteStorageAsync("s3://bucket/x.txt")).Returns(Task.CompletedTask);

            var facade = new StorageFacade(mock.Object);
            await facade.DeleteStorageAsync("s3://bucket/x.txt");

            mock.Verify(p => p.DeleteStorageAsync("s3://bucket/x.txt"), Times.Once);
        }

        [Test]
        public async Task ComputeMD5Checksum_DelegatesToProvider()
        {
            var mock = new Mock<IStorageProvider>();
            mock.Setup(p => p.ComputeMD5Checksum("s3://bucket/x.txt")).ReturnsAsync("abc123");

            var facade = new StorageFacade(mock.Object);
            Assert.That(await facade.ComputeMD5Checksum("s3://bucket/x.txt"), Is.EqualTo("abc123"));
        }
    }
}
