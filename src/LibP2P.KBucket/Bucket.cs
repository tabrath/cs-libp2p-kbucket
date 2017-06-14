using System.Collections.Generic;
using System.Linq;
using System.Threading;
using LibP2P.Peer;
using LibP2P.Utilities.Extensions;

namespace LibP2P.KBucket
{
    public class Bucket
    {
        private readonly List<PeerId> _list;
        private readonly ReaderWriterLockSlim _rwlock;

        public PeerId[] Peers => _rwlock.Read(() => _list.ToArray());

        public int Length => _rwlock.Read(() => _list.Count);

        public Bucket()
            : this(new List<PeerId>())
        {
        }

        protected Bucket(List<PeerId> list)
        {
            _list = list;
            _rwlock = new ReaderWriterLockSlim();
        }

        public bool Has(PeerId id) => _rwlock.Read(() => _list.Contains(id));

        public void Remove(PeerId id) => _rwlock.Write(() => _list.Remove(id));

        public void MoveToFront(PeerId id) => _rwlock.Write(() =>
        {
            _list.Remove(id);
            _list.Insert(0, id);
        });

        public void PushFront(PeerId id) => _rwlock.Write(() => _list.Insert(0, id));

        public PeerId PopBack() => _rwlock.Write(() =>
        {
            var last = _list.Last();
            _list.Remove(last);
            return last;
        });

        public Bucket Split(int cpl, DhtId target) => _rwlock.Write(() =>
        {
            var output = new List<PeerId>();
            var newBuck = new Bucket(output);

            foreach (var e in _list)
            {
                var peerId = DhtId.ConvertPeerId(e);
                var peerCpl = DhtId.CommonPrefixLength(peerId, target);

                if (peerCpl > cpl)
                    output.Add(e);
            }

            _list.RemoveAll(output.Contains);

            return newBuck;
        });
    }
}
