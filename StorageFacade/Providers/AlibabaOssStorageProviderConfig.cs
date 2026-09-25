// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage
{
    using System;

    /// <summary>
    /// Configuration for the Alibaba Cloud OSS provider.
    /// Prefer the endpoint+bucket constructor so access keys come from the environment
    /// (or an injected <c>ICredentialsProvider</c>); do not embed keys in source.
    /// </summary>
    public class AlibabaOssStorageProviderConfig : IStorageProviderConfig
    {
        /// <summary>
        /// Creates an OSS configuration that resolves credentials from the environment:
        /// <c>ALIBABA_CLOUD_ACCESS_KEY_ID</c> / <c>ALIBABA_CLOUD_ACCESS_KEY_SECRET</c>
        /// (optional <c>ALIBABA_CLOUD_SECURITY_TOKEN</c>), or the legacy
        /// <c>OSS_ACCESS_KEY_ID</c> / <c>OSS_ACCESS_KEY_SECRET</c> pair.
        /// </summary>
        /// <param name="endpoint">OSS endpoint host or URL (e.g. <c>oss-us-west-1.aliyuncs.com</c>).</param>
        /// <param name="bucketName">Bucket name.</param>
        public AlibabaOssStorageProviderConfig(string endpoint, string bucketName)
        {
            Init(endpoint, bucketName, accessKeyId: null, accessKeySecret: null, securityToken: null);
        }

        /// <summary>
        /// Creates an OSS configuration with explicit keys. Prefer the endpoint+bucket
        /// constructor so keys come from the environment or a secret store at runtime.
        /// Session/STS tokens do <strong>not</strong> auto-refresh.
        /// </summary>
        /// <param name="endpoint">OSS endpoint host or URL.</param>
        /// <param name="accessKeyId">Access key id (from secret store / env at runtime).</param>
        /// <param name="accessKeySecret">Access key secret.</param>
        /// <param name="bucketName">Bucket name.</param>
        /// <param name="securityToken">Optional STS security token.</param>
        public AlibabaOssStorageProviderConfig(
            string endpoint,
            string accessKeyId,
            string accessKeySecret,
            string bucketName,
            string securityToken = null)
        {
            if (string.IsNullOrWhiteSpace(accessKeyId))
                throw new ArgumentException("Access key id is required.", nameof(accessKeyId));
            if (string.IsNullOrWhiteSpace(accessKeySecret))
                throw new ArgumentException("Access key secret is required.", nameof(accessKeySecret));

            Init(endpoint, bucketName, accessKeyId, accessKeySecret, securityToken);
        }

        private void Init(
            string endpoint,
            string bucketName,
            string accessKeyId,
            string accessKeySecret,
            string securityToken)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
                throw new ArgumentException("Endpoint is required.", nameof(endpoint));
            if (string.IsNullOrWhiteSpace(bucketName))
                throw new ArgumentException("Bucket name is required.", nameof(bucketName));

            Endpoint = NormalizeEndpoint(endpoint);
            AccessKeyId = accessKeyId;
            AccessKeySecret = accessKeySecret;
            SecurityToken = securityToken;
            BucketName = bucketName.Trim();
            StorageFacadeType = StorageFacadeType.AlibabaOssStore;
            Name = $"oss://{BucketName}".ToLowerInvariant();
        }

        /// <inheritdoc/>
        public string Name { get; private set; }

        /// <inheritdoc/>
        public StorageFacadeType StorageFacadeType { get; private set; }

        internal string Endpoint { get; private set; }

        /// <summary>Null when using environment credential resolution.</summary>
        internal string AccessKeyId { get; private set; }

        /// <summary>Null when using environment credential resolution.</summary>
        internal string AccessKeySecret { get; private set; }

        internal string SecurityToken { get; private set; }

        internal string BucketName { get; private set; }

        private static string NormalizeEndpoint(string endpoint)
        {
            string value = endpoint.Trim();
            if (!value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                && !value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                value = "https://" + value;
            }

            return value.TrimEnd('/');
        }
    }
}
