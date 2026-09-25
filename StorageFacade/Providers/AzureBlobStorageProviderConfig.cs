// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage
{
    using System;
    using System.Collections.Generic;

    /// <summary>Configuration for the Azure Blob Storage provider.</summary>
    public class AzureBlobStorageProviderConfig : IStorageProviderConfig
    {
        /// <inheritdoc/>
        public string Name { get; }

        /// <inheritdoc/>
        public StorageFacadeType StorageFacadeType { get; }

        internal Uri BlobUri { get; }

        internal string AccountName { get; }

        internal string AccountKey { get; }

        internal string ContainerName { get; }

        internal bool IsHierarchicalNamespace { get; }

        /// <summary>
        /// Creates a configuration using account name/key credentials.
        /// <see cref="Name"/> is normalized to <c>https://{domainName}/{containerName}</c>.
        /// </summary>
        /// <param name="domainName">Blob endpoint host, e.g. <c>myaccount.blob.core.windows.net</c>.</param>
        /// <param name="accountKey">Storage account key.</param>
        /// <param name="containerName">Target container name.</param>
        /// <param name="isHierarchicalNamespace">When true, uses hierarchical (ADLS Gen2) listing.</param>
        public AzureBlobStorageProviderConfig(string domainName, string accountKey, string containerName, bool isHierarchicalNamespace = false)
        {
            this.StorageFacadeType = StorageFacadeType.AzureBlobStore;
            this.AccountName = domainName.Split(".")[0];
            this.AccountKey = accountKey;
            this.ContainerName = containerName;
            this.Name = $"https://{domainName}/{this.ContainerName}".ToLower();
            this.BlobUri = new Uri($"https://{domainName}".ToLower());
            this.IsHierarchicalNamespace = isHierarchicalNamespace;
            this.ConnectionString = null;
        }

        /// <summary>
        /// Creates a configuration from a container SAS URI.
        /// </summary>
        /// <param name="blobUri">
        /// URI of the form
        /// <c>https://{account}.blob.core.windows.net/{container}/?{SASToken}</c>.
        /// </param>
        /// <param name="isHierarchicalNamespace">When true, uses hierarchical (ADLS Gen2) listing.</param>
        public AzureBlobStorageProviderConfig(Uri blobUri, bool isHierarchicalNamespace = false)
        {
            this.StorageFacadeType = StorageFacadeType.AzureBlobStore;
            this.BlobUri = blobUri;
            this.AccountName = GetAccountNameFromBlobUri(blobUri);
            this.ContainerName = GetContainerNameFromBlobUri(blobUri);
            string SASToken = this.GetSASTokenFromBlobUri(blobUri);
            this.Name = $"https://{this.AccountName}.blob.core.windows.net/{this.ContainerName}".ToLower();
            this.IsHierarchicalNamespace = isHierarchicalNamespace;
            this.ConnectionString = null;
        }

        /// <summary>
        /// Creates a configuration with an explicit blob <strong>service</strong> URI and account key
        /// (e.g. Azurite <c>http://127.0.0.1:10000/devstoreaccount1</c>).
        /// </summary>
        /// <param name="blobServiceUri">Blob service endpoint (scheme + host[/account path]).</param>
        /// <param name="accountKey">Storage account key.</param>
        /// <param name="containerName">Target container name.</param>
        /// <param name="isHierarchicalNamespace">When true, uses hierarchical (ADLS Gen2) listing.</param>
        public AzureBlobStorageProviderConfig(
            Uri blobServiceUri,
            string accountKey,
            string containerName,
            bool isHierarchicalNamespace = false)
        {
            ArgumentNullException.ThrowIfNull(blobServiceUri);
            if (string.IsNullOrWhiteSpace(accountKey))
                throw new ArgumentException("Account key is required.", nameof(accountKey));
            if (string.IsNullOrWhiteSpace(containerName))
                throw new ArgumentException("Container name is required.", nameof(containerName));

            StorageFacadeType = StorageFacadeType.AzureBlobStore;
            BlobUri = blobServiceUri;
            AccountKey = accountKey;
            ContainerName = containerName;
            AccountName = GetAccountNameFromServiceUri(blobServiceUri);
            string serviceRoot = blobServiceUri.GetLeftPart(UriPartial.Path).TrimEnd('/');
            Name = $"{serviceRoot}/{containerName}".ToLowerInvariant();
            IsHierarchicalNamespace = isHierarchicalNamespace;
            ConnectionString = null;
        }

        /// <summary>
        /// Creates a configuration from an Azure Storage connection string and container name
        /// (e.g. Azurite development connection string with <c>BlobEndpoint</c>).
        /// </summary>
        /// <param name="connectionString">
        /// Storage connection string including <c>AccountName</c>, <c>AccountKey</c>, and typically
        /// <c>BlobEndpoint</c> for stand-ins.
        /// </param>
        /// <param name="containerName">Target container name.</param>
        /// <param name="isHierarchicalNamespace">When true, uses hierarchical (ADLS Gen2) listing.</param>
        public AzureBlobStorageProviderConfig(
            string connectionString,
            string containerName,
            bool isHierarchicalNamespace = false)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Connection string is required.", nameof(connectionString));
            if (string.IsNullOrWhiteSpace(containerName))
                throw new ArgumentException("Container name is required.", nameof(containerName));

            StorageFacadeType = StorageFacadeType.AzureBlobStore;
            ContainerName = containerName;
            IsHierarchicalNamespace = isHierarchicalNamespace;
            ConnectionString = connectionString.Trim();

            Dictionary<string, string> parts = ParseConnectionString(ConnectionString);
            if (!parts.TryGetValue("AccountName", out string accountName) || string.IsNullOrWhiteSpace(accountName))
                throw new ArgumentException("Connection string must include AccountName.", nameof(connectionString));
            if (!parts.TryGetValue("AccountKey", out string accountKey) || string.IsNullOrWhiteSpace(accountKey))
                throw new ArgumentException("Connection string must include AccountKey.", nameof(connectionString));

            AccountName = accountName;
            AccountKey = accountKey;
            (BlobUri, Name) = ResolveEndpoint(parts, accountName, containerName);
        }

        // Internal

        internal string ConnectionString { get; }

        private static (Uri BlobUri, string Name) ResolveEndpoint(
            Dictionary<string, string> parts,
            string accountName,
            string containerName)
        {
            if (parts.TryGetValue("BlobEndpoint", out string blobEndpoint) && !string.IsNullOrWhiteSpace(blobEndpoint))
            {
                var blobUri = new Uri(blobEndpoint.TrimEnd('/'));
                string name = $"{blobUri.GetLeftPart(UriPartial.Path).TrimEnd('/')}/{containerName}".ToLowerInvariant();
                return (blobUri, name);
            }

            var publicUri = new Uri($"https://{accountName}.blob.core.windows.net".ToLowerInvariant());
            string publicName = $"https://{accountName}.blob.core.windows.net/{containerName}".ToLowerInvariant();
            return (publicUri, publicName);
        }

        private static Dictionary<string, string> ParseConnectionString(string connectionString)
        {
            var parts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string segment in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                int eq = segment.IndexOf('=');
                if (eq <= 0)
                    continue;
                parts[segment[..eq].Trim()] = segment[(eq + 1)..].Trim();
            }

            return parts;
        }

        private static string GetAccountNameFromServiceUri(Uri blobServiceUri)
        {
            string path = blobServiceUri.AbsolutePath.Trim('/');
            if (!string.IsNullOrEmpty(path))
                return path.Split('/')[0];

            string host = blobServiceUri.Host;
            int dot = host.IndexOf('.');
            return dot > 0 ? host[..dot] : host;
        }

        // This returns the account name from the blob Uri of the format: https://<account-name>.blob.core.windows.net/<container-name>/?<SASToken>
        private string GetAccountNameFromBlobUri(Uri blobUri)
        {
            return blobUri.ToString().Split("?")[0].Split("/")[2].Split(".")[0];
        }

        // This returns the account name from the blob Uri of the format: https://<account-name>.blob.core.windows.net/<container-name>/?<SASToken>
        private string GetContainerNameFromBlobUri(Uri blobUri)
        {
            return blobUri.ToString().Split("?")[0].Split("/")[3];
        }

        // This returns the account name from the blob Uri of the format: https://<account-name>.blob.core.windows.net/<container-name>/?<SASToken>
        private string GetSASTokenFromBlobUri(Uri blobUri)
        {
            return blobUri.ToString().Split("?")[1];
        }
    }
}
