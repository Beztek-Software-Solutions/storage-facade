// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage.Providers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Security.Cryptography;
    using System.Threading.Tasks;
    using Amazon;
    using Amazon.Runtime;
    using Amazon.S3;
    using Amazon.S3.Model;
    using Amazon.S3.Transfer;
    using Beztek.Facade.Storage;

    /// <summary>
    /// Implements the storage provider for Azure Blob Storage
    /// </summary>
    internal class AwsS3StorageProvider : IStorageProvider
    {
        private AwsS3StorageProviderConfig AwsS3StorageProviderConfig { get; }
        private IAmazonS3 AwsS3Client;

        internal AwsS3StorageProvider(AwsS3StorageProviderConfig awsS3StorageProviderConfig)
        {
            AwsS3StorageProviderConfig = awsS3StorageProviderConfig;
            var awsCredentials = new BasicAWSCredentials(awsS3StorageProviderConfig.AccessKeyId, awsS3StorageProviderConfig.SecretAccessKey);
            var regionEndpoint = RegionEndpoint.GetBySystemName(awsS3StorageProviderConfig.RegionName);
            AwsS3Client = new AmazonS3Client(awsCredentials, regionEndpoint);
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
                Prefix = prefix // Start from the specified subfolder
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
                        if (currPath == $"{prefix}/{GetNameFromLogicalPath(currPath)}") {
                            yield return GetStorageInfo(s3Object);
                        }
                    }
                }

                // Set continuation token for the next batch
                request.ContinuationToken = response.NextContinuationToken;

            } while (response.IsTruncated); // Continue if there are more objects
        }

        public StorageInfo GetStorageInfo(string logicalPath)
        {
            string name = GetNameFromLogicalPath(logicalPath);
            string relativePath = $"{GetRelativePath(logicalPath)}";
            Task<GetObjectMetadataResponse> task = AwsS3Client.GetObjectMetadataAsync(AwsS3StorageProviderConfig.BucketName, relativePath);
            task.Wait();
            GetObjectMetadataResponse response = task.Result;
            return new StorageInfo
            {
                IsFile = true,
                Name = name,
                LogicalPath = logicalPath,
                Timestamp = response.LastModified,
                SizeBytes = response.ContentLength
            };
        }

        public async Task<Stream> ReadStorageAsync(StorageInfo storageInfo)
        {
            return await ReadStorageAsync(storageInfo.LogicalPath);
        }

        public async Task WriteStorageAsync(string logicalPath, Stream inputStream, bool createParentDirectories = false)
        {
            // Use TransferUtility for efficient upload
            var transferUtility = new TransferUtility(AwsS3Client);

            // Upload the stream to S3 using TransferUtility
            await transferUtility.UploadAsync(inputStream, AwsS3StorageProviderConfig.BucketName, GetRelativePath(logicalPath));
        }

        public async Task DeleteStorageAsync(string logicalPath)
        {
            // Create delete request
            var deleteRequest = new DeleteObjectRequest
            {
                BucketName = AwsS3StorageProviderConfig.BucketName,
                Key = GetRelativePath(logicalPath)
            };

            // Execute delete
            await AwsS3Client.DeleteObjectAsync(deleteRequest);
        }

        public async Task<string> ComputeMD5Checksum(string logicalPath)
        {
            using var md5 = MD5.Create();
            using var stream = await ReadStorageAsync(logicalPath);
            return Convert.ToBase64String(md5.ComputeHash(stream));
        }

        // Internal

        private StorageInfo GetStorageInfo(S3Object s3Object)
        {
            return new StorageInfo
            {
                IsFile = true,
                Name = GetNameFromLogicalPath(s3Object.Key),
                LogicalPath = $"{GetName()}/{s3Object.Key}",
                Timestamp = s3Object.LastModified,
                SizeBytes = s3Object.Size
            };
        }

        private string GetNameFromLogicalPath(string logicalPath)
        {
            int index = logicalPath.LastIndexOf("/") + 1;
            return logicalPath[index..];
        }

        // This returns the relative path from the logical path
        private string GetRelativePath(string logicalPath)
        {
            if (!logicalPath.EndsWith("/")) logicalPath = $"{logicalPath}/";
            int uriLength = GetName().Length;
            int logicalPathLength = logicalPath.Length;
            string currPath = logicalPath.Substring(uriLength + 1, logicalPathLength - uriLength - 1);
            if (currPath.StartsWith("/")) currPath = currPath[1..];
            if (currPath.EndsWith("/")) currPath = currPath[..^1];
            return currPath;
        }

        private async Task<Stream> ReadStorageAsync(string logicalPath)
        {
            // Get the object from S3 as a stream
            var getObjectRequest = new GetObjectRequest
            {
                BucketName = AwsS3StorageProviderConfig.BucketName,
                Key = GetRelativePath(logicalPath)
            };

            var response = await AwsS3Client.GetObjectAsync(getObjectRequest);
            return response.ResponseStream;
        }
    }
}
