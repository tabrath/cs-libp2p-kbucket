﻿using System;
using System.Linq;
using System.Threading;
using LibP2P.Peer;
using LibP2P.Peer.Store;
using LibP2P.Utilities.Extensions;

namespace LibP2P.KBucket
{
    public class RoutingTable
    {
        private readonly DhtId _local;
        private readonly ReaderWriterLockSlim _rwLock;
        private readonly IMetrics _metrics;
        private readonly TimeSpan _maxLatency;
        private readonly int _bucketSize;

        public Bucket[] Buckets { get; }
        public int Size => _rwLock.Read(() => Buckets.Sum(b => b.Length));
        public PeerId[] Peers => _rwLock.Read(() => Buckets.SelectMany(b => b.Peers).ToArray());

        public RoutingTable(int bucketSize, DhtId localId, TimeSpan latency, IMetrics metrics)
        {
            Buckets = new[] { new Bucket() };
            _bucketSize = bucketSize;
            _local = localId;
            _maxLatency = latency;
            _metrics = metrics;
            _rwLock = new ReaderWriterLockSlim();
        }

        public void Update(PeerId p)
        {
            var peerId = DhtId.ConvertPeerId(p);
            var cpl = DhtId.CommonPrefixLength(peerId, _local);
            _rwLock.Write(() =>
            {
                var bucketId = cpl;
                if (bucketId >= Buckets.Length)
                    bucketId = Buckets.Length - 1;

                var bucket = Buckets[bucketId];
                if (bucket.Has(p))
                {
                    bucket.MoveToFront(p);
                    return;
                }

                if (_metrics.LatencyEWMA(p) > _maxLatency)
                    return;

                bucket.PushFront(p);

                if (bucket.Length > _bucketSize)
                {
                    if (bucketId == Buckets.Length - 1)
                    {
                        NextBucket();
                    }
                    else
                    {
                        bucket.PopBack();
                    }
                }
            });
        }

        public void Remove(PeerId p)
        {
            _rwLock.Write(() =>
            {
                var peerId = DhtId.ConvertPeerId(p);
                var cpl = DhtId.CommonPrefixLength(peerId, _local);

                var bucketId = cpl;
                if (bucketId >= Buckets.Length)
                    bucketId = Buckets.Length - 1;

                var bucket = Buckets[bucketId];
                bucket.Remove(p);
            });
        }

        public PeerId NextBucket()
        {
            var bucket = Buckets[Buckets.Length - 1];
            var newBucket = bucket.Split(Buckets.Length - 1, _local);
            if (newBucket.Length > _bucketSize)
                return NextBucket();

            if (bucket.Length > _bucketSize)
                return bucket.PopBack();

            return null;
        }

        public PeerId Find(PeerId id)
        {
            var srch = NearestPeers(DhtId.ConvertPeerId(id), 1);
            if (srch.Length == 0 || srch[0] != id)
                return null;

            return srch[0];
        }

        public PeerId NearestPeer(DhtId id)
        {
            var peers = NearestPeers(id, 1);
            if (peers.Length > 0)
                return peers[0];

            return null;
        }

        public PeerId[] NearestPeers(DhtId id, int count)
        {
            var cpl = DhtId.CommonPrefixLength(id, _local);

            return _rwLock.Read(() =>
            {
                if (cpl >= Buckets.Length)
                    cpl = Buckets.Length - 1;

                var bucket = Buckets[cpl];
                var peers = Util.CopyPeersFromList(id, bucket.Peers).ToArray();
                if (peers.Length < count)
                {
                    if (cpl > 0)
                    {
                        var plist = Buckets[cpl - 1].Peers;
                        peers = Util.CopyPeersFromList(id, plist).ToArray();
                    }

                    if (cpl < Buckets.Length - 1)
                    {
                        var plist = Buckets[cpl + 1].Peers;
                        peers = Util.CopyPeersFromList(id, plist).ToArray();
                    }
                }
                return peers.OrderBy(p => p.Distance, new Util.DistanceComparer())
                    .Take(count)
                    .Select(p => p.P).ToArray();
            });
        }
    }
}
