// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage.Tests
{
    using Amazon.S3;
    using NUnit.Framework;
    using Storage.Providers;

    [TestFixture]
    public class S3ClientCreatorTests
    {
        [Test]
        public void CreateClient_WithRegionOnly_UsesDefaultCredentialChain()
        {
            var creator = new S3ClientCreator();
            var config = new AwsS3StorageProviderConfig("us-east-1", "my-bucket");

            using IAmazonS3 client = creator.CreateClient(config);

            Assert.That(client, Is.Not.Null);
            Assert.That(client.Config.RegionEndpoint.SystemName, Is.EqualTo("us-east-1"));
            Assert.That(client.Config.ServiceURL, Is.Null.Or.Empty);
        }

        [Test]
        public void CreateClient_WithServiceUrlAndRegion_ConfiguresCustomEndpoint()
        {
            var creator = new S3ClientCreator();
            var config = new AwsS3StorageProviderConfig(
                "us-east-1",
                "my-bucket",
                serviceUrl: "http://localhost:9090");

            using IAmazonS3 client = creator.CreateClient(config);

            Assert.That(client, Is.Not.Null);
            Assert.That(client.Config.ServiceURL, Does.Contain("localhost:9090"));
            Assert.That(((AmazonS3Config)client.Config).ForcePathStyle, Is.True);
            Assert.That(client.Config.AuthenticationRegion, Is.EqualTo("us-east-1"));
        }

        [Test]
        public void CreateClient_WithExplicitCredentials_UsesBasicCredentials()
        {
            var creator = new S3ClientCreator();
            var config = new AwsS3StorageProviderConfig(
                "test-key",
                "test-secret",
                "us-west-2",
                "my-bucket");

            using IAmazonS3 client = creator.CreateClient(config);

            Assert.That(client, Is.Not.Null);
            Assert.That(client.Config.RegionEndpoint.SystemName, Is.EqualTo("us-west-2"));
        }

        [Test]
        public void CreateClient_WithSessionToken_CreatesClient()
        {
            var creator = new S3ClientCreator();
            var config = new AwsS3StorageProviderConfig(
                "test-key",
                "test-secret",
                "us-east-1",
                "my-bucket",
                sessionToken: "test-session-token");

            using IAmazonS3 client = creator.CreateClient(config);

            Assert.That(client, Is.Not.Null);
            Assert.That(client.Config.RegionEndpoint.SystemName, Is.EqualTo("us-east-1"));
        }

        [Test]
        public void CreateClient_WithKeysAndServiceUrl_UsesCustomEndpoint()
        {
            var creator = new S3ClientCreator();
            var config = new AwsS3StorageProviderConfig(
                "test-key",
                "test-secret",
                "us-east-1",
                "my-bucket",
                sessionToken: null,
                serviceUrl: "http://127.0.0.1:9000");

            using IAmazonS3 client = creator.CreateClient(config);

            Assert.That(client, Is.Not.Null);
            Assert.That(client.Config.ServiceURL, Does.Contain("127.0.0.1:9000"));
            Assert.That(((AmazonS3Config)client.Config).ForcePathStyle, Is.True);
        }
    }
}
