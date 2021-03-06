﻿using SQLite;

namespace PokeD.Server.Database
{
    public sealed class ClientChannelTable : IDatabaseTable
    {
        [PrimaryKey]
        public int ClientID { get; set; }

        public int Channel { get; set; }


        public ClientChannelTable() { }
        public ClientChannelTable(int clientID, int channel)
        {
            ClientID = clientID;

            Channel = channel;
        }
    }
}