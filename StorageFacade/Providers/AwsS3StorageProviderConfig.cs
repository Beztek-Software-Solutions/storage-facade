namespace Beztek.Facade.Storage
{
    using System;
    using Amazon;

    /// <summary>Configuration for the Amazon S3 storage provider.</summary>
    public class AwsS3StorageProviderConfig : IStorageProviderConfig
    {

        /// <summary>Creates an S3 provider configuration. <see cref="Name"/> is normalized to `s3://{bucket}`.</summary>
        public AwsS3StorageProviderConfig(string accessKeyId, string secretAccessKey, string regionName, string bucketName)
        {
            this.StorageFacadeType = StorageFacadeType.AmazonS3Store;
            this.AccessKeyId = accessKeyId;
            this.SecretAccessKey = secretAccessKey;
            this.RegionName = regionName;
            this.BucketName = bucketName;
            this.Name = $"s3://{bucketName}".ToLower();
        }

        /// <inheritdoc/>
        public string Name { get; }

        /// <inheritdoc/>
        public StorageFacadeType StorageFacadeType { get; }

        internal string AccessKeyId { get; }

        internal string SecretAccessKey { get; }

        internal string BucketName { get; }

        internal string RegionName { get; }
    }
}
