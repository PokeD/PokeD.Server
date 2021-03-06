﻿using System;
using System.Collections.Generic;

using PokeD.Core.Services;
using PokeD.Server.Clients;

namespace PokeD.Server.Commands
{
    public class SetTimeCommand : Command
    {
        public override string Name => "settime";
        public override string Description => "Set World Time.";
        public override IEnumerable<string> Aliases => new [] { "st" };
        public override PermissionFlags Permissions => PermissionFlags.ModeratorOrHigher;

        public SetTimeCommand(IServiceContainer componentManager) : base(componentManager) { }

        public override void Handle(Client client, string alias, string[] arguments)
        {
            if (arguments.Length == 1)
            {
                if (string.Equals(arguments[0], "real", StringComparison.OrdinalIgnoreCase))
                {
                    World.UseRealTime = !World.UseRealTime;
                    client.SendServerMessage(World.UseRealTime ? "Enabled Real Time!" : "Disabled Real Time!");
                    return;
                }
                if (string.Equals(arguments[0], "daycycle", StringComparison.OrdinalIgnoreCase))
                {
                    World.DoDayCycle = !World.DoDayCycle;
                    client.SendServerMessage(World.DoDayCycle ? "Enabled Day Cycle!" : "Disabled Day Cycle!");
                    return;
                }

                if (TimeSpan.TryParseExact(arguments[0], "HH\\:mm\\:ss", null, out var time))
                {
                    World.CurrentTime = time;
                    World.UseRealTime = false;
                    client.SendServerMessage($"Set time to {time}!");
                    client.SendServerMessage("Disabled Real Time!");
                }
                else
                    client.SendServerMessage("Invalid time!");
            }
            else
                client.SendServerMessage("Invalid arguments given.");
        }

        public override void Help(Client client, string alias) => client.SendServerMessage($"Correct usage is /{alias} <Time[HH:mm:ss]/Real>");
    }
}