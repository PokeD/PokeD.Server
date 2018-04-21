using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Sockets;
using System.Threading;

using Aragas.Network.Packets;

using PokeD.Core.Data;
using PokeD.Core.Data.P3D;
using PokeD.Core.Extensions;
using PokeD.Core.IO;
using PokeD.Core.Packets.P3D;
using PokeD.Core.Packets.P3D.Battle;
using PokeD.Core.Packets.P3D.Chat;
using PokeD.Core.Packets.P3D.Client;
using PokeD.Core.Packets.P3D.Server;
using PokeD.Core.Packets.P3D.Shared;
using PokeD.Core.Packets.P3D.Trade;
using PokeD.Server.Chat;
using PokeD.Server.Commands;
using PokeD.Server.Data;
using PokeD.Server.Database;
using PokeD.Server.Modules;

namespace PokeD.Server.Clients.P3D
{
    public partial class P3DPlayer : Client<ModuleP3D>
    {
        private static CultureInfo CultureInfo => CultureInfo.InvariantCulture;

        #region P3D Values

        public override int ID { get; set; }

        public string GameMode { get; private set; }
        public bool IsGameJoltPlayer { get; private set; }
        public long GameJoltID { get; private set; }
        private char DecimalSeparator { get; set; }


        public override string Nickname { get; protected set; }


        public override string LevelFile { get; set; }
        public override Vector3 Position { get; set; }
        public int Facing { get; private set; }
        public bool Moving { get; private set; }

        public string Skin { get; private set; }
        public string BusyType { get; private set; }

        public bool PokemonVisible { get; private set; }
        public Vector3 PokemonPosition { get; private set; }
        public string PokemonSkin { get; private set; }
        public int PokemonFacing { get; private set; }

        #endregion P3D Values

        #region Values

        public override Prefix Prefix { get; protected set; }
        public override string PasswordHash { get; set; }

        public override string IP => Stream.Host;

        public override DateTime ConnectionTime { get; } = DateTime.Now;
        public override CultureInfo Language => new CultureInfo("en");
        public override PermissionFlags Permissions { get; set; } = PermissionFlags.UnVerified;

        private bool IsInitialized { get; set; }

        #endregion Values

        private BasePacketFactory<P3DPacket, int, P3DSerializer, P3DDeserializer> PacketFactory { get; }
        private P3DTransmission Stream { get; }

        private ConcurrentQueue<P3DPacket> PacketsToSend { get; } = new ConcurrentQueue<P3DPacket>();

#if DEBUG
        // -- Debug -- //
        private const int QueueSize = 1000;
        private Queue<P3DPacket> Received { get; } = new Queue<P3DPacket>(QueueSize);
        private Queue<P3DPacket> Sended { get; } = new Queue<P3DPacket>(QueueSize);
        // -- Debug -- //
#endif

        private bool IsDisposing { get; set; }

        public P3DPlayer(Socket socket, ModuleP3D module) : base(module)
        {
            PacketFactory = new PacketAttributeFactory<P3DPacket, int, P3DSerializer, P3DDeserializer>();
            Stream = new P3DTransmission(socket, PacketFactory);
        }

        public override void Update()
        {
            UpdateLock.Reset(); // Signal that the UpdateThread is alive.
            try
            {
                while (!UpdateToken.IsCancellationRequested && Stream.IsConnected)
                {
                    ConnectionLock.Reset(); // Signal that we are handling pending client data.
                    try
                    {
                        while (Stream.TryReadPacket(out var packetToReceive))
                        {
                            HandlePacket(packetToReceive);

#if DEBUG
                            Received.Enqueue(packetToReceive);
                            if (Received.Count >= QueueSize)
                                Received.Dequeue();
#endif
                        }

                        while (PacketsToSend.TryDequeue(out var packetToSend))
                        {
                            Stream.SendPacket(packetToSend);

#if DEBUG
                            Sended.Enqueue(packetToSend);
                            if (Sended.Count >= QueueSize)
                                Sended.Dequeue();
#endif
                        }
                    }
                    finally
                    {
                        ConnectionLock.Set(); // Signal that we are not handling anymore pending client data.
                    }

                    Thread.Sleep(100); // 100 calls per second should not be too often?
                }
            }
            finally
            {               
                UpdateLock.Set(); // Signal that the UpdateThread is finished

                if (!UpdateToken.IsCancellationRequested && !Stream.IsConnected) // Leave() if the update cycle stopped unexpectedly
                    Leave();
            }
        }

