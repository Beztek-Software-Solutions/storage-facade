// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage.Providers
{
    using Amazon;
    using Amazon.Runtime;
    using Amazon.S3;

    /// <summary>
    /// Creates <see cref="IAmazonS3"/> clients. Overridable in tests.
    /// Uses the default AWS credential chain unless explicit keys are set on the config.
    /// </summary>
    internal class S3ClientCreator
    {
        internal virtual IAmazonS3 CreateClient(AwsS3StorageProviderConfig config)
        {
            var s3Config = new AmazonS3Config();
            if (!string.IsNullOrWhiteSpace(config.ServiceUrl))
            {
                s3Config.ServiceURL = config.ServiceUrl.Trim().TrimEnd('/');
                s3Config.ForcePathStyle = true;
                if (!string.IsNullOrWhiteSpace(config.RegionName))
                {
                    s3Config.AuthenticationRegion = config.RegionName;
                }
            }
            else
            {
                s3Config.RegionEndpoint = RegionEndpoint.GetBySystemName(config.RegionName);
            }

            if (!string.IsNullOrEmpty(config.AccessKeyId) && !string.IsNullOrEmpty(config.SecretAccessKey))
            {
                AWSCredentials creds = string.IsNullOrEmpty(config.SessionToken)
                    ? new BasicAWSCredentials(config.AccessKeyId, config.SecretAccessKey)
                    : new SessionAWSCredentials(config.AccessKeyId, config.SecretAccessKey, config.SessionToken);
                return new AmazonS3Client(creds, s3Config);
            }

            return new AmazonS3Client(s3Config);
        }
    }
}
