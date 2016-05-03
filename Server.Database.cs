﻿using System.Diagnostics;
using System.Linq;

using PokeD.Server.Clients;
using PokeD.Server.Clients.PokeD;
using PokeD.Server.Data;
using PokeD.Server.DatabaseData;

namespace PokeD.Server
{
    public partial class Server
    {
        public int DatabasePlayerGetID(Client player)
        {
            if (AllClients().Any(p => p.Name == player.Name))
                return -1;

            var data = BaseDatabase.Find<Player>(p => p.Name == player.Name);
            if (data != null)
            {
                player.ID = data.Id;
                return player.ID;
            }
            else
            {
                BaseDatabase.Insert(new Player(player));
                return DatabasePlayerGetID(player);
            }
        }

        Stopwatch DatabasePlayerWatch { get; } = Stopwatch.StartNew();
        public void DatabasePlayerSave(Client player, bool forceUpdate = false)
        {
            if (player.ID == 0)
                return;

            if (DatabasePlayerWatch.ElapsedMilliseconds < 2000 && !forceUpdate)
                return;

            BaseDatabase.Update(new Player(player));

            DatabasePlayerWatch.Reset();
            DatabasePlayerWatch.Start();
        }

        public bool DatabasePlayerLoad(Client player)
        {
            if (AllClients().Any(p => p.Name == player.Name))
                return false;

            var data = BaseDatabase.Find<Player>(p => p.Name == player.Name);


            if (data != null && data.PasswordHash == null)
            {
                BaseDatabase.Update(new Player(player));
                return true;
            }
            else if (data != null)
            {
                if (data.PasswordHash == player.PasswordHash)
                {
                    player.LoadFromDB(data);
                    return true;
                }
                else
                    return false;
            }
            else
            {
                BaseDatabase.Insert(new Player(player));
                player.LoadFromDB(BaseDatabase.Find<Player>(p => p.Name == player.Name));

                return true;
            }
        }


        public bool DatabaseBatteSave(BattleInstance battleInstance)
        {
            BaseDatabase.Insert(new Battle(battleInstance));
            return true;
        }


        public bool DatabaseTradeSave(TradeInstance tradeInstance)
        {
            BaseDatabase.Insert(new Trade(BaseDatabase, tradeInstance));
            return true;
        }
    }
}
