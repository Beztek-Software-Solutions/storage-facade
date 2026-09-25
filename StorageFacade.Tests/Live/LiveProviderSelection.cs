// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage.Tests.Live
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Selects which providers run live tests via <c>STORAGEFACADE_LIVE_PROVIDERS</c>.
    /// <para>
    /// Unset / empty → <c>file</c> only (default local path, no containers).
    /// Examples: <c>file</c> | <c>file,s3,gcs</c> | <c>all</c>
    /// </para>
    /// Containers / env are prepared by the Makefile; this suite only connects.
    /// </summary>
    public static class LiveProviderSelection
    {
        public const string EnvVar = "STORAGEFACADE_LIVE_PROVIDERS";

        private static readonly StorageFacadeType[] AllProviders =
        {
            StorageFacadeType.LocalFileStore,
            StorageFacadeType.AmazonS3Store,
            StorageFacadeType.AzureBlobStore,
            StorageFacadeType.SMBNetworkStore,
            StorageFacadeType.GoogleCloudStorageStore,
            StorageFacadeType.AlibabaOssStore,
        };

        /// <summary>Providers for this process (always at least File when unset).</summary>
        public static IReadOnlyList<StorageFacadeType> Resolve()
        {
            string raw = Environment.GetEnvironmentVariable(EnvVar);
            if (string.IsNullOrWhiteSpace(raw))
                return new[] { StorageFacadeType.LocalFileStore };

            var selected = new List<StorageFacadeType>();
            foreach (string token in raw.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string t = token.Trim().ToLowerInvariant();
                if (t is "all" or "*")
                    return AllProviders.ToList();

                if (TryParse(t, out StorageFacadeType provider) && !selected.Contains(provider))
                    selected.Add(provider);
                else
                    throw new ArgumentException(
                        $"Unknown provider '{token}' in {EnvVar}. " +
                        "Use: all | file | s3 | azure | smb | gcs | oss (comma-separated).");
            }

            if (selected.Count == 0)
                selected.Add(StorageFacadeType.LocalFileStore);

            return selected;
        }

        public static bool TryParse(string token, out StorageFacadeType provider)
        {
            switch (token.Trim().ToLowerInvariant())
            {
                case "file":
                case "local":
                case "localfile":
                case "localfilestore":
                    provider = StorageFacadeType.LocalFileStore;
                    return true;
                case "s3":
                case "amazon":
                case "amazons3":
                case "amazons3store":
                    provider = StorageFacadeType.AmazonS3Store;
                    return true;
                case "azure":
                case "blob":
                case "azureblob":
                case "azureblobstore":
                    provider = StorageFacadeType.AzureBlobStore;
                    return true;
                case "smb":
                case "cifs":
                case "smbnetwork":
                case "smbnetworkstore":
                    provider = StorageFacadeType.SMBNetworkStore;
                    return true;
                case "gcs":
                case "google":
                case "gcp":
                case "googlecloud":
                case "googlecloudstorage":
                case "googlecloudstoragestore":
                    provider = StorageFacadeType.GoogleCloudStorageStore;
                    return true;
                case "oss":
                case "alibaba":
                case "aliyun":
                case "alibabaoss":
                case "alibabaossstore":
                    provider = StorageFacadeType.AlibabaOssStore;
                    return true;
                default:
                    provider = default;
                    return false;
            }
        }
    }
}
