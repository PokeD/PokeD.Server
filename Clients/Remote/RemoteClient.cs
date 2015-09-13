using System;

using PokeD.Core.Data;
using PokeD.Core.Interfaces;
using PokeD.Core.Packets;
using PokeD.Core.Packets.Remote.Authorization;
using PokeD.Core.Packets.Shared;
using PokeD.Core.Wrappers;

using PokeD.Server.Exceptions;
using PokeD.Server.IO;

namespace PokeD.Server.Clients.Remote
{
    public partial class RemoteClient : IClient
    {
        public int ID { get; set; }
        public string Name { get; private set; }
        public string IP { get; private set; }
        public DateTime ConnectionTime { get; private set; }
        public bool UseCustomWorld { get; private set; }
        public long GameJoltId { get; }
        public bool IsGameJoltPlayer { get; private set; }

        IPacketStream Stream { get; }

        readonly Server _server;

        public RemoteClient(INetworkTCPClient client, Server server)
        {
            Stream = new P3DStream(client);
            _server = server;
        }

        public void Update()
        {
            if (Stream.Connected && Stream.DataAvailable > 0)
            {
                int packetId = 0;
                byte[] data = null;


                if (!CompressionEnabled)
                {
                    var packetLength = Stream.ReadVarInt();
                    if (packetLength == 0)
                        throw new ServerException("Remote Client reading error: Packet Length size is 0");

                    packetId = Stream.ReadVarInt();

                    data = Stream.ReadByteArray(packetLength - 1);
                }

                HandlePacket(packetId, data);
            }
        }

        /// <summary>
        /// Packets are handled here. Compression and encryption are handled here too
        /// </summary>
        /// <param name="id">Packet ID</param>
        /// <param name="data">Packet byte[] data</param>
        private void HandlePacket(int id, byte[] data)
        {
            using (var reader = new ProtobufDataReader(data))
            {
                if (RemoteResponse.Packets[id] == null)
                    throw new ServerException("RemoteClient eeading error: Wrong packet ID.");

                var packet = RemoteResponse.Packets[id]().ReadPacket(reader);

                HandlePacket(packet);
            }
        }

        private void HandlePacket(Packet packet)
        {
            switch ((RemotePacketTypes) packet.ID)
            {
                case RemotePacketTypes.AuthorizationRequestPacket:
                    HandleAuthorizationRequest((AuthorizationRequestPacket) packet);
                    break;

                case RemotePacketTypes.EncryptionRequestPacket:
                    HandleEncryptionRequest((EncryptionRequestPacket) packet);
                    break;

                case RemotePacketTypes.CompressionRequestPacket:
                    HandleCompressionRequest((CompressionRequestPacket) packet);
                    break;

            }
        }

        public void SendPacket(Packet packet)
        {
            if (Stream.Connected)
                Stream.SendPacket(ref packet);
        }


        public GameDataPacket GetDataPacket()
        {
            return null;
        }
        public DataItems GenerateDataItems()
        {
            throw new NotImplementedException();
        }


        public void SendPacket(Packet packet, int originID)
        {
            throw new NotImplementedException();
        }


        public void Dispose()
        {
            Stream?.Dispose();

            _server.RemovePlayer(this);
        }
    }
}