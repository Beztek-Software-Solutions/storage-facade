// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage.Tests.Live
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using NUnit.Framework;

    /// <summary>Shared helpers for live fixture startup and failure assertions.</summary>
    internal static class LiveTestHelpers
    {
        internal static bool IsStartupFailure(Exception ex)
        {
            for (Exception cur = ex; cur != null; cur = cur.InnerException)
            {
                string msg = cur.Message ?? "";
                if (msg.Contains("Unable to connect", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("Connection refused", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("No connection could be made", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("Name or service not known", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Providers surface missing-object failures as SDK exceptions, <see cref="System.IO.IOException"/>,
        /// or <see cref="FileNotFoundException"/>. Any thrown exception is the expected live contract.
        /// </summary>
        internal static void AssertThrowsOnMissing(Action action, string because)
        {
            Exception caught = null;
            try
            {
                action();
            }
            catch (Exception ex)
            {
                caught = ex;
            }

            if (caught == null)
                throw new AssertionException($"Expected an exception ({because}), but none was thrown.");
        }

        internal static async Task AssertThrowsOnMissingAsync(Func<Task> action, string because)
        {
            Exception caught = null;
            try
            {
                await action().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                caught = ex;
            }

            if (caught == null)
                throw new AssertionException($"Expected an exception ({because}), but none was thrown.");
        }

        /// <summary>Forward-only stream with no Length/Position (unknown-length upload path).</summary>
        internal sealed class NonSeekableStream : Stream
        {
            private readonly Stream _inner;

            public NonSeekableStream(Stream inner) => _inner = inner;

            public override bool CanRead => _inner.CanRead;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override void Flush() => _inner.Flush();
            public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }

        /// <summary>Non-seekable stream that yields <paramref name="failAfter"/> bytes then throws.</summary>
        internal sealed class FailAfterBytesStream : Stream
        {
            private readonly int _failAfter;
            private int _read;

            public FailAfterBytesStream(int failAfter) => _failAfter = failAfter;

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override void Flush() { }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (_read >= _failAfter)
                    throw new IOException("simulated client abort mid-upload");

                int n = Math.Min(count, _failAfter - _read);
                Array.Fill(buffer, (byte)'a', offset, n);
                _read += n;
                return n;
            }

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}
