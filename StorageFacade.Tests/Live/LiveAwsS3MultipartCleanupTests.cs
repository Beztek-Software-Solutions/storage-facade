// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage.Tests.Live
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Amazon.Runtime;
    using Amazon.S3;
    using Amazon.S3.Model;
    using Beztek.Facade.Storage;
    using NUnit.Framework;

    /// <summary>
    /// Live S3 multipart cleanup: incomplete MPUs must be aborted by write-failure cleanup
    /// and by <see cref="IStorageFacade.DeleteStorageAsync"/>. Requires
    /// <c>STORAGEFACADE_LIVE_PROVIDERS</c> including <c>s3</c> (e.g. <c>make -- test --use-s3-container</c>).
    /// </summary>
    [TestFixture]
    [Category("Live")]
    public class LiveAwsS3MultipartCleanupTests
    {
        private LiveProviderHost _host;
        private IAmazonS3 _s3;
        private string _bucket;

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            if (!LiveProviderSelection.Resolve().Contains(StorageFacadeType.AmazonS3Store))
            {
                Assert.Inconclusive(
                    "S3 live multipart cleanup requires STORAGEFACADE_LIVE_PROVIDERS to include s3 "
                    + "(make -- test --use-s3-container).");
            }

            try
            {
                _host = await LiveProviderHost.StartAsync(StorageFacadeType.AmazonS3Store).ConfigureAwait(false);
                (_s3, _bucket) = CreateS3ClientAndBucket();
            }
            catch (InvalidOperationException ex)
            {
                Assert.Inconclusive(ex.Message);
            }
            catch (Exception ex) when (LiveTestHelpers.IsStartupFailure(ex))
            {
                Assert.Inconclusive($"Skipping S3 multipart live tests: startup failed — {ex.Message}");
            }
        }

        [OneTimeTearDown]
        public async Task OneTimeTearDown()
        {
            _s3?.Dispose();
            if (_host != null)
                await _host.DisposeAsync().ConfigureAwait(false);
        }

        [SetUp]
        public void SetUp()
        {
            Assume.That(_host, Is.Not.Null);
            Assume.That(_s3, Is.Not.Null);
        }

        [Test]
        public async Task DeleteStorageAsync_AbortsOrphanIncompleteMultipartUpload()
        {
            string relative = "live-mpu/" + Guid.NewGuid().ToString("N") + ".bin";
            string path = _host.ObjectPath(relative);
            string key = relative.Replace('\\', '/');

            InitiateMultipartUploadResponse init = await _s3.InitiateMultipartUploadAsync(
                new InitiateMultipartUploadRequest
                {
                    BucketName = _bucket,
                    Key = key,
                }).ConfigureAwait(false);

            byte[] partBytes = Encoding.UTF8.GetBytes(new string('x', 5 * 1024 * 1024)); // 5 MiB min part
            using (var partStream = new MemoryStream(partBytes))
            {
                await _s3.UploadPartAsync(new UploadPartRequest
                {
                    BucketName = _bucket,
                    Key = key,
                    UploadId = init.UploadId,
                    PartNumber = 1,
                    InputStream = partStream,
                    PartSize = partBytes.Length,
                }).ConfigureAwait(false);
            }

            Assert.That(
                await ListExactKeyUploadIdsAsync(key).ConfigureAwait(false),
                Does.Contain(init.UploadId),
                "precondition: incomplete multipart must be visible before delete");

            await _host.Storage.DeleteStorageAsync(path).ConfigureAwait(false);

            Assert.That(
                await ListExactKeyUploadIdsAsync(key).ConfigureAwait(false),
                Does.Not.Contain(init.UploadId),
                "DeleteStorageAsync must abort incomplete multipart uploads for the key");
        }

        [Test]
        public async Task WriteStorageAsync_StreamAbort_CleansIncompleteMultipartUpload()
        {
            string relative = "live-mpu-abort/" + Guid.NewGuid().ToString("N") + ".bin";
            string path = _host.ObjectPath(relative);
            string key = relative.Replace('\\', '/');

            // Non-seekable stream that fails after enough bytes for TransferUtility to start multipart.
            using var failing = new LiveTestHelpers.FailAfterBytesStream(6 * 1024 * 1024);

            Exception thrown = null;
            try
            {
                await _host.Storage.WriteStorageAsync(path, failing, createParentDirectories: true)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                thrown = ex;
            }

            Assert.That(thrown, Is.Not.Null, "write must fail when the source stream throws");

            // Best-effort cleanup on write failure should leave no incomplete MPU for this key.
            IReadOnlyList<string> remaining = await ListExactKeyUploadIdsAsync(key).ConfigureAwait(false);
            Assert.That(remaining, Is.Empty,
                "write-failure cleanup must abort incomplete multipart uploads; left: "
                + string.Join(", ", remaining));

            // Idempotent try-delete also clears any residual object/MPU.
            try
            {
                await _host.Storage.DeleteStorageAsync(path).ConfigureAwait(false);
            }
            catch
            {
                // Missing object is fine.
            }

            Assert.That(await ListExactKeyUploadIdsAsync(key).ConfigureAwait(false), Is.Empty);
        }

        private async Task<IReadOnlyList<string>> ListExactKeyUploadIdsAsync(string key)
        {
            var uploadIds = new List<string>();
            string keyMarker = null;
            string uploadIdMarker = null;
            ListMultipartUploadsResponse response;
            do
            {
                response = await _s3.ListMultipartUploadsAsync(new ListMultipartUploadsRequest
                {
                    BucketName = _bucket,
                    Prefix = key,
                    KeyMarker = keyMarker,
                    UploadIdMarker = uploadIdMarker,
                }).ConfigureAwait(false);

                foreach (MultipartUpload upload in response.MultipartUploads ?? Enumerable.Empty<MultipartUpload>())
                {
                    if (string.Equals(upload.Key, key, StringComparison.Ordinal))
                        uploadIds.Add(upload.UploadId);
                }

                keyMarker = response.NextKeyMarker;
                uploadIdMarker = response.NextUploadIdMarker;
            } while (response.IsTruncated == true);

            return uploadIds;
        }

        private static (IAmazonS3 Client, string Bucket) CreateS3ClientAndBucket()
        {
            string bucket = FirstNonEmpty(
                Environment.GetEnvironmentVariable("S3__BucketName"),
                Environment.GetEnvironmentVariable("S3:BucketName"),
                "storage-live")!;
            string region = FirstNonEmpty(
                Environment.GetEnvironmentVariable("S3__Region"),
                Environment.GetEnvironmentVariable("S3:Region"),
                "us-east-1")!;
            string accessKey = FirstNonEmpty(
                Environment.GetEnvironmentVariable("S3__AccessKeyId"),
                Environment.GetEnvironmentVariable("S3:AccessKeyId"),
                "test")!;
            string secretKey = FirstNonEmpty(
                Environment.GetEnvironmentVariable("S3__SecretAccessKey"),
                Environment.GetEnvironmentVariable("S3:SecretAccessKey"),
                "test")!;
            string serviceUrl = FirstNonEmpty(
                Environment.GetEnvironmentVariable("S3__ServiceUrl"),
                Environment.GetEnvironmentVariable("S3:ServiceUrl"),
                Environment.GetEnvironmentVariable("S3__Endpoint"),
                Environment.GetEnvironmentVariable("S3:Endpoint"),
                "http://127.0.0.1:19090")!;

            var config = new AmazonS3Config
            {
                ServiceURL = serviceUrl.Trim().TrimEnd('/'),
                ForcePathStyle = true,
                AuthenticationRegion = region,
            };
            var client = new AmazonS3Client(new BasicAWSCredentials(accessKey, secretKey), config);
            return (client, bucket);
        }

        private static string FirstNonEmpty(params string[] values)
            => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
    }
}
