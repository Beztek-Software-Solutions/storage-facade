namespace Beztek.Facade.Storage
{
    using System;
    using Amazon;

    /// <summary>
    /// Implements the storage provider configuration for Azure Blob Storage
    /// </summary>
    public class AwsS3StorageProviderConfig : IStorageProviderConfig
    {

        public AwsS3StorageProviderConfig(string accessKeyId, string secretAccessKey, string regionName, string bucketName)
        {
            this.StorageFacadeType = StorageFacadeType.AmazonS3Store;
            this.AccessKeyId = accessKeyId;
            this.SecretAccessKey = secretAccessKey;
            this.RegionName = regionName;
            this.BucketName = bucketName;
            this.Name = $"s3://{bucketName}".ToLower();
        }

        public string Name { get; }

        public StorageFacadeType StorageFacadeType { get; }

        internal string AccessKeyId{ get; }

        internal string SecretAccessKey { get; }

        internal string BucketName { get; }

        internal string RegionName { get; }
    }
}
