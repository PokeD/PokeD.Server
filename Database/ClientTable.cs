﻿using System.Globalization;

using PokeD.Core.Extensions;
using PokeD.Server.Clients;
using PokeD.Server.Commands;
using PokeD.Server.Data;
using PokeD.Server.Extensions;

using SQLite;

namespace PokeD.Server.Database
{
    /// <summary>
    /// General Info.
    /// </summary>
    public sealed class ClientTable : IdatabaseTable
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; private set; }


        public Prefix Prefix { get; private set; }
        public PermissionFlags Permissions { get; private set; }
        public string Name { get; private set; }
        public string PasswordHash { get; private set; }

        public string Position { get; private set; }
        public string LevelFile { get; private set; }

        public string LastIP { get; private set; }
        public int LastConnectionTime { get; private set; }


        public ClientTable(){ }
        public ClientTable(Client client)
        {
            if (client.Id >= 0)
                Id = client.Id;

            Prefix = client.Prefix;
            Permissions = client.Permissions;
            Name = client.Nickname;
            PasswordHash = client.PasswordHash;

            Position = client.Position.ToPokeString(',', CultureInfo.InvariantCulture);
            LevelFile = client.LevelFile;

            LastIP = client.IP;

            LastConnectionTime = client.ConnectionTime.ToUnixTime();
        }
    }
}