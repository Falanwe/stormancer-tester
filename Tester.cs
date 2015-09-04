using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Collections.Concurrent;

using Stormancer;
using Stormancer.Core;

namespace Tester
{
    public class Command
    {
        public long senderId;
        public long receiverId;
        public string action;
        public string options;
    }

    public class Startup : IStartup
    {
        private ConcurrentDictionary<long, IScenePeer> clients = new ConcurrentDictionary<long, IScenePeer>();

        public void Run(IAppBuilder builder)
        {
            builder.SceneTemplate("tester", scene =>
            {
                scene.Connected.Add(peer => {
                    long id = peer.Id;
                    scene.Broadcast("id", id);
                    peer.Send("ids", clients.Keys);
                    clients.AddOrUpdate(id, peer, (key, oldValue) => peer);
                    return Task.FromResult(true);
                });

                scene.Disconnected.Add(args => {
                    long id = args.Peer.Id;
                    IScenePeer _;
                    clients.TryRemove(id, out _);
                    scene.Broadcast("di", id);
                    return Task.FromResult(true);
                });

                scene.AddRoute("echo", packet =>
                {
                    packet.Connection.Send("echo", s => packet.Stream.CopyTo(s, (int)packet.Stream.Length), PacketPriority.HIGH_PRIORITY, PacketReliability.RELIABLE_ORDERED);
                });

                scene.AddRoute("transfert", packet =>
                {
                    Command cmd = packet.ReadObject<Command>();
                    cmd.senderId = packet.Connection.Id;

                    if (clients.ContainsKey(cmd.receiverId))
                    {
                        clients[cmd.receiverId].Send("transfert", cmd, PacketPriority.HIGH_PRIORITY, PacketReliability.RELIABLE_ORDERED);
                    }
                });

                scene.AddRoute("broadcast", packet =>
                {
                    Command cmd = packet.ReadObject<Command>();
                    cmd.senderId = packet.Connection.Id;

                    scene.Broadcast("broadcast", cmd, PacketPriority.HIGH_PRIORITY, PacketReliability.RELIABLE_ORDERED);
                });

                scene.AddProcedure("rpc", (reqCtx) =>
                {
                    var t = new TaskCompletionSource<bool>();
                    reqCtx.RemotePeer.Rpc("rpc", s => reqCtx.InputStream.CopyTo(s)).Subscribe(p =>
                    {
                        reqCtx.SendValue(s2 => p.Stream.CopyTo(s2));
                        t.SetResult(true);
                    });
                    return t.Task;
                });
            }, new Dictionary<string, string> { { "description", "Stormancer tester app." } });
        }
    }
}
