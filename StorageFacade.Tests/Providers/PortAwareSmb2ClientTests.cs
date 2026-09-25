// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Storage.Tests
{
    using System;
    using System.Net;
    using System.Net.Sockets;
    using Beztek.Facade.Storage.Providers;
    using NUnit.Framework;

    [TestFixture]
    public class PortAwareSmb2ClientTests
    {
        [Test]
        public void Connect_RejectsBlankServerName()
        {
            var client = new PortAwareSmb2Client(_ => Array.Empty<IPAddress>(), (_, _) => true);
            Assert.Throws<ArgumentException>(() => client.Connect(" ", 445));
            Assert.Throws<ArgumentNullException>(() => client.Connect(null, 445));
        }

        [Test]
        public void Connect_RejectsOutOfRangePort()
        {
            var client = new PortAwareSmb2Client(_ => new[] { IPAddress.Loopback }, (_, _) => true);
            Assert.Throws<ArgumentOutOfRangeException>(() => client.Connect("localhost", 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => client.Connect("localhost", 65536));
        }

        [Test]
        public void Connect_WhenDnsReturnsNoAddresses_ThrowsStorageFacadeException()
        {
            var client = new PortAwareSmb2Client(_ => Array.Empty<IPAddress>(), (_, _) => true);
            var ex = Assert.Throws<StorageFacadeException>(() => client.Connect("missing.example", 1445));
            Assert.That(ex!.Message, Does.Contain("Cannot resolve"));
        }

        [Test]
        public void SelectServerAddress_PrefersIpv4()
        {
            var ipv6 = IPAddress.Parse("2001:db8::1");
            var ipv4 = IPAddress.Parse("10.0.0.5");
            IPAddress selected = PortAwareSmb2Client.SelectServerAddress("host", new[] { ipv6, ipv4 });
            Assert.That(selected, Is.EqualTo(ipv4));
        }

        [Test]
        public void SelectServerAddress_FallsBackToFirstWhenNoIpv4()
        {
            var ipv6 = IPAddress.Parse("2001:db8::1");
            IPAddress selected = PortAwareSmb2Client.SelectServerAddress("host", new[] { ipv6 });
            Assert.That(selected, Is.EqualTo(ipv6));
            Assert.That(selected.AddressFamily, Is.EqualTo(AddressFamily.InterNetworkV6));
        }

        [Test]
        public void SelectServerAddress_NullOrEmpty_Throws()
        {
            Assert.Throws<StorageFacadeException>(() =>
                PortAwareSmb2Client.SelectServerAddress("host", null));
            Assert.Throws<StorageFacadeException>(() =>
                PortAwareSmb2Client.SelectServerAddress("host", Array.Empty<IPAddress>()));
        }

        [Test]
        public void Connect_UsesResolvedAddressAndPort()
        {
            IPAddress seenAddress = null;
            int seenPort = 0;
            var client = new PortAwareSmb2Client(
                _ => new[] { IPAddress.Parse("192.0.2.10") },
                (addr, port) =>
                {
                    seenAddress = addr;
                    seenPort = port;
                    return true;
                });

            Assert.That(client.Connect("example.test", 1445), Is.True);
            Assert.That(seenAddress, Is.EqualTo(IPAddress.Parse("192.0.2.10")));
            Assert.That(seenPort, Is.EqualTo(1445));
        }
    }
}
