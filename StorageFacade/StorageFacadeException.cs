// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage
{
    using System;
    using System.IO;

    /// <summary>
    /// Thrown when a storage provider operation fails (I/O, protocol, or backend error).
    /// Missing objects use <see cref="StorageNotFoundException"/> instead.
    /// </summary>
    public class StorageFacadeException : IOException
    {
        /// <summary>Creates an exception with the given message.</summary>
        public StorageFacadeException(string message)
            : base(message)
        {
        }

        /// <summary>Creates an exception with the given message and inner cause.</summary>
        public StorageFacadeException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
