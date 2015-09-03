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
                scene.Connected.Add(async peer => {
                    long id = peer.Id;
                    scene.Broadcast("id", id);
                    peer.Send("ids", clients.Keys);
                    clients.AddOrUpdate(id, peer, (key, oldValue) => peer);
                });

                scene.Disconnected.Add(async args => {
                    long id = args.Peer.Id;
                    IScenePeer _;
                    clients.TryRemove(id, out _);
                    scene.Broadcast("di", id);
                });

                scene.AddRoute("echo", packet =>
                {
                    packet.Connection.Send("echo", s => packet.Stream.CopyTo(s, (int)packet.Stream.Length), PacketPriority.HIGH_PRIORITY, PacketReliability.RELIABLE_ORDERED);
                });

                scene.AddRoute("transfert", packet =>
                {
                    Command cmd = packet.ReadObject<Command>();
                    cmd.senderId = packet.Connection.Id;

                    if (cmd.senderId != null && clients.ContainsKey(cmd.receiverId))
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

                scene.AddProcedure("rpc", async (reqCtx) =>
                {
                    var length = reqCtx.InputStream.Length;
                    var data = new byte[length];
                    reqCtx.InputStream.Read(data, 0, (int)length);
                    reqCtx.SendValue(data);
                });
            }, new Dictionary<string, string> { { "description", "Stormancer tester app." } });
        }
    }
}
