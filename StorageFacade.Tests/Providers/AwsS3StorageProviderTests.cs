// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.S3;
    using Amazon.S3.Model;
    using Beztek.Facade.Storage;
    using Beztek.Facade.Storage.Providers;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    public class AwsS3StorageProviderTests
    {
        private const string Bucket = "orders";
        private readonly AwsS3StorageProviderConfig _config = new("key", "secret", "us-east-1", Bucket);

        [Test]
        public void GetName_ReturnsConfiguredS3Uri()
        {
            var provider = new AwsS3StorageProvider(_config, Mock.Of<IAmazonS3>());
            Assert.That(provider.GetName(), Is.EqualTo("s3://orders"));
        }

        [Test]
        public void EnumerateStorageInfo_Recursive_ReturnsAllObjects()
        {
            var mockS3 = new Mock<IAmazonS3>();
            mockS3.Setup(c => c.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), default))
                .ReturnsAsync(new ListObjectsV2Response
                {
                    S3Objects = new List<S3Object>
                    {
                        new() { Key = "a/file1.txt", LastModified = DateTime.UtcNow, Size = 3 },
                        new() { Key = "a/b/file2.txt", LastModified = DateTime.UtcNow, Size = 4 }
                    },
                    IsTruncated = false
                });

            var provider = new AwsS3StorageProvider(_config, mockS3.Object);
            var results = new List<StorageInfo>(provider.EnumerateStorageInfo("s3://orders/a", isRecursive: true));

            Assert.That(results, Has.Count.EqualTo(2));
        }

        [Test]
        public void EnumerateStorageInfo_NonRecursive_ReturnsOnlyDirectChildren()
        {
            var mockS3 = new Mock<IAmazonS3>();
            mockS3.Setup(c => c.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), default))
                .ReturnsAsync(new ListObjectsV2Response
                {
                    S3Objects = new List<S3Object>
                    {
                        new() { Key = "a/file1.txt", LastModified = DateTime.UtcNow, Size = 3 },
                        new() { Key = "a/nested/file2.txt", LastModified = DateTime.UtcNow, Size = 4 }
                    },
                    IsTruncated = false
                });

            var provider = new AwsS3StorageProvider(_config, mockS3.Object);
            var results = new List<StorageInfo>(provider.EnumerateStorageInfo("s3://orders/a", isRecursive: false));

            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0].Name, Is.EqualTo("file1.txt"));
        }

        [Test]
        public async Task ComputeMD5Checksum_ReturnsBase64Hash()
        {
            var mockS3 = new Mock<IAmazonS3>();
            mockS3.Setup(c => c.GetObjectAsync(It.IsAny<GetObjectRequest>(), default))
                .ReturnsAsync(new GetObjectResponse
                {
                    ResponseStream = new MemoryStream(Encoding.UTF8.GetBytes("hash-me"))
                });

            var provider = new AwsS3StorageProvider(_config, mockS3.Object);
            string checksum = await provider.ComputeMD5Checksum("s3://orders/a/file.txt");

            Assert.That(checksum, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void GetStorageInfo_ReturnsMetadataFromS3()
        {
            var mockS3 = new Mock<IAmazonS3>();
            mockS3.Setup(c => c.GetObjectMetadataAsync(Bucket, "docs/readme.txt", default))
                .ReturnsAsync(new GetObjectMetadataResponse
                {
                    LastModified = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                    ContentLength = 42
                });

            var provider = new AwsS3StorageProvider(_config, mockS3.Object);
            StorageInfo info = provider.GetStorageInfo("s3://orders/docs/readme.txt");

            Assert.That(info.Name, Is.EqualTo("readme.txt"));
            Assert.That(info.SizeBytes, Is.EqualTo(42));
        }

        [Test]
        public async Task ReadWriteDelete_RoundTripViaMockedS3Client()
        {
            var store = new Dictionary<string, byte[]>();
            var mockS3 = new Mock<IAmazonS3>();

            mockS3.Setup(c => c.PutObjectAsync(It.IsAny<PutObjectRequest>(), default))
                .Returns((PutObjectRequest req, CancellationToken _) =>
                {
                    using var ms = new MemoryStream();
                    req.InputStream.CopyTo(ms);
                    store[req.Key] = ms.ToArray();
                    return Task.FromResult(new PutObjectResponse());
                });

            mockS3.Setup(c => c.GetObjectAsync(It.IsAny<GetObjectRequest>(), default))
                .Returns((GetObjectRequest req, CancellationToken _) =>
                {
                    if (!store.TryGetValue(req.Key, out byte[] data))
                        throw new AmazonS3Exception("Not found");

                    return Task.FromResult(new GetObjectResponse
                    {
                        ResponseStream = new MemoryStream(data, writable: false)
                    });
                });

            mockS3.Setup(c => c.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), default))
                .Returns((DeleteObjectRequest req, CancellationToken _) =>
                {
                    store.Remove(req.Key);
                    return Task.FromResult(new DeleteObjectResponse());
                });

            var provider = new AwsS3StorageProvider(_config, mockS3.Object);
            string path = "s3://orders/out/data.bin";
            byte[] payload = Encoding.UTF8.GetBytes("s3-payload");

            using (var writeStream = new MemoryStream(payload))
                await provider.WriteStorageAsync(path, writeStream);

            var info = new StorageInfo { LogicalPath = path };
            using (var readStream = await provider.ReadStorageAsync(info))
            using (var reader = new StreamReader(readStream))
                Assert.That(reader.ReadToEnd(), Is.EqualTo("s3-payload"));

            await provider.DeleteStorageAsync(path);
            Assert.That(store.ContainsKey("out/data.bin"), Is.False);
        }
    }
}
