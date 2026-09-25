// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage.Providers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Security.Cryptography;
    using System.Threading.Tasks;
    using Amazon.S3;
    using Amazon.S3.Model;
    using Amazon.S3.Transfer;
    using Beztek.Facade.Storage;

    /// <summary>
    /// Storage provider for Amazon S3.
    /// </summary>
    internal class AwsS3StorageProvider : IStorageProvider, IDisposable
    {
        private AwsS3StorageProviderConfig AwsS3StorageProviderConfig { get; }
        private IAmazonS3 AwsS3Client;
        private ITransferUtility TransferUtility;
        private bool _disposed;

        internal AwsS3StorageProvider(AwsS3StorageProviderConfig awsS3StorageProviderConfig)
            : this(
                awsS3StorageProviderConfig,
                awsS3StorageProviderConfig.S3ClientCreator.CreateClient(awsS3StorageProviderConfig))
        {
        }

        internal AwsS3StorageProvider(AwsS3StorageProviderConfig awsS3StorageProviderConfig, IAmazonS3 awsS3Client)
            : this(awsS3StorageProviderConfig, awsS3Client, new TransferUtility(awsS3Client))
        {
        }

        internal AwsS3StorageProvider(
            AwsS3StorageProviderConfig awsS3StorageProviderConfig,
            IAmazonS3 awsS3Client,
            ITransferUtility transferUtility)
        {
            AwsS3StorageProviderConfig = awsS3StorageProviderConfig;
            AwsS3Client = awsS3Client;
            TransferUtility = transferUtility;
        }

        public string GetName()
        {
            return AwsS3StorageProviderConfig.Name;
        }

        public new StorageFacadeType GetType()
        {
            return AwsS3StorageProviderConfig.StorageFacadeType;
        }

        public IEnumerable<StorageInfo> EnumerateStorageInfo(string logicalPath, bool isRecursive = false, StorageFilter storageFilter = null)
        {
            string prefix = $"{GetRelativePath(logicalPath)}";
            if ("/".Equals(prefix)) prefix = "";
            var request = new ListObjectsV2Request
            {
                BucketName = AwsS3StorageProviderConfig.BucketName,
                Prefix = prefix
            };

            ListObjectsV2Response response;
            do
            {
                Task<ListObjectsV2Response> task = AwsS3Client.ListObjectsV2Async(request);
                task.Wait();
                response = task.Result;

                foreach (var s3Object in response.S3Objects)
                {
                    if (isRecursive)
                    {
                        yield return GetStorageInfo(s3Object);
                    }
                    else
                    {
                        string currPath = s3Object.Key;
                        if (currPath == $"{prefix}/{CloudLogicalPath.GetLeafName(currPath)}")
                        {
                            yield return GetStorageInfo(s3Object);
                        }
                    }
                }

                request.ContinuationToken = response.NextContinuationToken;

            } while ((bool)response.IsTruncated);
        }

        public StorageInfo GetStorageInfo(string logicalPath)
        {
            string name = CloudLogicalPath.GetLeafName(logicalPath);
            string relativePath = CloudLogicalPath.GetRelativePath(GetName(), logicalPath);
            Task<GetObjectMetadataResponse> task = AwsS3Client.GetObjectMetadataAsync(AwsS3StorageProviderConfig.BucketName, relativePath);
            task.Wait();
            GetObjectMetadataResponse response = task.Result;
            return new StorageInfo
            {
                IsFile = true,
                Name = name,
                LogicalPath = logicalPath,
                Timestamp = (DateTime)response.LastModified,
                SizeBytes = (long)response.ContentLength
            };
        }

        public async Task<Stream> ReadStorageAsync(StorageInfo storageInfo)
        {
            return await ReadStorageAsync(storageInfo.LogicalPath);
        }

        public async Task WriteStorageAsync(string logicalPath, Stream inputStream, bool createParentDirectories = false)
        {
            string key = CloudLogicalPath.GetRelativePath(GetName(), logicalPath);

            // Known length: simple PutObject with ContentLength (AWSSDK.S3 4.x requires it).
            // Unknown length: TransferUtility multipart uploads in part-sized chunks without
            // buffering the entire object (PutObject chunked encoding still needs ContentLength
            // when the stream does not report Length).
            if (TryGetRemainingLength(inputStream, out long contentLength))
            {
                var putRequest = new PutObjectRequest
                {
                    BucketName = AwsS3StorageProviderConfig.BucketName,
                    Key = key,
                    InputStream = inputStream,
                    AutoCloseStream = false
                };
                putRequest.Headers.ContentLength = contentLength;
                await AwsS3Client.PutObjectAsync(putRequest).ConfigureAwait(false);
                return;
            }

            var uploadRequest = new TransferUtilityUploadRequest
            {
                BucketName = AwsS3StorageProviderConfig.BucketName,
                Key = key,
                InputStream = inputStream,
                AutoCloseStream = false
            };
            await TransferUtility.UploadAsync(uploadRequest).ConfigureAwait(false);
        }

        private static bool TryGetRemainingLength(Stream stream, out long contentLength)
        {
            contentLength = 0;
            if (!stream.CanSeek)
                return false;

            try
            {
                contentLength = stream.Length - stream.Position;
                return true;
            }
            catch (NotSupportedException)
            {
                return false;
            }
        }

        public async Task DeleteStorageAsync(string logicalPath)
        {
            var deleteRequest = new DeleteObjectRequest
            {
                BucketName = AwsS3StorageProviderConfig.BucketName,
                Key = CloudLogicalPath.GetRelativePath(GetName(), logicalPath)
            };

            await AwsS3Client.DeleteObjectAsync(deleteRequest);
        }

        public async Task<string> ComputeMD5Checksum(string logicalPath)
        {
            using var md5 = MD5.Create();
            using var stream = await ReadStorageAsync(logicalPath);
            return Convert.ToBase64String(md5.ComputeHash(stream));
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            (TransferUtility as IDisposable)?.Dispose();
            AwsS3Client?.Dispose();
        }

        private StorageInfo GetStorageInfo(S3Object s3Object)
        {
            return new StorageInfo
            {
                IsFile = true,
                Name = CloudLogicalPath.GetLeafName(s3Object.Key),
                LogicalPath = $"{GetName()}/{s3Object.Key}",
                Timestamp = (DateTime)s3Object.LastModified,
                SizeBytes = (long)s3Object.Size
            };
        }

        private string GetRelativePath(string logicalPath)
            => CloudLogicalPath.GetRelativePath(GetName(), logicalPath);

        private async Task<Stream> ReadStorageAsync(string logicalPath)
        {
            var getObjectRequest = new GetObjectRequest
            {
                BucketName = AwsS3StorageProviderConfig.BucketName,
                Key = CloudLogicalPath.GetRelativePath(GetName(), logicalPath)
            };

            var response = await AwsS3Client.GetObjectAsync(getObjectRequest);
            return response.ResponseStream;
        }
    }
}
