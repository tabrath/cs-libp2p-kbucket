using System;
using System.Linq;
using System.Threading.Tasks;
using LibP2P.Peer;
using LibP2P.Peer.Store;
using Xunit;

namespace LibP2P.KBucket.Tests
{
    public class TableTests
    {
        [Fact]
        public void TestBucket()
        {
            var b = new Bucket();

            var peers = new PeerId[100];
            for (var i = 0; i < 100; i++)
            {
                peers[i] = new PeerId($"Random PeerId {i}");
                b.PushFront(peers[i]);
            }

            var local = new PeerId($"Local PeerId");
            var localId = DhtId.ConvertPeerId(local);
            var x = new Random(Environment.TickCount).Next(peers.Length);
            Assert.True(b.Has(peers[x]));

            var spl = b.Split(0, DhtId.ConvertPeerId(local));
            var llist = b.Peers;
            foreach (var e in llist)
            {
                var p = DhtId.ConvertPeerId(e);
                var cpl = DhtId.CommonPrefixLength(p, localId);
                Assert.False(cpl > 0);
            }
            var rlist = spl.Peers;
            foreach (var e in rlist)
            {
                var p = DhtId.ConvertPeerId(e);
                var cpl = DhtId.CommonPrefixLength(p, localId);
                Assert.NotEqual(0, cpl);
            }
        }

        [Fact]
        public void TestTableUpdate()
        {
            var local = new PeerId("Random Local Peer");
            var m = new Metrics();
            var rt = new RoutingTable(10, DhtId.ConvertPeerId(local), TimeSpan.FromHours(1), m);

            var peers = Enumerable.Range(0, 100)
                .Select(i => new PeerId($"Random Peer {i}"))
                .ToArray();

            var rand = new Random(Environment.TickCount);
            for (var i = 0; i < 10000; i++)
            {
                rt.Update(peers[rand.Next(peers.Length)]);
            }

            for (var i = 0; i < 100; i++)
            {
                var id = DhtId.ConvertPeerId(new PeerId($"Random Peer {rand.Next(1024)}"));
                var ret = rt.NearestPeers(id, 5);

                Assert.True(ret.Length > 0);
            }
        }

        [Fact]
        public void TestTableFind()
        {
            var local = new PeerId("Random Local Peer");
            var m = new Metrics();
            var rt = new RoutingTable(10, DhtId.ConvertPeerId(local), TimeSpan.FromHours(1), m);

            var peers = new PeerId[100];
            for (var i = 0; i < 5; i++)
            {
                peers[i] = new PeerId($"Random Peer {i}");
                rt.Update(peers[i]);
            }

            var found = rt.NearestPeer(DhtId.ConvertPeerId(peers[2]));
            Assert.Equal(peers[2], found);
        }

        [Fact]
        public void TestTableFindMultiple()
        {
            var local = new PeerId("Random Local Peer");
            var m = new Metrics();
            var rt = new RoutingTable(20, DhtId.ConvertPeerId(local), TimeSpan.FromHours(1), m);

            var peers = new PeerId[100];
            for (var i = 0; i < 18; i++)
            {
                peers[i] = new PeerId($"Random Peer {i}");
                rt.Update(peers[i]);
            }

            var found = rt.NearestPeers(DhtId.ConvertPeerId(peers[2]), 15);
            Assert.Equal(15, found.Length);
        }

        [Fact]
        public void TestTableMultithreaded()
        {
            var local = new PeerId("Random Local Peer");
            var m = new Metrics();
            var rt = new RoutingTable(20, DhtId.ConvertPeerId(local), TimeSpan.FromHours(1), m);

            var peers = Enumerable.Range(0, 500)
                .Select(i => new PeerId($"Random Peer {i}"))
                .ToArray();

            var t1 = Task.Factory.StartNew(() =>
            {
                var rand = new Random(Environment.TickCount);
                for (var i = 0; i < 1000; i++)
                {
                    var n = rand.Next(peers.Length);
                    rt.Update(peers[n]);
                }
            });

            var t2 = Task.Factory.StartNew(() =>
            {
                var rand = new Random(Environment.TickCount);
                for (var i = 0; i < 1000; i++)
                {
                    var n = rand.Next(peers.Length);
                    rt.Update(peers[n]);
                }
            });

            var t3 = Task.Factory.StartNew(() =>
            {
                var rand = new Random(Environment.TickCount);
                for (var i = 0; i < 1000; i++)
                {
                    var n = rand.Next(peers.Length);
                    rt.Find(peers[n]);
                }
            });

            Task.WaitAll(t1, t2, t3);
        }
    }
}
