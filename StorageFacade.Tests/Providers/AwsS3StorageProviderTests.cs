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
    using Amazon.S3.Transfer;
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

        /// <summary>
        /// Unknown-length streams use TransferUtility multipart (part-sized chunks), not a full MemoryStream buffer.
        /// PutObject alone still requires ContentLength when Length is not reported (AWSSDK.S3 4.x).
        /// </summary>
        [Test]
        public async Task WriteStorageAsync_NonSeekableStream_UsesTransferUtilityMultipartUpload()
        {
            TransferUtilityUploadRequest captured = null;
            var mockTransfer = new Mock<ITransferUtility>();
            mockTransfer.Setup(t => t.UploadAsync(It.IsAny<TransferUtilityUploadRequest>(), default))
                .Returns((TransferUtilityUploadRequest req, CancellationToken _) =>
                {
                    captured = req;
                    using var ms = new MemoryStream();
                    req.InputStream.CopyTo(ms);
                    Assert.That(ms.ToArray(), Is.EqualTo(Encoding.UTF8.GetBytes("multipart-body")));
                    return Task.CompletedTask;
                });

            var mockS3 = new Mock<IAmazonS3>(MockBehavior.Strict);
            var provider = new AwsS3StorageProvider(_config, mockS3.Object, mockTransfer.Object);
            byte[] payload = Encoding.UTF8.GetBytes("multipart-body");

            using var inner = new MemoryStream(payload);
            using var nonSeekable = new NonSeekableStream(inner);
            await provider.WriteStorageAsync("s3://orders/uploads/photo.jpg", nonSeekable);

            Assert.That(captured, Is.Not.Null);
            Assert.That(captured.BucketName, Is.EqualTo(Bucket));
            Assert.That(captured.Key, Is.EqualTo("uploads/photo.jpg"));
            Assert.That(captured.AutoCloseStream, Is.False);
            mockS3.Verify(c => c.PutObjectAsync(It.IsAny<PutObjectRequest>(), default), Times.Never);
        }

        [Test]
        public async Task WriteStorageAsync_SeekableStream_SetsContentLengthWithoutRebufferingRequirement()
        {
            PutObjectRequest captured = null;
            var mockS3 = new Mock<IAmazonS3>();
            mockS3.Setup(c => c.PutObjectAsync(It.IsAny<PutObjectRequest>(), default))
                .Returns((PutObjectRequest req, CancellationToken _) =>
                {
                    captured = req;
                    if (req.Headers.ContentLength <= 0)
                        throw new AmazonS3Exception("MissingContentLength");
                    return Task.FromResult(new PutObjectResponse());
                });

            var mockTransfer = new Mock<ITransferUtility>(MockBehavior.Strict);
            var provider = new AwsS3StorageProvider(_config, mockS3.Object, mockTransfer.Object);
            byte[] payload = Encoding.UTF8.GetBytes("seekable");

            using var writeStream = new MemoryStream(payload);
            await provider.WriteStorageAsync("s3://orders/out/seek.bin", writeStream);

            Assert.That(captured, Is.Not.Null);
            Assert.That(captured.Headers.ContentLength, Is.EqualTo(payload.Length));
            mockTransfer.Verify(t => t.UploadAsync(It.IsAny<TransferUtilityUploadRequest>(), default), Times.Never);
        }

        [Test]
        public async Task StorageFacade_WriteStorageAsync_NonSeekableStream_UsesTransferUtility()
        {
            // Production path: StorageFacade wraps input in CryptoStream (non-seekable / unknown length).
            TransferUtilityUploadRequest captured = null;
            var mockTransfer = new Mock<ITransferUtility>();
            mockTransfer.Setup(t => t.UploadAsync(It.IsAny<TransferUtilityUploadRequest>(), default))
                .Returns((TransferUtilityUploadRequest req, CancellationToken _) =>
                {
                    captured = req;
                    using var ms = new MemoryStream();
                    req.InputStream.CopyTo(ms);
                    Assert.That(ms.ToArray(), Is.EqualTo(Encoding.UTF8.GetBytes("form-file")));
                    return Task.CompletedTask;
                });

            var facade = new StorageFacade(new AwsS3StorageProvider(_config, Mock.Of<IAmazonS3>(), mockTransfer.Object));
            byte[] payload = Encoding.UTF8.GetBytes("form-file");

            using var inner = new MemoryStream(payload);
            using var nonSeekable = new NonSeekableStream(inner);
            string checksum = await facade.WriteStorageAsync("s3://orders/uploads/avatar.png", nonSeekable);

            Assert.That(checksum, Is.Not.Null.And.Not.Empty);
            Assert.That(captured, Is.Not.Null);
            Assert.That(captured.AutoCloseStream, Is.False);
        }

        /// <summary>Forward-only stream with no Length/Position — like multipart request bodies.</summary>
        private sealed class NonSeekableStream : Stream
        {
            private readonly Stream _inner;

            public NonSeekableStream(Stream inner) => _inner = inner;

            public override bool CanRead => _inner.CanRead;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override void Flush() => _inner.Flush();
            public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}
