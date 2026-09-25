// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage.Tests.Live
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Net.Sockets;
    using System.Threading.Tasks;
    using Amazon.Runtime;
    using Amazon.S3;
    using Azure.Storage;
    using Azure.Storage.Blobs;
    using Beztek.Facade.Storage;
    using Beztek.Facade.Storage.Providers;

    /// <summary>
    /// Builds an <see cref="IStorageFacade"/> for a live provider using env written by Make.
    /// Does not start containers — hard-fails when a remote provider is selected but unreachable.
    /// </summary>
    public sealed class LiveProviderHost : IAsyncDisposable
    {
        private readonly string _tempRoot;

        private LiveProviderHost(
            StorageFacadeType providerType,
            IStorageFacade storage,
            string tempRoot,
            string pathPrefix)
        {
            ProviderType = providerType;
            Storage = storage;
            _tempRoot = tempRoot;
            PathPrefix = pathPrefix;
        }

        public StorageFacadeType ProviderType { get; }

        public IStorageFacade Storage { get; }

        /// <summary>
        /// Prefix for object paths (absolute dir for File; store name for S3/Azure/SMB).
        /// </summary>
        public string PathPrefix { get; }

        public static async Task<LiveProviderHost> StartAsync(StorageFacadeType providerType)
        {
            return providerType switch
            {
                StorageFacadeType.LocalFileStore => StartFile(),
                StorageFacadeType.AmazonS3Store => await StartS3Async().ConfigureAwait(false),
                StorageFacadeType.AzureBlobStore => await StartAzureAsync().ConfigureAwait(false),
                StorageFacadeType.SMBNetworkStore => StartSmb(),
                StorageFacadeType.GoogleCloudStorageStore => await StartGcsAsync().ConfigureAwait(false),
                StorageFacadeType.AlibabaOssStore => StartOss(),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(providerType),
                    providerType,
                    "ComboStore is not a live backend; use File/S3/Azure/SMB/GCS/OSS."),
            };
        }

        public string ObjectPath(string relativeName)
        {
            if (ProviderType == StorageFacadeType.LocalFileStore)
                return Path.Combine(PathPrefix, relativeName);

            if (ProviderType == StorageFacadeType.SMBNetworkStore)
                return PathPrefix.TrimEnd('\\') + @"\" + relativeName.Replace('/', '\\');

            return PathPrefix.TrimEnd('/') + "/" + relativeName.TrimStart('/');
        }

        public ValueTask DisposeAsync()
        {
            if (!string.IsNullOrEmpty(_tempRoot) && Directory.Exists(_tempRoot))
            {
                try { Directory.Delete(_tempRoot, recursive: true); }
                catch { /* best-effort */ }
            }

            return ValueTask.CompletedTask;
        }

        private static LiveProviderHost StartFile()
        {
            string root = Path.Combine(Path.GetTempPath(), "storage-facade-live-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            IStorageFacade storage = StorageFacadeFactory.GetStorageFacade(new FileStorageProviderConfig());
            return new LiveProviderHost(StorageFacadeType.LocalFileStore, storage, root, root);
        }

        private static async Task<LiveProviderHost> StartS3Async()
        {
            string bucket = FirstNonEmpty(
                Environment.GetEnvironmentVariable("S3__BucketName"),
                Environment.GetEnvironmentVariable("S3:BucketName"),
                "storage-live")!;
            string region = FirstNonEmpty(
                Environment.GetEnvironmentVariable("S3__Region"),
                Environment.GetEnvironmentVariable("S3:Region"),
                "us-east-1")!;
            string accessKey = FirstNonEmpty(
                Environment.GetEnvironmentVariable("S3__AccessKeyId"),
                Environment.GetEnvironmentVariable("S3:AccessKeyId"),
                "test")!;
            string secretKey = FirstNonEmpty(
                Environment.GetEnvironmentVariable("S3__SecretAccessKey"),
                Environment.GetEnvironmentVariable("S3:SecretAccessKey"),
                "test")!;
            string serviceUrl = FirstNonEmpty(
                Environment.GetEnvironmentVariable("S3__ServiceUrl"),
                Environment.GetEnvironmentVariable("S3:ServiceUrl"),
                Environment.GetEnvironmentVariable("S3__Endpoint"),
                Environment.GetEnvironmentVariable("S3:Endpoint"),
                "http://127.0.0.1:19090")!;

            EnsureHttpReachable(serviceUrl, "S3 (adobe/s3mock)", "make -- test --use-s3-container");

            await EnsureS3BucketExistsAsync(serviceUrl, region, accessKey, secretKey, bucket)
                .ConfigureAwait(false);

            var config = new AwsS3StorageProviderConfig(
                accessKey,
                secretKey,
                region,
                bucket,
                sessionToken: null,
                serviceUrl: serviceUrl);
            IStorageFacade storage = StorageFacadeFactory.GetStorageFacade(config);
            return new LiveProviderHost(StorageFacadeType.AmazonS3Store, storage, tempRoot: null, pathPrefix: config.Name);
        }

        private static async Task EnsureS3BucketExistsAsync(
            string serviceUrl,
            string region,
            string accessKey,
            string secretKey,
            string bucket)
        {
            var s3Config = new AmazonS3Config
            {
                ServiceURL = serviceUrl.Trim().TrimEnd('/'),
                ForcePathStyle = true,
                AuthenticationRegion = region,
            };
            using var client = new AmazonS3Client(new BasicAWSCredentials(accessKey, secretKey), s3Config);
            try
            {
                await client.PutBucketAsync(bucket).ConfigureAwait(false);
            }
            catch (AmazonS3Exception ex) when (
                ex.StatusCode == HttpStatusCode.Conflict
                || string.Equals(ex.ErrorCode, "BucketAlreadyOwnedByYou", StringComparison.OrdinalIgnoreCase)
                || string.Equals(ex.ErrorCode, "BucketAlreadyExists", StringComparison.OrdinalIgnoreCase))
            {
                // Already present — fine for live stand-ins.
            }
        }

        private static async Task<LiveProviderHost> StartAzureAsync()
        {
            string containerName = FirstNonEmpty(
                Environment.GetEnvironmentVariable("AZURE__ContainerName"),
                Environment.GetEnvironmentVariable("AZURE:ContainerName"),
                "storage-live")!;

            string connectionString = FirstNonEmpty(
                Environment.GetEnvironmentVariable("AZURE__ConnectionString"),
                Environment.GetEnvironmentVariable("AZURE:ConnectionString"));

            AzureBlobStorageProviderConfig config;
            if (!string.IsNullOrEmpty(connectionString))
            {
                config = new AzureBlobStorageProviderConfig(connectionString, containerName);
                EnsureHttpReachable(
                    config.BlobUri.GetLeftPart(UriPartial.Authority),
                    "Azurite",
                    "make -- test --use-azure-container");
            }
            else
            {
                string serviceUriText = FirstNonEmpty(
                    Environment.GetEnvironmentVariable("AZURE__BlobServiceUri"),
                    Environment.GetEnvironmentVariable("AZURE:BlobServiceUri"))
                    ?? throw new InvalidOperationException(
                        "Azure live tests require AZURE__ConnectionString or AZURE__BlobServiceUri "
                        + "(make test-azure / make -- test --use-azure-container).");

                string accountKey = FirstNonEmpty(
                    Environment.GetEnvironmentVariable("AZURE__AccountKey"),
                    Environment.GetEnvironmentVariable("AZURE:AccountKey"))
                    ?? throw new InvalidOperationException("Azure live tests require AZURE__AccountKey.");

                var serviceUri = new Uri(serviceUriText);
                EnsureHttpReachable(
                    serviceUri.GetLeftPart(UriPartial.Authority),
                    "Azurite",
                    "make test-azure / make -- test --use-azure-container");

                config = new AzureBlobStorageProviderConfig(serviceUri, accountKey, containerName);
            }

            // Ensure the blob container exists (Azurite starts empty aside from the account).
            BlobServiceClient service = !string.IsNullOrEmpty(config.ConnectionString)
                ? new BlobServiceClient(config.ConnectionString)
                : new BlobServiceClient(config.BlobUri, new StorageSharedKeyCredential(config.AccountName, config.AccountKey));
            BlobContainerClient container = service.GetBlobContainerClient(containerName);
            await container.CreateIfNotExistsAsync().ConfigureAwait(false);

            IStorageFacade storage = StorageFacadeFactory.GetStorageFacade(config);
            return new LiveProviderHost(StorageFacadeType.AzureBlobStore, storage, tempRoot: null, pathPrefix: config.Name);
        }

        private static LiveProviderHost StartSmb()
        {
            string host = FirstNonEmpty(
                Environment.GetEnvironmentVariable("SMB__Host"),
                Environment.GetEnvironmentVariable("SMB:Host"),
                "127.0.0.1")!;
            string share = FirstNonEmpty(
                Environment.GetEnvironmentVariable("SMB__Share"),
                Environment.GetEnvironmentVariable("SMB:Share"),
                "share")!;
            string username = FirstNonEmpty(
                Environment.GetEnvironmentVariable("SMB__Username"),
                Environment.GetEnvironmentVariable("SMB:Username"),
                "storage")!;
            string password = FirstNonEmpty(
                Environment.GetEnvironmentVariable("SMB__Password"),
                Environment.GetEnvironmentVariable("SMB:Password"),
                "storage")!;
            string domain = FirstNonEmpty(
                Environment.GetEnvironmentVariable("SMB__Domain"),
                Environment.GetEnvironmentVariable("SMB:Domain"),
                "WORKGROUP")!;
            int port = 445;
            string portText = FirstNonEmpty(
                Environment.GetEnvironmentVariable("SMB__Port"),
                Environment.GetEnvironmentVariable("SMB:Port"));
            if (!string.IsNullOrEmpty(portText) && !int.TryParse(portText, out port))
            {
                throw new InvalidOperationException(
                    $"SMB__Port '{portText}' is not a valid TCP port.");
            }

            EnsureTcpReachable(host, port, "Samba", "make -- test --use-smb-container");

            var config = new SMBNetworkStorageProviderConfig(
                logicalServer: host,
                shareName: share,
                domain: domain,
                username: username,
                password: password,
                physicalServer: host,
                port: port);
            IStorageFacade storage = StorageFacadeFactory.GetStorageFacade(config);
            return new LiveProviderHost(StorageFacadeType.SMBNetworkStore, storage, tempRoot: null, pathPrefix: config.Name);
        }

        private static async Task<LiveProviderHost> StartGcsAsync()
        {
            string bucket = FirstNonEmpty(
                Environment.GetEnvironmentVariable("GCS__BucketName"),
                Environment.GetEnvironmentVariable("GCS:BucketName"),
                "storage-live")!;
            string serviceUri = FirstNonEmpty(
                Environment.GetEnvironmentVariable("GCS__ServiceUri"),
                Environment.GetEnvironmentVariable("GCS:ServiceUri"),
                "http://127.0.0.1:4443/storage/v1/")!;
            string credentialsFile = FirstNonEmpty(
                Environment.GetEnvironmentVariable("GCS__CredentialsFile"),
                Environment.GetEnvironmentVariable("GCS:CredentialsFile"),
                Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS"));

            var authority = new Uri(serviceUri).GetLeftPart(UriPartial.Authority);
            EnsureHttpReachable(authority, "GCS (fake-gcs-server)", "make -- test --use-gcs-container");

            var config = new GoogleCloudStorageProviderConfig(bucket, serviceUri, credentialsFile);
            // Ensure bucket exists on the emulator (or no-op if already present).
            var admin = GoogleCloudStorageProvider.CreateClient(config);
            try
            {
                admin.GetBucket(bucket);
            }
            catch (Exception)
            {
                admin.CreateBucket("storage-facade-live", bucket);
            }

            IStorageFacade storage = StorageFacadeFactory.GetStorageFacade(config);
            return new LiveProviderHost(StorageFacadeType.GoogleCloudStorageStore, storage, tempRoot: null, pathPrefix: config.Name);
        }

        private static LiveProviderHost StartOss()
        {
            string endpoint = FirstNonEmpty(
                Environment.GetEnvironmentVariable("OSS__Endpoint"),
                Environment.GetEnvironmentVariable("OSS:Endpoint"))
                ?? throw new InvalidOperationException(
                    "OSS live tests require OSS__Endpoint (and ALIBABA_CLOUD_ACCESS_KEY_*). "
                    + "There is no local OSS emulator — use `make -- test --use-oss-live` with real/env credentials.");

            string bucket = FirstNonEmpty(
                Environment.GetEnvironmentVariable("OSS__BucketName"),
                Environment.GetEnvironmentVariable("OSS:BucketName"),
                "storage-live")!;

            // Credentials come only from the environment (never hard-coded).
            bool hasEnvCreds =
                !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ALIBABA_CLOUD_ACCESS_KEY_ID"))
                || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OSS_ACCESS_KEY_ID"));
            if (!hasEnvCreds)
            {
                throw new InvalidOperationException(
                    "OSS live tests require ALIBABA_CLOUD_ACCESS_KEY_ID / ALIBABA_CLOUD_ACCESS_KEY_SECRET "
                    + "(or legacy OSS_ACCESS_KEY_ID / OSS_ACCESS_KEY_SECRET) in the environment.");
            }

            var endpointUri = new Uri(
                endpoint.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? endpoint
                    : "https://" + endpoint);
            EnsureTcpReachable(
                endpointUri.Host,
                endpointUri.IsDefaultPort ? (endpointUri.Scheme == "http" ? 80 : 443) : endpointUri.Port,
                "Alibaba OSS",
                "make -- test --use-oss-live");

            var config = new AlibabaOssStorageProviderConfig(endpoint, bucket);
            IStorageFacade storage = StorageFacadeFactory.GetStorageFacade(config);
            return new LiveProviderHost(StorageFacadeType.AlibabaOssStore, storage, tempRoot: null, pathPrefix: config.Name);
        }

        private static void EnsureHttpReachable(string endpoint, string label, string makeHint)
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                using var response = client.GetAsync(endpoint.TrimEnd('/') + "/").GetAwaiter().GetResult();
                _ = response.StatusCode;
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                throw new InvalidOperationException(
                    $"{label} is selected but unreachable at {endpoint}. Start it with `{makeHint}`.",
                    ex);
            }
        }

        private static void EnsureTcpReachable(string host, int port, string label, string makeHint)
        {
            try
            {
                using var tcp = new TcpClient();
                if (!tcp.ConnectAsync(host, port).Wait(TimeSpan.FromSeconds(5)))
                    throw new TimeoutException($"TCP connect to {host}:{port} timed out.");
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                throw new InvalidOperationException(
                    $"{label} is selected but unreachable at {host}:{port}. Start it with `{makeHint}`.",
                    ex);
            }
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
