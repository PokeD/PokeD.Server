﻿using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Aragas.Network.Packets;

using PCLExt.Network;
using PCLExt.Thread;

using PokeD.Core.Data.PokeD.Monster;
using PokeD.Core.Extensions;
using PokeD.Core.Packets.P3D.Chat;
using PokeD.Core.Packets.P3D.Server;
using PokeD.Core.Packets.P3D.Trade;
using PokeD.Server.Clients;
using PokeD.Server.Clients.P3D;
using PokeD.Server.Database;

namespace PokeD.Server
{
    public class ModuleP3D : ServerModule
    {
        protected override string ModuleFileName { get; } = "ModuleP3D";

        #region Settings

        public override bool Enabled { get; protected set; } = false;

        public override ushort Port { get; protected set; } = 15124;

        public string ServerName { get; protected set; } = "Put Server Name Here";

        public string ServerMessage { get; protected set; } = "Put Server Description Here";
        
        public int MaxPlayers { get; protected set; } = 1000;

        public bool ValidatePokemons { get; protected set; } = false;

        //public bool EncryptionEnabled { get; protected set; } = true;

        public bool MoveCorrectionEnabled { get; protected set; } = true;

        #endregion Settings

        private ITCPListener Listener { get; set; }

        private IThread PlayerWatcherThread { get; set; }
        private IThread PlayerCorrectionThread { get; set; }

        private ConcurrentDictionary<string, P3DPlayer[]> NearPlayers { get; } = new ConcurrentDictionary<string, P3DPlayer[]>();

        private bool IsDisposing { get; set; }


        public ModuleP3D(Server server) : base(server) { }


        public override bool Start()
        {
            if (!base.Start())
                return false;


            Logger.Log(LogType.Info, $"Starting {ModuleFileName}.");

            Listener = SocketServer.CreateTCP(Port);
            Listener.Start();

            if (MoveCorrectionEnabled)
            {
                PlayerWatcherThread = Thread.Create(PlayerWatcherCycle);
                PlayerWatcherThread.Name = "PlayerWatcherThread";
                PlayerWatcherThread.IsBackground = true;
                PlayerWatcherThread.Start();

                PlayerCorrectionThread = Thread.Create(PlayerCorrectionCycle);
                PlayerCorrectionThread.Name = "PlayerCorrectionThread";
                PlayerCorrectionThread.IsBackground = true;
                PlayerCorrectionThread.Start();
            }


            return true;
        }
        public override bool Stop()
        {
            if (!base.Stop())
                return false;


            Logger.Log(LogType.Info, $"Stopping {ModuleFileName}.");

            if (PlayerWatcherThread.IsRunning)
                PlayerWatcherThread.Abort();

            if (PlayerCorrectionThread.IsRunning)
                PlayerCorrectionThread.Abort();

            Dispose();


            return true;
        }


        public static long PlayerWatcherThreadTime { get; private set; }
        private void PlayerWatcherCycle()
        {
            var watch = Stopwatch.StartNew();
            while (!IsDisposing)
            {
                var players = new List<P3DPlayer>(Clients.Where(client => client is P3DPlayer).Cast<P3DPlayer>());

                foreach (var player in players.Where(player => player.LevelFile != null && !NearPlayers.ContainsKey(player.LevelFile)))
                    NearPlayers.TryAdd(player.LevelFile, null);

                foreach (var level in NearPlayers.Keys)
                {
                    var playerList = new List<P3DPlayer>();
                    foreach (var player in players.Where(player => level == player.LevelFile))
                        playerList.Add(player);

                    var array = playerList.ToArray();
                    NearPlayers.AddOrUpdate(level, array, (s, players1) => players1 = array);
                }



                if (watch.ElapsedMilliseconds < 400)
                {
                    PlayerWatcherThreadTime = watch.ElapsedMilliseconds;

                    var time = (int)(400 - watch.ElapsedMilliseconds);
                    if (time < 0) time = 0;
                    Thread.Sleep(time);
                }
                watch.Reset();
                watch.Start();
            }
        }

        public static long PlayerCorrectionThreadTime { get; private set; }
        private void PlayerCorrectionCycle()
        {
            var watch = Stopwatch.StartNew();
            while (!IsDisposing)
            {
                foreach (var nearPlayers in NearPlayers.Where(nearPlayers => nearPlayers.Value != null))
                    foreach (var player in nearPlayers.Value.Where(player => player.Moving))
                        foreach (var playerToSend in nearPlayers.Value.Where(playerToSend => player != playerToSend))
                        {
                            var packet = player.GetDataPacket();
                            packet.Origin = player.Id;
                            playerToSend.SendPacket(packet);
                        }



                if (watch.ElapsedMilliseconds < 5)
                {
                    PlayerCorrectionThreadTime = watch.ElapsedMilliseconds;

                    var time = (int)(5 - watch.ElapsedMilliseconds);
                    if (time < 0) time = 0;
                    Thread.Sleep(time);
                }
                watch.Reset();
                watch.Start();
            }
        }


