﻿using System;
using System.Collections.Generic;
using System.Globalization;

using PCLExt.Config;

using PokeD.Core.Data;
using PokeD.Core.Data.P3D;
using PokeD.Core.Packets.P3D.Shared;
using PokeD.Core.Services;
using PokeD.Server.Chat;
using PokeD.Server.Clients;
using PokeD.Server.Data;
using PokeD.Server.Database;
using PokeD.Server.Modules;
using PokeD.Server.Services;

namespace PokeD.Server.Commands
{
    public abstract class Command
    {
        private sealed class OfflineServerModule : ServerModule
        {
            public OfflineServerModule(IServiceContainer componentManager) : base(componentManager, 0) { }

            protected override string ComponentName { get; }
            protected override IConfigFile ComponentConfigFile { get; }
            public override bool Enabled { get; protected set; }
            public override ushort Port { get; protected set; }

            public override void Update() { }
            
            public override void ClientsForeach(Action<IReadOnlyList<Client>> func) { }
            public override TResult ClientsSelect<TResult>(Func<IReadOnlyList<Client>, TResult> func) => default;
            public override IReadOnlyList<TResult> ClientsSelect<TResult>(Func<IReadOnlyList<Client>, IReadOnlyList<TResult>> func) => new List<TResult>();

            public override void OnTradeRequest(Client sender, DataItems monster, Client destClient) { }
            public override void OnTradeConfirm(Client sender, Client destClient) { }
            public override void OnTradeCancel(Client sender, Client destClient) { }

            public override void OnPosition(Client sender) { }
        }
        private sealed class OfflineClient : Client
        {
            private IServiceContainer ServiceContainer { get; }
            private DatabaseService Database => ServiceContainer.GetService<DatabaseService>();


            private int _id;
            public override int ID
            {
                get => _id;
                set { _id = value; Database.DatabaseUpdate(new ClientTable(this)); }
            }

            private string _nickname;
            public override string Nickname
            {
                get => _nickname;
                protected set { _nickname = value; Database.DatabaseUpdate(new ClientTable(this)); }
            }

            private Prefix _prefix;
            public override Prefix Prefix
            {
                get => _prefix;
                protected set { _prefix = value; Database.DatabaseUpdate(new ClientTable(this)); }
            }

            private string _passwordHash;
            public override string PasswordHash
            {
                get => _passwordHash;
                set { _passwordHash = value; Database.DatabaseUpdate(new ClientTable(this)); }
            }

            private PermissionFlags _permissions;
            public override PermissionFlags Permissions
            {
                get => _permissions;
                set { _permissions = value; Database.DatabaseUpdate(new ClientTable(this)); }
            }

            public override Vector3 Position { get; set; } = Vector3.Zero;
            public override string LevelFile { get; set; } = string.Empty;
            public override string IP { get; } = string.Empty;
            public override DateTime ConnectionTime { get; } = DateTime.MinValue;
            public override CultureInfo Language { get; }
            public override GameDataPacket GetDataPacket() => null;


            public OfflineClient(IServiceContainer serviceContainer, ClientTable clientTable) : base(new OfflineServerModule(serviceContainer))
            {
                ServiceContainer = serviceContainer;

                ID = clientTable.ClientID.Value;
                Nickname = clientTable.Name;
                Prefix = clientTable.Prefix;
                PasswordHash = clientTable.PasswordHash;
                Permissions = clientTable.Permissions;
            }


            public override bool RegisterOrLogIn(string passwordHash) => false;
            public override bool ChangePassword(string oldPassword, string newPassword) => false;

            public override void SendPacket<TPacket>(TPacket packet) { }

            public override void SendChatMessage(ChatChannel chatChannel, ChatMessage chatMessage) { }
            public override void SendServerMessage(string text) { }
            public override void SendPrivateMessage(ChatMessage chatMessage) { }

            public override void Load(ClientTable data) { }


            public override void Update() { }
        }

        // -- Should be hidden ideally
        protected ModuleManagerService ModuleManager => ServiceContainer.GetService<ModuleManagerService>();
        protected CommandManagerService CommandManager => ServiceContainer.GetService<CommandManagerService>();
        protected ChatChannelManagerService ChatChannelManager => ServiceContainer.GetService<ChatChannelManagerService>();
        // -- Should be hidden ideally

        private IServiceContainer ServiceContainer { get; }
        protected WorldService World => ServiceContainer.GetService<WorldService>();

        protected Client GetClient(string name)
        {
            var client = ModuleManager.GetClient(name);
            if (client != null)
                return client;
            
            var clientTable = ServiceContainer.GetService<DatabaseService>().DatabaseFind<ClientTable>(c => c.Name == name);
            return clientTable == null ? null : new OfflineClient(ServiceContainer, clientTable);
        }


        public abstract string Name { get; }
        public abstract string Description { get; }
        public virtual IEnumerable<string> Aliases { get; } = Array.Empty<string>();
        public virtual PermissionFlags Permissions { get; } = PermissionFlags.None;
        public virtual bool LogCommand { get; } = true;

        protected Command(IServiceContainer serviceContainer) { ServiceContainer = serviceContainer; }

        public virtual void Handle(Client client, string alias, string[] arguments) { Help(client, alias); }

        public virtual void Help(Client client, string alias) { client.SendServerMessage($@"Command ""{alias}"" is not functional!"); }
    }
}