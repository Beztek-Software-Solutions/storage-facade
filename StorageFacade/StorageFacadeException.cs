// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage
{
    using System.IO;

    /// <summary>
    /// Thrown when a storage provider operation fails (I/O, protocol, or backend error).
    /// Missing objects should use <see cref="FileNotFoundException"/> instead.
    /// </summary>
    public class StorageFacadeException : IOException
    {
        /// <summary>Creates an exception with the given message.</summary>
        public StorageFacadeException(string message)
            : base(message)
        {
        }
    }
}