        private void HandlePacket(P3DPacket packet)
        {
            switch ((P3DPacketTypes) packet.ID)
            {
                case P3DPacketTypes.GameData:
                    HandleGameData((GameDataPacket) packet);
                    break;

                case P3DPacketTypes.ChatMessagePrivate:
                    HandlePrivateMessage((ChatMessagePrivatePacket) packet);
                    break;

                case P3DPacketTypes.ChatMessageGlobal:
                    HandleChatMessage((ChatMessageGlobalPacket) packet);
                    break;

                case P3DPacketTypes.Ping:
                    break;

                case P3DPacketTypes.GameStateMessage:
                    HandleGameStateMessage((GameStateMessagePacket) packet);
                    break;


                case P3DPacketTypes.TradeRequest:
                    HandleTradeRequest((TradeRequestPacket) packet);
                    break;

                case P3DPacketTypes.TradeJoin:
                    HandleTradeJoin((TradeJoinPacket) packet);
                    break;

                case P3DPacketTypes.TradeQuit:
                    HandleTradeQuit((TradeQuitPacket) packet);
                    break;

                case P3DPacketTypes.TradeOffer:
                    HandleTradeOffer((TradeOfferPacket) packet);
                    break;

                case P3DPacketTypes.TradeStart:
                    HandleTradeStart((TradeStartPacket) packet);
                    break;


                case P3DPacketTypes.BattleRequest:
                    HandleBattleRequest((BattleRequestPacket) packet);
                    break;

                case P3DPacketTypes.BattleJoin:
                    HandleBattleJoin((BattleJoinPacket) packet);
                    break;

                case P3DPacketTypes.BattleQuit:
                    HandleBattleQuit((BattleQuitPacket) packet);
                    break;

                case P3DPacketTypes.BattleOffer:
                    HandleBattleOffer((BattleOfferPacket) packet);
                    break;

                case P3DPacketTypes.BattleStart:
                    HandleBattleStart((BattleStartPacket) packet);
                    break;

                case P3DPacketTypes.BattleClientData:
                    HandleBattleClientData((BattleClientDataPacket) packet);
                    break;

                case P3DPacketTypes.BattleHostData:
                    HandleBattleHostData((BattleHostDataPacket) packet);
                    break;

                case P3DPacketTypes.BattleEndRoundData:
                    HandleBattlePokemonData((BattleEndRoundDataPacket) packet);
                    break;


                case P3DPacketTypes.ServerDataRequest:
                    HandleServerDataRequest((ServerDataRequestPacket) packet);
                    break;
            }
        }


        public override bool RegisterOrLogIn(string passwordHash)
        {
            if (base.RegisterOrLogIn(passwordHash))
            {
                Initialize();
                return true;
            }

            return false;
        }

        public override void SendPacket<TPacket>(Func<TPacket> func)
        {
            if (!(PacketFactory.Create(func) is P3DPacket packet))
                throw new Exception($"Wrong packet type, {typeof(TPacket).FullName}");

            PacketsToSend.Enqueue(packet);
        }
        public override void SendChatMessage(ChatChannel chatChannel, ChatMessage chatMessage) => SendPacket(() => new ChatMessageGlobalPacket { Origin = chatMessage.Sender.ID, Message = chatMessage.Message });
        public override void SendServerMessage(string text) => SendPacket(() => new ChatMessageGlobalPacket { Origin = Origin.Server, Message = text });
        public override void SendPrivateMessage(ChatMessage chatMessage) => SendPacket(() => new ChatMessagePrivatePacket { Origin = chatMessage.Sender.ID, DataItems = chatMessage.Message });

        public override void SendKick(string reason = "")
        {
            SendPacket(() => new KickedPacket { Origin = Origin.Server, Reason = reason });
            base.SendKick(reason);
        }
        public override void SendBan(BanTable banTable)
        {
            SendKick($"You have banned from this server; Reason: {banTable.Reason} Time left: {(banTable.UnbanTime - DateTime.UtcNow):%m} minutes; If you want to appeal your ban, please contact a staff member on the official forums (http://pokemon3d.net/forum/news/) or on the official Discord server (https://discord.me/p3d).");
            base.SendBan(banTable);
        }


        public override void Load(ClientTable data)
        {
            base.Load(data);

            Prefix = data.Prefix;
            Permissions = Permissions == PermissionFlags.UnVerified ? data.Permissions | PermissionFlags.UnVerified : data.Permissions;
            PasswordHash = data.PasswordHash;
        }

        
        private void Initialize()
        {
            if (!IsInitialized)
            {
                if ((Permissions & PermissionFlags.UnVerified) != PermissionFlags.None)
                    Permissions ^= PermissionFlags.UnVerified;

                if ((Permissions & PermissionFlags.User) == PermissionFlags.None)
                    Permissions |= PermissionFlags.User;

                Join();
                IsInitialized = true;
            }
        }

        private DataItems GenerateDataItems()
        {
            return new DataItems(
                GameMode,
                IsGameJoltPlayer ? "1" : "0",
                GameJoltID.ToString(CultureInfo),
                DecimalSeparator.ToString(),
                Name,
                LevelFile,
                Position.ToP3DString(DecimalSeparator, CultureInfo),
                Facing.ToString(CultureInfo),
                Moving ? "1" : "0",
                Skin,
                BusyType,
                PokemonVisible ? "1" : "0",
                PokemonPosition.ToP3DString(DecimalSeparator, CultureInfo),
                PokemonSkin,
                PokemonFacing.ToString(CultureInfo));
        }
        public override GameDataPacket GetDataPacket() => PacketFactory.Create(() => new GameDataPacket { Origin = ID, DataItems = GenerateDataItems() });


        protected override void Dispose(bool disposing)
        {
            if (!IsDisposing)
            {
                if (disposing)
                {
                    Stream.Dispose();

#if DEBUG
                    Sended.Clear();
                    Received.Clear();
#endif
                }


                IsDisposing = true;
            }
            base.Dispose(disposing);
        }
    }
}