// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage
{
    using Beztek.Facade.Storage.Providers;

    /// <summary>Configuration for the Amazon S3 storage provider.</summary>
    public class AwsS3StorageProviderConfig : IStorageProviderConfig
    {
        /// <summary>
        /// Creates an S3 provider configuration with explicit access keys.
        /// Prefer the region+bucket constructor on AWS hosts
        /// so the SDK default credential chain (instance role, IRSA, env, etc.) is used.
        /// </summary>
        public AwsS3StorageProviderConfig(
            string accessKeyId,
            string secretAccessKey,
            string regionName,
            string bucketName)
        {
            Init(regionName, bucketName, serviceUrl: null, accessKeyId, secretAccessKey, sessionToken: null);
        }

        /// <summary>
        /// Creates an S3 provider configuration with explicit credentials and an optional
        /// STS session token. Session credentials do <strong>not</strong> auto-refresh;
        /// for IAM roles use the region+bucket constructor instead.
        /// </summary>
        /// <param name="accessKeyId">AWS access key id.</param>
        /// <param name="secretAccessKey">AWS secret access key.</param>
        /// <param name="regionName">AWS region system name (e.g. us-east-1).</param>
        /// <param name="bucketName">S3 bucket name.</param>
        /// <param name="sessionToken">Optional STS session token (temporary credentials).</param>
        /// <param name="serviceUrl">Optional custom endpoint (e.g. MinIO / LocalStack).</param>
        public AwsS3StorageProviderConfig(
            string accessKeyId,
            string secretAccessKey,
            string regionName,
            string bucketName,
            string sessionToken,
            string serviceUrl = null)
        {
            Init(regionName, bucketName, serviceUrl, accessKeyId, secretAccessKey, sessionToken);
        }

        /// <summary>
        /// Creates an S3 provider configuration that uses the AWS SDK default credential
        /// chain (EC2/ECS instance role, IRSA, environment variables, shared profile, etc.).
        /// </summary>
        /// <param name="regionName">AWS region system name (e.g. us-east-1).</param>
        /// <param name="bucketName">S3 bucket name.</param>
        /// <param name="serviceUrl">Optional custom endpoint (e.g. MinIO / LocalStack).</param>
        public AwsS3StorageProviderConfig(
            string regionName,
            string bucketName,
            string serviceUrl = null)
        {
            Init(regionName, bucketName, serviceUrl, accessKeyId: null, secretAccessKey: null, sessionToken: null);
        }

        private void Init(
            string regionName,
            string bucketName,
            string serviceUrl,
            string accessKeyId,
            string secretAccessKey,
            string sessionToken)
        {
            StorageFacadeType = StorageFacadeType.AmazonS3Store;
            RegionName = regionName ?? string.Empty;
            BucketName = bucketName;
            ServiceUrl = serviceUrl;
            AccessKeyId = accessKeyId;
            SecretAccessKey = secretAccessKey;
            SessionToken = sessionToken;
            Name = $"s3://{bucketName}".ToLowerInvariant();
        }

        /// <inheritdoc/>
        public string Name { get; private set; }

        /// <inheritdoc/>
        public StorageFacadeType StorageFacadeType { get; private set; }

        internal string AccessKeyId { get; private set; }

        internal string SecretAccessKey { get; private set; }

        /// <summary>
        /// Optional STS session token. Used only with explicit access/secret keys;
        /// does not refresh when the token expires.
        /// </summary>
        internal string SessionToken { get; private set; }

        internal string BucketName { get; private set; }

        internal string RegionName { get; private set; }

        /// <summary>Custom S3 endpoint (MinIO / LocalStack / VPC). Null = regional AWS.</summary>
        internal string ServiceUrl { get; private set; }

        internal S3ClientCreator S3ClientCreator { get; set; } = new S3ClientCreator();
    }
}