        public override void AddClient(Client client)
        {
            // -- We assume the Client is a GameJolt or the Client's password is correct and no one is using the Client's name.

            if (IsGameJoltIdUsed(client as P3DPlayer))
            {
                client.Kick("You are already on server!");
                return;
            }
            SavePlayerGJ(client as P3DPlayer);


            ClientUpdate(client, true);


            // Send to player his Id
            client.SendPacket(new CreatePlayerPacket { Origin = -1, PlayerId = client.Id });
            // Send to player all Players Id
            foreach (var aClient in Server.GetAllClients())
            {
                client.SendPacket(new CreatePlayerPacket { Origin = -1, PlayerId = aClient.Id });
                var packet = aClient.GetDataPacket();
                packet.Origin = aClient.Id;
                client.SendPacket(packet);
            }
            // Send to Players player Id
            SendPacketToAll(new CreatePlayerPacket { Origin = -1, PlayerId = client.Id });
            var p = client.GetDataPacket();
            p.Origin = client.Id;
            SendPacketToAll(p);


            ClientConnected(client);


            base.AddClient(client);
        }
        public override void RemoveClient(Client client, string reason = "")
        {
            Clients.Remove(client);

            if (client.Id > 0)
            {
                ClientDisconnected(client);

                base.RemoveClient(client, reason);
            }
        }


        Stopwatch UpdateWatch = Stopwatch.StartNew();
        public override void Update()
        {
            if (Listener?.AvailableClients == true)
                Clients.Add(new P3DPlayer(Listener.AcceptTCPClient(), this));

            for (var i = Clients.Count - 1; i >= 0; i--)
                Clients[i]?.Update();


            if (UpdateWatch.ElapsedMilliseconds > 1000)
            {
                SendPacketToAll(new WorldDataPacket { Origin = -1, DataItems = Server.World.GenerateDataItems() });

                UpdateWatch.Reset();
                UpdateWatch.Start();
            }
        }


        public override void ClientConnected(Client client)
        {
            SendPacketToAll(new CreatePlayerPacket { Origin = -1, PlayerId = client.Id });
            var packet = client.GetDataPacket();
            packet.Origin = client.Id;
            SendPacketToAll(packet);
            SendPacketToAll(new ChatMessageGlobalPacket { Origin = -1, Message = $"Player {client.Name} joined the game!" });
        }
        public override void ClientDisconnected(Client client)
        {
            SendPacketToAll(new DestroyPlayerPacket { Origin = -1, PlayerId = client.Id });
            SendPacketToAll(new ChatMessageGlobalPacket { Origin = -1, Message = $"Player {client.Name} disconnected!" });
        }

        public override void SendPacketToAll(Packet packet)
        {
            for (var i = Clients.Count - 1; i >= 0; i--)
                Clients[i]?.SendPacket(packet);
        }

        public override void SendTradeRequest(Client sender, Monster monster, Client destClient, bool fromServer = false)
        {
            if (!fromServer)
                Server.NotifyClientTradeOffer(this, sender, monster, destClient);

            if (destClient is P3DPlayer)
                destClient.SendPacket(new TradeOfferPacket { Origin = sender.Id, DataItems = monster.ToDataItems() });
        }
        public override void SendTradeConfirm(Client sender, Client destClient, bool fromServer = false)
        {
            if (!fromServer)
                Server.NotifyClientTradeConfirm(this, destClient, sender);

            if (destClient is P3DPlayer)
                destClient.SendPacket(new TradeStartPacket { Origin = sender.Id });
        }
        public override void SendTradeCancel(Client sender, Client destClient, bool fromServer = false)
        {
            if (!fromServer)
                Server.NotifyClientTradeCancel(this, sender, destClient);

            if (destClient is P3DPlayer)
                destClient.SendPacket(new TradeQuitPacket { Origin = sender.Id });
        }

        public override void SendPosition(Client sender, bool fromServer = false)
        {
            if(!fromServer)
                Server.NotifyClientPosition(this, sender);

            var packet = sender.GetDataPacket();
            packet.Origin = sender.Id;
            SendPacketToAll(packet);
        }


        public bool ClientPasswordIsCorrect(Client client, string passwordHash)
        {
            ClientTable table;
            if ((table = Server.DatabaseGet<ClientTable>(client.Id)) != null)
            {
                if (table.PasswordHash == null)
                {
                    ClientUpdate(client, true);
                    return true;
                }

                return table.PasswordHash == passwordHash;
            }
            return false;
        }

        public bool SetClientId(Client client)
        {
            if (!Server.DatabaseSetClientId(client))
            {
                client.Kick("You are already on server!");
                return false;
            }
            return true;
        }

        private bool IsGameJoltIdUsed(P3DPlayer client)
        {
            if (!client.IsGameJoltPlayer)
                return false;

            for (var i = Clients.Count - 1; i >= 0; i--)
            {
                var player = (P3DPlayer) Clients[i];
                if (player != client && player.IsGameJoltPlayer && client.GameJoltId == player.GameJoltId)
                    return true;
            }

            return false;
        }
        private void SavePlayerGJ(P3DPlayer client)
        {
            if (client != null)
            {
                var obj = new ClientGJTable(client.Id, (int) client.GameJoltId);
                if (!Server.DatabaseFind<ClientGJTable>(obj.ClientId))
                    Server.DatabaseSet(obj);
                else
                    Server.DatabaseUpdate(obj);
            }
        }
        private void LoadPlayerGJ(P3DPlayer client)
        {
            if (client != null)
            {
                var obj = new ClientGJTable(client.Id, (int) client.GameJoltId);
                if (!Server.DatabaseFind<ClientGJTable>(obj.ClientId))
                    Server.DatabaseSet(obj);
                else
                    Server.DatabaseUpdate(obj);
            }
        }

        
        public override void Dispose()
        {
            if (IsDisposing)
                return;

            IsDisposing = true;


            for (var i = Clients.Count - 1; i >= 0; i--)
            {
                Clients[i]?.Kick("Server is closing!");
                Clients[i]?.Dispose();
            }
            Clients.Clear();

            NearPlayers.Clear();
        }
    }
}