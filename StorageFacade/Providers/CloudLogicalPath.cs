// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage.Providers
{
    using System;

    /// <summary>
    /// Shared helpers for cloud object stores whose logical paths are
    /// <c>{scheme}://{bucket|container}/{object-key}</c> (S3, Azure, GCS, OSS).
    /// </summary>
    internal static class CloudLogicalPath
    {
        /// <summary>
        /// Strips the store name prefix from <paramref name="logicalPath"/>, returning the
        /// object key (no leading or trailing slash).
        /// </summary>
        /// <param name="storeName">
        /// Value of <c>GetName()</c> / config <c>Name</c> (e.g. <c>s3://orders</c>).
        /// </param>
        /// <param name="logicalPath">Full logical path including the store prefix.</param>
        internal static string GetRelativePath(string storeName, string logicalPath)
        {
            ArgumentException.ThrowIfNullOrEmpty(storeName);
            ArgumentException.ThrowIfNullOrEmpty(logicalPath);

            if (!logicalPath.EndsWith('/'))
                logicalPath = $"{logicalPath}/";

            int uriLength = storeName.Length;
            if (logicalPath.Length <= uriLength + 1)
                return string.Empty;

            string currPath = logicalPath.Substring(uriLength + 1, logicalPath.Length - uriLength - 1);
            if (currPath.StartsWith('/'))
                currPath = currPath[1..];
            if (currPath.EndsWith('/'))
                currPath = currPath[..^1];
            return currPath;
        }

        /// <summary>Returns the final path segment of a key or logical path.</summary>
        internal static string GetLeafName(string path)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            int index = path.LastIndexOf('/') + 1;
            return path[index..];
        }

        /// <summary>
        /// Whether <paramref name="objectKey"/> should appear when listing under <paramref name="prefix"/>.
        /// Skips directory placeholders; when not recursive, only immediate children.
        /// </summary>
        internal static bool IsListedObjectVisible(string prefix, string objectKey, bool isRecursive)
        {
            if (string.IsNullOrEmpty(objectKey) || objectKey.EndsWith("/", StringComparison.Ordinal))
                return false;

            if (isRecursive)
                return true;

            string relative = string.IsNullOrEmpty(prefix)
                ? objectKey
                : objectKey.StartsWith(prefix + "/", StringComparison.Ordinal)
                    ? objectKey[(prefix.Length + 1)..]
                    : objectKey == prefix ? "" : null;

            return relative is not null && !relative.Contains('/');
        }
    }
}
