using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using LibP2P.Peer;
using LibP2P.Utilities.Extensions;

[assembly:InternalsVisibleTo("LibP2P.KBucket.Tests")]

namespace LibP2P.KBucket
{
    internal class Util
    {
        public class PeerDistance
        {
            public PeerId P { get; set; }
            public DhtId Distance { get; set; }
        }

        public static IEnumerable<PeerDistance> CopyPeersFromList(DhtId target, IEnumerable<PeerId> peers) => peers
            .Select(peer => new { peer, pid = DhtId.ConvertPeerId(peer) })
            .Select(@t => new PeerDistance { P = @t.peer, Distance = ((byte[]) target).XOR(@t.pid) });

        public static IEnumerable<PeerId> SortClosestPeers(IEnumerable<PeerId> peers, DhtId target) => CopyPeersFromList(target, peers)
            .OrderBy(p => p.Distance, new DistanceComparer())
            .Select(p => p.P);

        internal class DistanceComparer : IComparer<DhtId>
        {
            public int Compare(DhtId x, DhtId y) => x?.CompareTo(y) ?? -1;
        }
    }
}
