// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage
{
    using System;
    using System.IO;

    /// <summary>
    /// Thrown when a storage object or path does not exist.
    /// Prefer this over <see cref="FileNotFoundException"/> for <see cref="IStorageFacade"/> callers.
    /// </summary>
    public class StorageNotFoundException : IOException
    {
        /// <summary>Creates an exception for a missing storage path.</summary>
        public StorageNotFoundException(string logicalPath)
            : this(logicalPath, innerException: null)
        {
        }

        /// <summary>Creates an exception for a missing storage path with an inner cause.</summary>
        public StorageNotFoundException(string logicalPath, Exception innerException)
            : base($"Unable to find {logicalPath}", innerException)
        {
            LogicalPath = logicalPath;
        }

        /// <summary>Logical path that was not found.</summary>
        public string LogicalPath { get; }
    }
}
