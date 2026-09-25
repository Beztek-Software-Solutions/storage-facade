// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage
{
    using System;

    /// <summary>
    /// Configuration for the Google Cloud Storage provider
    /// (<c>Google.Cloud.Storage.V1</c>). Prefer Application Default Credentials;
    /// do not embed service-account keys in source.
    /// </summary>
    public class GoogleCloudStorageProviderConfig : IStorageProviderConfig
    {
        /// <summary>
        /// Creates a GCS configuration that uses Application Default Credentials
        /// (environment, <c>GOOGLE_APPLICATION_CREDENTIALS</c>, GCE/GKE metadata, etc.).
        /// For emulators (e.g. fake-gcs-server), pass <paramref name="serviceUri"/> only.
        /// </summary>
        /// <param name="bucketName">GCS bucket name.</param>
        /// <param name="serviceUri">
        /// Optional API base (e.g. <c>http://127.0.0.1:4443/storage/v1/</c> for fake-gcs-server).
        /// When set without a credentials file, the client uses unauthenticated emulator access.
        /// </param>
        public GoogleCloudStorageProviderConfig(string bucketName, string serviceUri = null)
            : this(bucketName, serviceUri, credentialsFilePath: null)
        {
        }

        /// <summary>
        /// Creates a GCS configuration that loads a service-account JSON <em>file path</em>
        /// (same shape as <c>GOOGLE_APPLICATION_CREDENTIALS</c>). Prefer ADC when possible.
        /// </summary>
        /// <param name="bucketName">GCS bucket name.</param>
        /// <param name="serviceUri">Optional API base / emulator URI.</param>
        /// <param name="credentialsFilePath">
        /// Path to a service-account JSON file. Null = ADC (or unauthenticated emulator when
        /// <paramref name="serviceUri"/> is set).
        /// </param>
        public GoogleCloudStorageProviderConfig(
            string bucketName,
            string serviceUri,
            string credentialsFilePath)
        {
            if (string.IsNullOrWhiteSpace(bucketName))
                throw new ArgumentException("Bucket name is required.", nameof(bucketName));

            BucketName = bucketName.Trim();
            ServiceUri = string.IsNullOrWhiteSpace(serviceUri) ? null : serviceUri.Trim();
            CredentialsFilePath = string.IsNullOrWhiteSpace(credentialsFilePath)
                ? null
                : credentialsFilePath.Trim();
            StorageFacadeType = StorageFacadeType.GoogleCloudStorageStore;
            Name = $"gs://{BucketName}".ToLowerInvariant();
        }

        /// <inheritdoc/>
        public string Name { get; }

        /// <inheritdoc/>
        public StorageFacadeType StorageFacadeType { get; }

        internal string BucketName { get; }

        internal string ServiceUri { get; }

        /// <summary>Optional path to service-account JSON; null means ADC / emulator.</summary>
        internal string CredentialsFilePath { get; }
    }
}
