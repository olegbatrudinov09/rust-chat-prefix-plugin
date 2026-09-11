using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Chat Prefix", "OlegBatrudinov", "1.0.0")]
    [Description("Плагин для смены префикса игрока через /chat с поддержкой привилегий")]
    public class ChatPrefix : RustPlugin
    {
        // ═══════════════════════════════════════════════════════════════════
        // КОНФИГУРАЦИЯ И КОНСТАНТЫ
        // ═══════════════════════════════════════════════════════════════════

        // Иерархия привилегий (снизу вверх) - каждая привилегия включает в себя все ниже
        private static readonly List<string> PrivilegeHierarchy = new List<string>
        {
            "player",      // 0 - дефолт
            "vip",         // 1
            "premium",     // 2
            "ultra",       // 3
            "kotokbas",    // 4
            "moderator",   // 5
            "admin"        // 6
        };

        // Специальная привилегия YEBOK
        private const string YEBOK_PRIVILEGE = "yebok";

        // Доступные префиксы для каждой привилегии
        private static readonly Dictionary<string, List<string>> PrefixesByPrivilege = new Dictionary<string, List<string>>
        {
            { "player", new List<string> { "PLAYER" } },
            { "vip", new List<string> { "VIP", "PLAYER" } },
            { "premium", new List<string> { "PREMIUM", "VIP", "PLAYER" } },
            { "ultra", new List<string> { "ULTRA", "PREMIUM", "VIP", "PLAYER" } },
            { "kotokbas", new List<string> { "KOTOKBAS", "ULTRA", "PREMIUM", "VIP", "PLAYER" } },
            { "moderator", new List<string> { "MODERATOR", "YEBOK", "KOTOKBAS", "ULTRA", "PREMIUM", "VIP", "PLAYER" } },
            { "admin", new List<string> { "ADMIN", "MODERATOR", "YEBOK", "KOTOKBAS", "ULTRA", "PREMIUM", "VIP", "PLAYER" } }
        };

        // Цвета префиксов в hex формате для Rust чата
        private static readonly Dictionary<string, string> PrefixColors = new Dictionary<string, string>
        {
            { "PLAYER", "#808080" },      // серый
            { "VIP", "#FFD700" },         // жёлтый
            { "PREMIUM", "#1E90FF" },     // синий
            { "ULTRA", "#8A2BE2" },       // фиолетовый
            { "KOTOKBAS", "#FF1493" },    // розовый (неоновый)
            { "YEBOK", "#8B4513" },       // коричневый
            { "MODERATOR", "#00FF00" },   // зелёный
            { "ADMIN", "#FF0000" }        // красный
        };

        // Хранилище текущих префиксов игроков в памяти
        private Dictionary<ulong, string> playerPrefixes = new Dictionary<ulong, string>();

        // ═══════════════════════════════════════════════════════════════════
        // ЖИЗНЕННЫЙ ЦИКЛ ПЛАГИНА
        // ═══════════════════════════════════════════════════════════════════

        private void OnServerInitialized()
        {
            // Загружаем сохранённые префиксы из Oxide Data файла
            LoadPlayerPrefixes();

            // Регистрируем разрешение для особой привилегии YEBOK
            permission.RegisterPermission("chatprefix.yebok", this);

            Puts("Chat Prefix плагин инициализирован");
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player == null) return;

            // Если префикс игрока не загружен, загружаем его (или устанавливаем дефолт)
            if (!playerPrefixes.ContainsKey(player.userID))
            {
                // Если у игрока есть привилегия YEBOK - принудительно устанавливаем "yebok"
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
            // Сохраняем всё перед выгрузкой
            SavePlayerPrefixes();
            Puts("Chat Prefix плагин выгружен");
        }

        // ═══���═══════════════════════════════════════════════════════════════
        // КОМАНДА /chat
        // ═══════════════════════════════════════════════════════════════════

        [ChatCommand("chat")]
        private void ChatCommand(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;

            // Если аргументов нет или первый аргумент "prefix" - показываем список доступных префиксов
            if (args.Length == 0 || (args.Length == 1 && args[0].ToLower() == "prefix"))
            {
                ShowAvailablePrefixes(player);
                return;
            }

            // Иначе пытаемся установить выбранный префикс
            string requestedPrefix = args[0];
            SetPlayerPrefix(player, requestedPrefix);
        }

        // ═══════════════════════════════════════════════════════════════════
        // ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Определяет самую высокую привилегию игрока из иерархии
        /// </summary>
        private string GetPlayerPrivilege(BasePlayer player)
        {
            if (player == null) return "player";

            // Проверяем от самой высокой привилегии к самой низкой
            for (int i = PrivilegeHierarchy.Count - 1; i >= 0; i--)
            {
                string priv = PrivilegeHierarchy[i];
                if (permission.UserHasGroup(player.UserIDString, priv))
                {
                    return priv;
                }
            }

            return "player"; // дефолт
        }

        /// <summary>
        /// Получает все доступные префиксы для игрока (включая по привилегии и выше)
        /// </summary>
        private List<string> GetAvailablePrefixes(BasePlayer player)
        {
            if (player == null) return new List<string> { "PLAYER" };

            // Если у игрока есть YEBOK - он видит только фейк-префикс
            if (permission.UserHasPermission(player.UserIDString, "chatprefix.yebot"))
            {
                return new List<string> { "Idi nahuy yebok" };
            }

            string privilege = GetPlayerPrivilege(player);
            
            // Получаем индекс привилегии в иерархии
            int privIndex = PrivilegeHierarchy.IndexOf(privilege);
            if (privIndex < 0) privIndex = 0;

            // Собираем все доступные префиксы от дефолта до текущей привилегии (и выше)
            List<string> available = new List<string>();
            for (int i = 0; i <= privIndex; i++)
            {
                if (PrefixesByPrivilege.ContainsKey(PrivilegeHierarchy[i]))
                {
                    available.AddRange(PrefixesByPrivilege[PrivilegeHierarchy[i]]);
                }
            }

            // Убираем дубликаты
            return available.Distinct().ToList();
        }

        /// <summary>
        /// Показывает игроку список доступных ему префиксов (колонка с дефисами)
        /// </summary>
        private void ShowAvailablePrefixes(BasePlayer player)
        {
            List<string> available = GetAvailablePrefixes(player);

            string message = "Доступные префиксы:\n";
            foreach (var prefix in available)
            {
                // Если у игрока есть YEBOK и это фейк-префикс - выводим как есть
                if (prefix == "Idi nahuy yebok")
                {
                    message += $"-{prefix}\n";
                }
                else
                {
                    // Получаем цвет префикса и применяем его
                    string color = PrefixColors.ContainsKey(prefix) ? PrefixColors[prefix] : "#FFFFFF";
                    message += $"-<color={color}>{prefix}</color>\n";
                }
            }

            SendPrivateMessage(player, message);
        }

        /// <summary>
        /// Устанавливает выбранный префикс игроку
        /// </summary>
        private void SetPlayerPrefix(BasePlayer player, string requestedPrefix)
        {
            if (player == null) return;

            // Если у игрока есть привилегия YEBOK - заблокирован на "yebok"
            if (permission.UserHasPermission(player.UserIDString, "chatprefix.yebot"))
            {
                SendPrivateMessage(player, "Ошибка — иди нахуй уебан");
                return;
            }

            List<string> available = GetAvailablePrefixes(player);

            // Проверяем, существует ли такой префикс вообще
            // и доступен ли он для данного игрока
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
                SendPrivateMessage(player, $"Ошибка: префикса \"{requestedPrefix}\" не существует или у вас нет прав на его использование");
                return;
            }

            // Нормализуем название префикса (в верхнем регистре)
            string normalizedPrefix = requestedPrefix.ToUpper();

            // Устанавливаем префикс
            playerPrefixes[player.userID] = normalizedPrefix;

            // Сохраняем в data файл
            SavePlayerPrefixes();

            SendPrivateMessage(player, $"Ваш префикс изменён на <color={PrefixColors[normalizedPrefix]}>{normalizedPrefix}</color>");
        }

        /// <summary>
        /// Отправляет приватное сообщение игроку
        /// </summary>
        private void SendPrivateMessage(BasePlayer player, string message)
        {
            if (player == null) return;
            player.SendConsoleCommand("chat.add", 0, 76561198000000000, message);
        }

        // ═══════════════════════════════════════════════════════════════════
        // СОХРАНЕНИЕ И ЗАГРУЗКА ДАННЫХ
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Загружает сохранённые префиксы из Oxide Data файла
        /// </summary>
        private void LoadPlayerPrefixes()
        {
            playerPrefixes = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, string>>("ChatPrefix/player_prefixes") ?? new Dictionary<ulong, string>();
            Puts($"Загружено {playerPrefixes.Count} сохранённых префиксов");
        }

        /// <summary>
        /// Сохраняет текущие префиксы в Oxide Data файл
        /// </summary>
        private void SavePlayerPrefixes()
        {
            Interface.Oxide.DataFileSystem.WriteObject("ChatPrefix/player_prefixes", playerPrefixes);
        }

        /// <summary>
        /// Получает текущий префикс игрока
        /// </summary>
        public string GetPlayerCurrentPrefix(BasePlayer player)
        {
            if (player == null) return "PLAYER";
            
            if (!playerPrefixes.ContainsKey(player.userID))
            {
                return "PLAYER";
            }

            return playerPrefixes[player.userID];
        }

        /// <summary>
        /// Получает цвет текущего префикса игрока
        /// </summary>
        public string GetPlayerPrefixColor(BasePlayer player)
        {
            string prefix = GetPlayerCurrentPrefix(player);
            return PrefixColors.ContainsKey(prefix) ? PrefixColors[prefix] : "#FFFFFF";
        }
    }
}
