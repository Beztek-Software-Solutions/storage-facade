// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Runtime.ExceptionServices;
    using Aliyun.OSS.Common;
    using Amazon.S3;
    using Azure;
    using Google;

    /// <summary>
    /// Normalizes provider/SDK failures onto the public <see cref="IStorageFacade"/>
    /// exception contract so File, SMB, Azure, S3, GCS, and OSS look the same to callers.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description>Missing object/path → <see cref="StorageNotFoundException"/></description></item>
    /// <item><description>Caller/argument mistakes → <see cref="ArgumentException"/> family (unchanged)</description></item>
    /// <item><description>Cancel / dispose / not-supported → unchanged</description></item>
    /// <item><description>All other backend/I/O/protocol failures → <see cref="StorageFacadeException"/></description></item>
    /// </list>
    /// </remarks>
    internal static class StorageExceptionPolicy
    {
        /// <summary>
        /// True when <paramref name="exception"/> (or a nested cause) indicates the
        /// object or path does not exist.
        /// </summary>
        public static bool IsMissing(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);
            foreach (Exception current in Flatten(exception))
            {
                if (IsMissingCore(current))
                    return true;
            }

            return false;
        }

        /// <summary>Rethrows a normalized failure for enumerate / get / read / checksum.</summary>
        public static void RethrowForRead(string logicalPath, Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);
            if (TryPassThrough(exception))
                return;

            if (IsMissing(exception))
                throw AsStorageNotFound(logicalPath, exception);

            throw AsStorageFailure($"Storage operation failed for '{logicalPath}'", exception);
        }

        /// <summary>Rethrows a normalized failure for write.</summary>
        public static void RethrowForWrite(string logicalPath, Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);
            if (TryPassThrough(exception))
                return;

            // Already-normalized facade errors (e.g. checksum mismatch) keep their message.
            if (PreferRoot(exception) is StorageFacadeException facade)
                ExceptionDispatchInfo.Capture(facade).Throw();

            throw AsStorageFailure($"Unable to write '{logicalPath}'", exception);
        }

        /// <summary>Rethrows a normalized failure for delete (caller already handled misses).</summary>
        public static void RethrowForDelete(string logicalPath, Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);
            if (TryPassThrough(exception))
                return;

            throw AsStorageFailure($"Unable to delete '{logicalPath}'", exception);
        }

        /// <summary>Rethrows a normalized failure for enumerate.</summary>
        public static void RethrowForEnumerate(string logicalPath, Exception exception)
            => RethrowForRead(logicalPath, exception);

        private static bool TryPassThrough(Exception exception)
        {
            Exception root = PreferRoot(exception);
            if (root is ArgumentException
                or ObjectDisposedException
                or OperationCanceledException
                or NotSupportedException)
            {
                ExceptionDispatchInfo.Capture(root).Throw();
                return true;
            }

            return false;
        }

        private static StorageNotFoundException AsStorageNotFound(string logicalPath, Exception exception)
        {
            Exception root = PreferMissingCause(exception) ?? PreferRoot(exception);
            if (root is StorageNotFoundException existing)
                return existing;

            return new StorageNotFoundException(logicalPath, root);
        }

        private static StorageFacadeException AsStorageFailure(string message, Exception exception)
        {
            Exception root = PreferRoot(exception);
            if (root is StorageFacadeException existing)
                return existing;

            return new StorageFacadeException(message, root);
        }

        private static bool IsMissingCore(Exception current) =>
            current switch
            {
                StorageNotFoundException or FileNotFoundException or DirectoryNotFoundException => true,
                AmazonS3Exception s3 => IsS3Miss(s3),
                RequestFailedException azure => azure.Status == 404,
                GoogleApiException google => google.HttpStatusCode == HttpStatusCode.NotFound,
                OssException oss => IsOssMiss(oss),
                _ => false,
            };

        private static bool IsS3Miss(AmazonS3Exception s3) =>
            s3.StatusCode == HttpStatusCode.NotFound || IsS3MissErrorCode(s3.ErrorCode);

        private static bool IsS3MissErrorCode(string code) =>
            code?.ToUpperInvariant() is "NOTFOUND" or "NOSUCHKEY" or "NOSUCHBUCKET";

        private static bool IsOssMiss(OssException oss)
        {
            string code = oss.ErrorCode?.ToUpperInvariant();
            return code is "NOSUCHKEY" or "NOSUCHBUCKET" or "NOTFOUND";
        }

        private static Exception PreferMissingCause(Exception exception)
        {
            foreach (Exception current in Flatten(exception))
            {
                if (IsMissingCore(current))
                    return current;
            }

            return null;
        }

        private static Exception PreferRoot(Exception exception)
        {
            if (exception is AggregateException aggregate)
            {
                AggregateException flat = aggregate.Flatten();
                if (flat.InnerExceptions.Count == 1)
                    return PreferRoot(flat.InnerExceptions[0]);
            }

            return exception;
        }

        private static IEnumerable<Exception> Flatten(Exception exception)
        {
            if (exception is AggregateException aggregate)
            {
                foreach (Exception inner in aggregate.Flatten().InnerExceptions)
                {
                    foreach (Exception nested in Flatten(inner))
                        yield return nested;
                }

                yield break;
            }

            yield return exception;
            if (exception.InnerException is { } child)
            {
                foreach (Exception nested in Flatten(child))
                    yield return nested;
            }
        }
    }
}
