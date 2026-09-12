using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Chat Prefix", "OlegBatrudinov", "1.1.0")]
    [Description("Плагин для смены префикса игрока через /chat с поддержкой привилегий")]
    public class ChatPrefix : RustPlugin
    {
        // Структура для хранения конфига группы
        private class GroupConfig
        {
            public int Priority;
            public List<string> Prefixes = new List<string>();
        }

        private Dictionary<string, GroupConfig> config = new Dictionary<string, GroupConfig>();

        // Иерархия привилегий (снизу вверх)
        private static readonly List<string> PrivilegeHierarchy = new List<string>
        {
            "player",
            "vip",
            "premium",
            "ultra",
            "kotokbas",
            "moderator",
            "admin"
        };

        private const string YEBOK_PRIVILEGE = "yebok";

        private Dictionary<string, List<string>> PrefixesByPrivilege = new Dictionary<string, List<string>>();

        private static readonly Dictionary<string, string> PrefixColors = new Dictionary<string, string>
        {
            { "PLAYER", "#808080" },
            { "VIP", "#FFD700" },
            { "PREMIUM", "#1E90FF" },
            { "ULTRA", "#8A2BE2" },
            { "KOTOKBAS", "#FF1493" },
            { "YEBOK", "#8B4513" },
            { "MODERATOR", "#00FF00" },
            { "ADMIN", "#FF0000" }
        };

        private Dictionary<ulong, string> playerPrefixes = new Dictionary<ulong, string>();

        // ═══════════════════════════════════════════════════════════════════
        // ИНИЦИАЛИЗАЦИЯ
        // ═══════════════════════════════════════════════════════════════════

        private void OnServerInitialized()
        {
            LoadConfig();
            LoadPlayerPrefixes();

            permission.RegisterPermission("chatprefix.yebot", this);
            CreateGroupsIfNotExist();

            Puts("Chat Prefix плагин инициализирован");
            Puts(string.Format("Загружено {0} групп из конфига", config.Count));
            Puts(string.Format("Загружено {0} сохранённых префиксов", playerPrefixes.Count));
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player == null) return;

            if (!playerPrefixes.ContainsKey(player.userID))
            {
                if (permission.UserHasPermission(player.UserIDString, "chatprefix.yebot"))
                {
                    playerPrefixes[player.userID] = "YEBOK";
                }
                else
                {
                    playerPrefixes[player.userID] = "PLAYER";
                }
            }
        }

        private void Unload()
        {
            SavePlayerPrefixes();
            Puts("Chat Prefix плагин выгружен");
        }

        // ═══════════════════════════════════════════════════════════════════
        // КОНФИГУРАЦИЯ
        // ═══════════════════════════════════════════════════════════════════

        private void LoadConfig()
        {
            config.Clear();

            config["player"] = new GroupConfig { Priority = 1, Prefixes = new List<string> { "PLAYER" } };
            config["vip"] = new GroupConfig { Priority = 2, Prefixes = new List<string> { "VIP", "PLAYER" } };
            config["premium"] = new GroupConfig { Priority = 3, Prefixes = new List<string> { "PREMIUM", "VIP", "PLAYER" } };
            config["ultra"] = new GroupConfig { Priority = 4, Prefixes = new List<string> { "ULTRA", "PREMIUM", "VIP", "PLAYER" } };
            config["kotokbas"] = new GroupConfig { Priority = 5, Prefixes = new List<string> { "KOTOKBAS", "ULTRA", "PREMIUM", "VIP", "PLAYER" } };
            config["moderator"] = new GroupConfig { Priority = 6, Prefixes = new List<string> { "MODERATOR", "YEBOK", "KOTOKBAS", "ULTRA", "PREMIUM", "VIP", "PLAYER" } };
            config["admin"] = new GroupConfig { Priority = 7, Prefixes = new List<string> { "ADMIN", "MODERATOR", "YEBOK", "KOTOKBAS", "ULTRA", "PREMIUM", "VIP", "PLAYER" } };

            PrefixesByPrivilege.Clear();
            foreach (var group in config)
            {
                PrefixesByPrivilege[group.Key] = group.Value.Prefixes;
            }
        }

        private void CreateGroupsIfNotExist()
        {
            foreach (var groupName in config.Keys)
            {
                int priority = config[groupName].Priority;

                if (!permission.GroupExists(groupName))
                {
                    permission.CreateGroup(groupName, string.Format("Группа {0}", groupName), priority);
                    Puts(string.Format("Создана группа Oxide: {0}", groupName));
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // КОМАНДА /chat
        // ═══════════════════════════════════════════════════════════════════

        [ChatCommand("chat")]
        private void ChatCommand(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;

            if (args.Length == 0 || (args.Length == 1 && args[0].ToLower() == "prefix"))
            {
                ShowAvailablePrefixes(player);
                return;
            }

            string requestedPrefix = args[0];
            SetPlayerPrefix(player, requestedPrefix);
        }

        // ═══════════════════════════════════════════════════════════════════
        // МЕТОДЫ
        // ═══════════════════════════════════════════════════════════════════

        private string GetPlayerPrivilege(BasePlayer player)
        {
            if (player == null) return "player";

            for (int i = PrivilegeHierarchy.Count - 1; i >= 0; i--)
            {
                string priv = PrivilegeHierarchy[i];
                if (permission.UserHasGroup(player.UserIDString, priv))
                {
                    return priv;
                }
            }

            return "player";
        }

        private List<string> GetAvailablePrefixes(BasePlayer player)
        {
            if (player == null) return new List<string> { "PLAYER" };

            if (permission.UserHasPermission(player.UserIDString, "chatprefix.yebot"))
            {
                return new List<string> { "Idi nahuy yebok" };
            }

            string privilege = GetPlayerPrivilege(player);
            int privIndex = PrivilegeHierarchy.IndexOf(privilege);
            if (privIndex < 0) privIndex = 0;

            List<string> available = new List<string>();
            for (int i = 0; i <= privIndex; i++)
            {
                string groupName = PrivilegeHierarchy[i];
                if (PrefixesByPrivilege.ContainsKey(groupName))
                {
                    available.AddRange(PrefixesByPrivilege[groupName]);
                }
            }

            return available.Distinct().ToList();
        }

        private void ShowAvailablePrefixes(BasePlayer player)
        {
            List<string> available = GetAvailablePrefixes(player);

            string message = "Доступные префиксы:\n";
            foreach (var prefix in available)
            {
                if (prefix == "Idi nahuy yebok")
                {
                    message += string.Format("-{0}\n", prefix);
                }
                else
                {
                    string color = PrefixColors.ContainsKey(prefix) ? PrefixColors[prefix] : "#FFFFFF";
                    message += string.Format("-<color={0}>{1}</color>\n", color, prefix);
                }
            }

            SendPrivateMessage(player, message);
        }

        private void SetPlayerPrefix(BasePlayer player, string requestedPrefix)
        {
            if (player == null) return;

            if (permission.UserHasPermission(player.UserIDString, "chatprefix.yebot"))
            {
                SendPrivateMessage(player, "Ошибка — иди нахуй уебан");
                return;
            }

            List<string> available = GetAvailablePrefixes(player);

            bool found = false;
            foreach (var prefix in available)
            {
                if (prefix.Equals(requestedPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                SendPrivateMessage(player, string.Format("Ошибка: префикса '{0}' не существует или у вас нет прав на его использование", requestedPrefix));
                return;
            }

            string normalizedPrefix = requestedPrefix.ToUpper();
            playerPrefixes[player.userID] = normalizedPrefix;
            SavePlayerPrefixes();

            SendPrivateMessage(player, string.Format("Ваш префикс изменён на <color={0}>{1}</color>", PrefixColors[normalizedPrefix], normalizedPrefix));
        }

        private void SendPrivateMessage(BasePlayer player, string message)
        {
            if (player == null) return;
            player.SendConsoleCommand("chat.add", 0, 76561198000000000, message);
        }

        // ═══════════════════════════════════════════════════════════════════
        // СОХРАНЕНИЕ ДАННЫХ
        // ═══════════════════════════════════════════════════════════════════

        private void LoadPlayerPrefixes()
        {
            playerPrefixes = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, string>>("ChatPrefix/player_prefixes") ?? new Dictionary<ulong, string>();
        }

        private void SavePlayerPrefixes()
        {
            Interface.Oxide.DataFileSystem.WriteObject("ChatPrefix/player_prefixes", playerPrefixes);
        }

        public string GetPlayerCurrentPrefix(BasePlayer player)
        {
            if (player == null) return "PLAYER";

            if (!playerPrefixes.ContainsKey(player.userID))
            {
                return "PLAYER";
            }

            return playerPrefixes[player.userID];
        }

        public string GetPlayerPrefixColor(BasePlayer player)
        {
            string prefix = GetPlayerCurrentPrefix(player);
            return PrefixColors.ContainsKey(prefix) ? PrefixColors[prefix] : "#FFFFFF";
        }
    }
}
