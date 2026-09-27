// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Threading.Tasks;
    using Amazon.S3;
    using Azure;
    using Beztek.Facade.Storage;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    public class StorageExceptionPolicyFacadeTests
    {
        [Test]
        public void GetStorageInfo_MapsAmazonS3NotFound_ToStorageNotFoundException()
        {
            var provider = new Mock<IStorageProvider>();
            provider.Setup(p => p.GetStorageInfo("s3://b/missing"))
                .Throws(new AmazonS3Exception("gone")
                {
                    StatusCode = HttpStatusCode.NotFound,
                    ErrorCode = "NoSuchKey",
                });

            var facade = new StorageFacade(provider.Object);
            StorageNotFoundException ex = Assert.Throws<StorageNotFoundException>(
                () => facade.GetStorageInfo("s3://b/missing"));
            Assert.That(ex.LogicalPath, Is.EqualTo("s3://b/missing"));
            Assert.That(ex.InnerException, Is.TypeOf<AmazonS3Exception>());
        }

        [Test]
        public void GetStorageInfo_MapsAmazonS3AccessDenied_ToStorageFacadeException()
        {
            var provider = new Mock<IStorageProvider>();
            provider.Setup(p => p.GetStorageInfo("s3://b/secret"))
                .Throws(new AmazonS3Exception("denied")
                {
                    StatusCode = HttpStatusCode.Forbidden,
                    ErrorCode = "AccessDenied",
                });

            var facade = new StorageFacade(provider.Object);
            StorageFacadeException ex = Assert.Throws<StorageFacadeException>(
                () => facade.GetStorageInfo("s3://b/secret"));
            Assert.That(ex.InnerException, Is.TypeOf<AmazonS3Exception>());
        }

        [Test]
        public void GetStorageInfo_MapsAzure404_ToStorageNotFoundException()
        {
            var provider = new Mock<IStorageProvider>();
            provider.Setup(p => p.GetStorageInfo("https://acct/c/x"))
                .Throws(new RequestFailedException(404, "Not Found"));

            var facade = new StorageFacade(provider.Object);
            Assert.Throws<StorageNotFoundException>(() => facade.GetStorageInfo("https://acct/c/x"));
        }

        [Test]
        public void GetStorageInfo_MapsWrappedAggregateS3Miss_ToStorageNotFoundException()
        {
            var s3 = new AmazonS3Exception("gone")
            {
                StatusCode = HttpStatusCode.NotFound,
                ErrorCode = "NotFound",
            };
            var provider = new Mock<IStorageProvider>();
            provider.Setup(p => p.GetStorageInfo("s3://b/x"))
                .Throws(new AggregateException(s3));

            var facade = new StorageFacade(provider.Object);
            Assert.Throws<StorageNotFoundException>(() => facade.GetStorageInfo("s3://b/x"));
        }

        [Test]
        public void GetStorageInfo_PassesThroughArgumentException()
        {
            var provider = new Mock<IStorageProvider>();
            provider.Setup(p => p.GetStorageInfo(It.IsAny<string>()))
                .Throws(new ArgumentException("bad path", "storagePath"));

            var facade = new StorageFacade(provider.Object);
            Assert.Throws<ArgumentException>(() => facade.GetStorageInfo("x"));
        }

        [Test]
        public async Task ReadStorageAsync_MapsGenericIoFailure_ToStorageFacadeException()
        {
            var provider = new Mock<IStorageProvider>();
            provider.Setup(p => p.ReadStorageAsync(It.IsAny<StorageInfo>()))
                .ThrowsAsync(new IOException("disk died"));

            var facade = new StorageFacade(provider.Object);
            StorageFacadeException ex = Assert.ThrowsAsync<StorageFacadeException>(
                async () => await facade.ReadStorageAsync(new StorageInfo { LogicalPath = "/tmp/a" }));
            Assert.That(ex.InnerException, Is.TypeOf<IOException>());
        }

        [Test]
        public async Task WriteStorageAsync_MapsProviderFailure_ToStorageFacadeException()
        {
            var provider = new Mock<IStorageProvider>();
            provider.Setup(p => p.WriteStorageAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<bool>()))
                .ThrowsAsync(new AmazonS3Exception("slow down")
                {
                    StatusCode = HttpStatusCode.ServiceUnavailable,
                    ErrorCode = "SlowDown",
                });

            var facade = new StorageFacade(provider.Object);
            await using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
            Assert.ThrowsAsync<StorageFacadeException>(
                async () => await facade.WriteStorageAsync("s3://b/a.bin", stream));
        }

        [Test]
        public async Task DeleteStorageAsync_SwallowsMissingObject()
        {
            var provider = new Mock<IStorageProvider>();
            provider.Setup(p => p.DeleteStorageAsync("s3://b/gone"))
                .ThrowsAsync(new AmazonS3Exception("gone")
                {
                    StatusCode = HttpStatusCode.NotFound,
                    ErrorCode = "NoSuchKey",
                });

            var facade = new StorageFacade(provider.Object);
            Assert.DoesNotThrowAsync(async () => await facade.DeleteStorageAsync("s3://b/gone"));
        }

        [Test]
        public async Task DeleteStorageAsync_MapsOtherFailures_ToStorageFacadeException()
        {
            var provider = new Mock<IStorageProvider>();
            provider.Setup(p => p.DeleteStorageAsync("s3://b/x"))
                .ThrowsAsync(new AmazonS3Exception("denied")
                {
                    StatusCode = HttpStatusCode.Forbidden,
                    ErrorCode = "AccessDenied",
                });

            var facade = new StorageFacade(provider.Object);
            Assert.ThrowsAsync<StorageFacadeException>(
                async () => await facade.DeleteStorageAsync("s3://b/x"));
        }

        [Test]
        public async Task ComputeMD5Checksum_MapsMissing_ToStorageNotFoundException()
        {
            var provider = new Mock<IStorageProvider>();
            provider.Setup(p => p.ComputeMD5Checksum("/tmp/missing"))
                .ThrowsAsync(new StorageNotFoundException("/tmp/missing"));

            var facade = new StorageFacade(provider.Object);
            Assert.ThrowsAsync<StorageNotFoundException>(
                async () => await facade.ComputeMD5Checksum("/tmp/missing"));
        }

        [Test]
        public void EnumerateStorageInfo_MapsMissingDirectory_ToStorageNotFoundException()
        {
            var provider = new Mock<IStorageProvider>();
            provider.Setup(p => p.EnumerateStorageInfo(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<StorageFilter>()))
                .Throws(new DirectoryNotFoundException("gone"));

            var facade = new StorageFacade(provider.Object);
            Assert.Throws<StorageNotFoundException>(
                () => facade.EnumerateStorageInfo("/tmp/nope").GetEnumerator().MoveNext());
        }

        [Test]
        public void EnumerateStorageInfo_MapsProviderFailure_ToStorageFacadeException()
        {
            var provider = new Mock<IStorageProvider>();
            provider.Setup(p => p.EnumerateStorageInfo(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<StorageFilter>()))
                .Returns(ThrowingEnumerable());

            var facade = new StorageFacade(provider.Object);
            Assert.Throws<StorageFacadeException>(
                () =>
                {
                    foreach (StorageInfo _ in facade.EnumerateStorageInfo("s3://b/p"))
                    {
                    }
                });
        }

        private static IEnumerable<StorageInfo> ThrowingEnumerable()
        {
            yield return new StorageInfo { LogicalPath = "s3://b/p/a" };
            throw new AmazonS3Exception("boom")
            {
                StatusCode = HttpStatusCode.InternalServerError,
                ErrorCode = "InternalError",
            };
        }
    }
}
