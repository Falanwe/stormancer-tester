using Stormancer;
using Stormancer.Core;
using Stormancer.Diagnostics;
using Stormancer.Plugins;
using Stormancer.Server.Components;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Base
{
    public class Command
    {
        public long senderId;
        public long receiverId;
        public string action;
        public string options;
    }

    public class TesterBehavior
    {
        public static TesterBehavior AddTesterBehaviorToScene(ISceneHost scene)
        {
            var result = new TesterBehavior(scene);

            scene.Starting.Add(result.OnStarting);

            scene.Connected.Add(result.OnConnected);

            scene.Disconnected.Add(result.OnDisconnected);

            scene.AddRoute("echo", result.OnEcho);

            scene.AddRoute("transfert", result.OnTransfert);

            scene.AddRoute("broadcast", result.OnBroadcast);

            scene.AddProcedure("rpc", result.OnRpc);

            scene.AddProcedure("rpcping", result.OnRpcPing);

            scene.Shuttingdown.Add(result.OnShutDown);

            return result;
        }

        private Task OnStarting(dynamic arg)
        {
            _isRunning = true;
            _queueTask = QueueTaskImpl();
            return Task.FromResult(true);
        }


        private async Task OnShutDown(ShutdownArgs arg)
        {
            _isRunning = false;
            await _queueTask;
        }

        private async  Task QueueTaskImpl()
        {
            while(_isRunning)
            {
                Task t;
                while(_queue.TryDequeue(out t))
                {
                    try
                    {
                        await t;
                    }
                    catch (Exception ex)
                    {
                        _scene.GetComponent<ILogger>().Log(LogLevel.Error, "rpc", "an exception occurred in RPC", ex);
                    }
                }
                await Task.Delay(1000);
            }
        }

        private TesterBehavior(ISceneHost scene)
        {
            _scene = scene;
        }

        private Task OnConnected(IScenePeer peer)
        {
            long id = peer.Id;
            _scene.Broadcast("id", id);
            peer.Send("ids", _clients.Keys);
            _clients.AddOrUpdate(id, peer, (key, oldValue) => peer);
            return Task.FromResult(true);
        }

        private Task OnDisconnected(DisconnectedArgs args)
        {
            long id = args.Peer.Id;
            IScenePeer _;
            _clients.TryRemove(id, out _);
            _scene.Broadcast("di", id);
            return Task.FromResult(true);
        }

        private void OnEcho(Packet<IScenePeerClient> packet)
        {
            packet.Connection.Send("echo", s => packet.Stream.CopyTo(s, (int)packet.Stream.Length), PacketPriority.HIGH_PRIORITY, PacketReliability.RELIABLE_ORDERED);
        }

        private void OnTransfert(Packet<IScenePeerClient> packet)
        {
            Command cmd = packet.ReadObject<Command>();
            cmd.senderId = packet.Connection.Id;
            if (_clients.ContainsKey(cmd.receiverId))
            {
                _clients[cmd.receiverId].Send("transfert", cmd, PacketPriority.HIGH_PRIORITY, PacketReliability.RELIABLE_ORDERED);
            }
        }

        private void OnBroadcast(Packet<IScenePeerClient> packet)
        {
            Command cmd = packet.ReadObject<Command>();
            cmd.senderId = packet.Connection.Id;
            _scene.Broadcast("broadcast", cmd, PacketPriority.HIGH_PRIORITY, PacketReliability.RELIABLE_ORDERED);
        }

        private Task OnRpc(RequestContext<IScenePeerClient> reqCtx)
        {
            _scene.GetComponent<ILogger>().Info("rpc", "rpc request received");
            reqCtx.SendValue(s => reqCtx.InputStream.CopyTo(s));
            _queue.Enqueue(Task.Run(async () =>
            {
                await Task.Delay(100);
                await reqCtx.RemotePeer.RpcTask<string, string>("rpc", "stormancer");
                _scene.GetComponent<ILogger>().Info("rpc", "rpc response received");
            }));

            return Task.FromResult(true);
        }

        private Task OnRpcPing(RequestContext<IScenePeerClient> reqCtx)
        {
            reqCtx.SendValue((ulong)_scene.GetComponent<IEnvironment>().Clock);
            return Task.FromResult(true);
        }

        private readonly ISceneHost _scene;
        private readonly ConcurrentDictionary<long, IScenePeer> _clients = new ConcurrentDictionary<long, IScenePeer>();
        private readonly ConcurrentQueue<Task> _queue = new ConcurrentQueue<Task>();
        private Task _queueTask;
        private bool _isRunning = false;
    }
}
