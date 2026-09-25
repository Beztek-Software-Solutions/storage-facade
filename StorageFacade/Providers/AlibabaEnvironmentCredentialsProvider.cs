// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage.Providers
{
    using System;
    using Aliyun.OSS.Common.Authentication;

    /// <summary>
    /// Resolves Alibaba Cloud credentials from environment variables
    /// (no keys embedded in configuration).
    /// </summary>
    internal sealed class AlibabaEnvironmentCredentialsProvider : ICredentialsProvider
    {
        internal const string AccessKeyIdEnv = "ALIBABA_CLOUD_ACCESS_KEY_ID";
        internal const string AccessKeySecretEnv = "ALIBABA_CLOUD_ACCESS_KEY_SECRET";
        internal const string SecurityTokenEnv = "ALIBABA_CLOUD_SECURITY_TOKEN";
        internal const string LegacyAccessKeyIdEnv = "OSS_ACCESS_KEY_ID";
        internal const string LegacyAccessKeySecretEnv = "OSS_ACCESS_KEY_SECRET";
        internal const string LegacySecurityTokenEnv = "OSS_SECURITY_TOKEN";

        public void SetCredentials(ICredentials creds)
            => throw new NotSupportedException("Environment credentials are read-only.");

        public ICredentials GetCredentials()
        {
            string accessKeyId = FirstNonEmpty(
                Environment.GetEnvironmentVariable(AccessKeyIdEnv),
                Environment.GetEnvironmentVariable(LegacyAccessKeyIdEnv));
            string accessKeySecret = FirstNonEmpty(
                Environment.GetEnvironmentVariable(AccessKeySecretEnv),
                Environment.GetEnvironmentVariable(LegacyAccessKeySecretEnv));
            string token = FirstNonEmpty(
                Environment.GetEnvironmentVariable(SecurityTokenEnv),
                Environment.GetEnvironmentVariable(LegacySecurityTokenEnv));

            if (string.IsNullOrWhiteSpace(accessKeyId) || string.IsNullOrWhiteSpace(accessKeySecret))
            {
                throw new InvalidOperationException(
                    "Alibaba OSS credentials were not configured. Set "
                    + $"{AccessKeyIdEnv} and {AccessKeySecretEnv} "
                    + $"(or legacy {LegacyAccessKeyIdEnv} / {LegacyAccessKeySecretEnv}), "
                    + "or pass explicit keys from a secret store at runtime.");
            }

            return new DefaultCredentials(accessKeyId, accessKeySecret, token);
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            }

            return null;
        }
    }
}
