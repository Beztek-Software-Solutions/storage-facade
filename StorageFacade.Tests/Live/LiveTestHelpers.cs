// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage.Tests.Live
{
    using System;
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
    }
}
