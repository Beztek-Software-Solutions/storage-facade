// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage.Tests
{
    using System;
    using NUnit.Framework;

    [TestFixture]
    public class ProviderConfigurationTests
    {
        [Test]
        public void FileStorageProviderConfig_HasExpectedDefaults()
        {
            var config = new FileStorageProviderConfig();
            Assert.That(config.Name, Is.EqualTo("Local Store"));
            Assert.That(config.StorageFacadeType, Is.EqualTo(StorageFacadeType.LocalFileStore));
        }

        [Test]
        public void AwsS3StorageProviderConfig_NormalizesName()
        {
            var config = new AwsS3StorageProviderConfig("key", "secret", "us-west-2", "My-Bucket");
            Assert.That(config.Name, Is.EqualTo("s3://my-bucket"));
            Assert.That(config.StorageFacadeType, Is.EqualTo(StorageFacadeType.AmazonS3Store));
        }

        [Test]
        public void AwsS3StorageProviderConfig_DefaultChainConstructor_NormalizesName()
        {
            var config = new AwsS3StorageProviderConfig("us-east-1", "My-Bucket");
            Assert.That(config.Name, Is.EqualTo("s3://my-bucket"));
            Assert.That(config.StorageFacadeType, Is.EqualTo(StorageFacadeType.AmazonS3Store));
            Assert.That(config.AccessKeyId, Is.Null);
            Assert.That(config.SecretAccessKey, Is.Null);
            Assert.That(config.SessionToken, Is.Null);
        }

        [Test]
        public void AwsS3StorageProviderConfig_SessionTokenConstructor_StoresToken()
        {
            var config = new AwsS3StorageProviderConfig(
                "key",
                "secret",
                "us-east-1",
                "bucket",
                sessionToken: "tok",
                serviceUrl: "http://localhost:9090");

            Assert.That(config.Name, Is.EqualTo("s3://bucket"));
            Assert.That(config.SessionToken, Is.EqualTo("tok"));
            Assert.That(config.ServiceUrl, Is.EqualTo("http://localhost:9090"));
        }

        [Test]
        public void AzureBlobStorageProviderConfig_ServiceUriConstructor_UsesCustomEndpoint()
        {
            var serviceUri = new Uri("http://127.0.0.1:10000/devstoreaccount1");
            var config = new AzureBlobStorageProviderConfig(
                serviceUri,
                accountKey: "key",
                containerName: "data");

            Assert.That(config.Name, Is.EqualTo("http://127.0.0.1:10000/devstoreaccount1/data"));
            Assert.That(config.StorageFacadeType, Is.EqualTo(StorageFacadeType.AzureBlobStore));
            Assert.That(config.AccountName, Is.EqualTo("devstoreaccount1"));
            Assert.That(config.ContainerName, Is.EqualTo("data"));
            Assert.That(config.BlobUri, Is.EqualTo(serviceUri));
            Assert.That(config.ConnectionString, Is.Null);
        }

        [Test]
        public void AzureBlobStorageProviderConfig_ConnectionStringConstructor_ParsesAzuriteEndpoint()
        {
            const string connectionString =
                "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;"
                + "AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;"
                + "BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;";

            var config = new AzureBlobStorageProviderConfig(connectionString, containerName: "storage-live");

            Assert.That(config.StorageFacadeType, Is.EqualTo(StorageFacadeType.AzureBlobStore));
            Assert.That(config.AccountName, Is.EqualTo("devstoreaccount1"));
            Assert.That(config.ContainerName, Is.EqualTo("storage-live"));
            Assert.That(config.BlobUri, Is.EqualTo(new Uri("http://127.0.0.1:10000/devstoreaccount1")));
            Assert.That(config.Name, Is.EqualTo("http://127.0.0.1:10000/devstoreaccount1/storage-live"));
            Assert.That(config.ConnectionString, Is.EqualTo(connectionString));
        }

        [Test]
        public void AzureBlobStorageProviderConfig_AccountKeyConstructor_ParsesAccountName()
        {
            var config = new AzureBlobStorageProviderConfig(
                "MyAccount.blob.core.windows.net",
                "key",
                "uploads",
                isHierarchicalNamespace: true);

            Assert.That(config.Name, Is.EqualTo("https://myaccount.blob.core.windows.net/uploads"));
            Assert.That(config.StorageFacadeType, Is.EqualTo(StorageFacadeType.AzureBlobStore));
        }

        [Test]
        public void AzureBlobStorageProviderConfig_SasUriConstructor_ParsesContainer()
        {
            var uri = new Uri("https://acct.blob.core.windows.net/data?sp=r&sig=x");
            var config = new AzureBlobStorageProviderConfig(uri, isHierarchicalNamespace: false);

            Assert.That(config.Name, Is.EqualTo("https://acct.blob.core.windows.net/data"));
            Assert.That(config.StorageFacadeType, Is.EqualTo(StorageFacadeType.AzureBlobStore));
        }

        [Test]
        public void SmbNetworkStorageProviderConfig_MapsDfsPhysicalServer()
        {
            var config = new SMBNetworkStorageProviderConfig(
                logicalServer: "DFS-HOST",
                shareName: "Docs",
                domain: "CORP",
                username: "svc",
                password: "secret",
                physicalServer: "PHYS-HOST",
                smbIdleTimeoutSeconds: 600);

            Assert.That(config.Name, Is.EqualTo(@"\\dfs-host\docs"));
            Assert.That(config.StorageFacadeType, Is.EqualTo(StorageFacadeType.SMBNetworkStore));
            Assert.That(config.Port, Is.EqualTo(445));
        }

        [Test]
        public void SmbNetworkStorageProviderConfig_CustomPort_IsRetained()
        {
            var config = new SMBNetworkStorageProviderConfig(
                logicalServer: "127.0.0.1",
                shareName: "share",
                domain: "WORKGROUP",
                username: "u",
                password: "p",
                port: 1445);

            Assert.That(config.Port, Is.EqualTo(1445));
        }

        [Test]
        public void SmbNetworkStorageProviderConfig_InvalidPort_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new SMBNetworkStorageProviderConfig(
                    logicalServer: "h",
                    shareName: "s",
                    domain: "d",
                    username: "u",
                    password: "p",
                    port: 0));
        }

        [Test]
        public void GoogleCloudStorageProviderConfig_BucketOnly_UsesAdcDefaults()
        {
            var config = new GoogleCloudStorageProviderConfig("My-Bucket");

            Assert.That(config.Name, Is.EqualTo("gs://my-bucket"));
            Assert.That(config.StorageFacadeType, Is.EqualTo(StorageFacadeType.GoogleCloudStorageStore));
            Assert.That(config.CredentialsFilePath, Is.Null);
            Assert.That(config.ServiceUri, Is.Null);
        }

        [Test]
        public void AlibabaOssStorageProviderConfig_EndpointBucket_LeavesKeysUnsetForEnv()
        {
            var config = new AlibabaOssStorageProviderConfig("oss-us-west-1.aliyuncs.com", "My-Bucket");

            Assert.That(config.Name, Is.EqualTo("oss://my-bucket"));
            Assert.That(config.StorageFacadeType, Is.EqualTo(StorageFacadeType.AlibabaOssStore));
            Assert.That(config.AccessKeyId, Is.Null);
            Assert.That(config.AccessKeySecret, Is.Null);
            Assert.That(config.Endpoint, Is.EqualTo("https://oss-us-west-1.aliyuncs.com"));
        }

        [Test]
        public void AlibabaEnvironmentCredentialsProvider_Throws_WhenEnvMissing()
        {
            string id = Environment.GetEnvironmentVariable("ALIBABA_CLOUD_ACCESS_KEY_ID");
            string secret = Environment.GetEnvironmentVariable("ALIBABA_CLOUD_ACCESS_KEY_SECRET");
            string legacyId = Environment.GetEnvironmentVariable("OSS_ACCESS_KEY_ID");
            string legacySecret = Environment.GetEnvironmentVariable("OSS_ACCESS_KEY_SECRET");
            try
            {
                Environment.SetEnvironmentVariable("ALIBABA_CLOUD_ACCESS_KEY_ID", null);
                Environment.SetEnvironmentVariable("ALIBABA_CLOUD_ACCESS_KEY_SECRET", null);
                Environment.SetEnvironmentVariable("OSS_ACCESS_KEY_ID", null);
                Environment.SetEnvironmentVariable("OSS_ACCESS_KEY_SECRET", null);

                var provider = new Beztek.Facade.Storage.Providers.AlibabaEnvironmentCredentialsProvider();
                Assert.Throws<InvalidOperationException>(() => provider.GetCredentials());
            }
            finally
            {
                Environment.SetEnvironmentVariable("ALIBABA_CLOUD_ACCESS_KEY_ID", id);
                Environment.SetEnvironmentVariable("ALIBABA_CLOUD_ACCESS_KEY_SECRET", secret);
                Environment.SetEnvironmentVariable("OSS_ACCESS_KEY_ID", legacyId);
                Environment.SetEnvironmentVariable("OSS_ACCESS_KEY_SECRET", legacySecret);
            }
        }

        [Test]
        public void AlibabaEnvironmentCredentialsProvider_ReadsLegacyEnvAndToken()
        {
            string id = Environment.GetEnvironmentVariable("ALIBABA_CLOUD_ACCESS_KEY_ID");
            string secret = Environment.GetEnvironmentVariable("ALIBABA_CLOUD_ACCESS_KEY_SECRET");
            string token = Environment.GetEnvironmentVariable("ALIBABA_CLOUD_SECURITY_TOKEN");
            string legacyId = Environment.GetEnvironmentVariable("OSS_ACCESS_KEY_ID");
            string legacySecret = Environment.GetEnvironmentVariable("OSS_ACCESS_KEY_SECRET");
            string legacyToken = Environment.GetEnvironmentVariable("OSS_SECURITY_TOKEN");
            try
            {
                Environment.SetEnvironmentVariable("ALIBABA_CLOUD_ACCESS_KEY_ID", null);
                Environment.SetEnvironmentVariable("ALIBABA_CLOUD_ACCESS_KEY_SECRET", null);
                Environment.SetEnvironmentVariable("ALIBABA_CLOUD_SECURITY_TOKEN", null);
                Environment.SetEnvironmentVariable("OSS_ACCESS_KEY_ID", "legacy-id");
                Environment.SetEnvironmentVariable("OSS_ACCESS_KEY_SECRET", "legacy-secret");
                Environment.SetEnvironmentVariable("OSS_SECURITY_TOKEN", "legacy-token");

                var provider = new Beztek.Facade.Storage.Providers.AlibabaEnvironmentCredentialsProvider();
                Assert.Throws<NotSupportedException>(() => provider.SetCredentials(null));

                Aliyun.OSS.Common.Authentication.ICredentials creds = provider.GetCredentials();
                Assert.That(creds.AccessKeyId, Is.EqualTo("legacy-id"));
                Assert.That(creds.AccessKeySecret, Is.EqualTo("legacy-secret"));
                Assert.That(creds.SecurityToken, Is.EqualTo("legacy-token"));
            }
            finally
            {
                Environment.SetEnvironmentVariable("ALIBABA_CLOUD_ACCESS_KEY_ID", id);
                Environment.SetEnvironmentVariable("ALIBABA_CLOUD_ACCESS_KEY_SECRET", secret);
                Environment.SetEnvironmentVariable("ALIBABA_CLOUD_SECURITY_TOKEN", token);
                Environment.SetEnvironmentVariable("OSS_ACCESS_KEY_ID", legacyId);
                Environment.SetEnvironmentVariable("OSS_ACCESS_KEY_SECRET", legacySecret);
                Environment.SetEnvironmentVariable("OSS_SECURITY_TOKEN", legacyToken);
            }
        }

        [Test]
        public void AzureBlobStorageProviderConfig_RejectsMissingAccountKey()
        {
            Assert.Throws<ArgumentException>(() =>
                new AzureBlobStorageProviderConfig(
                    new Uri("http://127.0.0.1:10000/devstoreaccount1"),
                    accountKey: " ",
                    containerName: "c"));
        }

        [Test]
        public void AzureBlobStorageProviderConfig_RejectsMissingContainer()
        {
            Assert.Throws<ArgumentException>(() =>
                new AzureBlobStorageProviderConfig(
                    new Uri("http://127.0.0.1:10000/devstoreaccount1"),
                    accountKey: "key",
                    containerName: " "));
        }

        [Test]
        public void AzureBlobStorageProviderConfig_ConnectionString_RequiresAccountFields()
        {
            Assert.Throws<ArgumentException>(() =>
                new AzureBlobStorageProviderConfig("DefaultEndpointsProtocol=https;", "c"));
            Assert.Throws<ArgumentException>(() =>
                new AzureBlobStorageProviderConfig(
                    "DefaultEndpointsProtocol=https;AccountName=a;", "c"));
            Assert.Throws<ArgumentException>(() =>
                new AzureBlobStorageProviderConfig(" ", "c"));
            Assert.Throws<ArgumentException>(() =>
                new AzureBlobStorageProviderConfig(
                    "DefaultEndpointsProtocol=https;AccountName=a;AccountKey=k;", " "));
        }

        [Test]
        public void AzureBlobStorageProviderConfig_ConnectionString_WithoutBlobEndpoint_UsesPublicHost()
        {
            var config = new AzureBlobStorageProviderConfig(
                "DefaultEndpointsProtocol=https;AccountName=MyAcct;AccountKey=abc123;",
                "uploads");

            Assert.That(config.AccountName, Is.EqualTo("MyAcct"));
            Assert.That(config.Name, Is.EqualTo("https://myacct.blob.core.windows.net/uploads"));
            Assert.That(config.BlobUri.Host, Is.EqualTo("myacct.blob.core.windows.net"));
        }

        [Test]
        public void GoogleCloudStorageProviderConfig_RejectsEmptyBucket()
        {
            Assert.Throws<ArgumentException>(() => new GoogleCloudStorageProviderConfig(" "));
        }

        [Test]
        public void AlibabaOssStorageProviderConfig_RejectsEmptyKeysOrEndpoint()
        {
            Assert.Throws<ArgumentException>(() =>
                new AlibabaOssStorageProviderConfig("oss.example.com", " ", "secret", "b"));
            Assert.Throws<ArgumentException>(() =>
                new AlibabaOssStorageProviderConfig("oss.example.com", "id", " ", "b"));
            Assert.Throws<ArgumentException>(() =>
                new AlibabaOssStorageProviderConfig(" ", "b"));
            Assert.Throws<ArgumentException>(() =>
                new AlibabaOssStorageProviderConfig("oss.example.com", " "));
        }

        [Test]
        public void PortAwareSmb2Client_RejectsInvalidPort()
        {
            var client = new Beztek.Facade.Storage.Providers.PortAwareSmb2Client(
                _ => Array.Empty<System.Net.IPAddress>(),
                (_, _) => true);
            Assert.Throws<ArgumentOutOfRangeException>(() => client.Connect("localhost", 0));
            Assert.Throws<ArgumentException>(() => client.Connect(" ", 445));
        }

        [Test]
        public void CloudLogicalPath_StripsLeadingSlashAfterPrefix()
        {
            // Double-slash after store name is normalized.
            Assert.That(
                Beztek.Facade.Storage.Providers.CloudLogicalPath.GetRelativePath("s3://b", "s3://b//a/b.txt"),
                Is.EqualTo("a/b.txt"));
        }
    }
}
