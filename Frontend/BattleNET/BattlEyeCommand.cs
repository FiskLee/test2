/* * * * * * * * * * * * * * * * * * * * * * * * * * * * * *
 * BattleNET v1.3.4 - BattlEye Library and Client            *
 *                                                         *
 *  Copyright (C) 2018 by it's authors.                    *
 *  Some rights reserved. See license.txt, authors.txt.    *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */

using System;
using System.ComponentModel;
using System.Linq;

namespace BattleNET
{
    /// <summary>
    /// Represents available RCON commands for BattlEye server administration.
    /// Commands are organized by category for better maintainability.
    /// </summary>
    public enum BattlEyeCommand
    {
        #region Server Management
        /// <summary>
        /// #init - Reload server config file loaded by –config option.
        /// </summary>
        [Description("#init")]
        Init,

        /// <summary>
        /// #shutdown - Shuts down the server.
        /// </summary>
        [Description("#shutdown")]
        Shutdown,

        /// <summary>
        /// #restartserver - Restart server.
        /// </summary>
        [Description("#restartserver")]
        RestartServer,
        #endregion

        #region Mission Management
        /// <summary>
        /// #reassign - Start over and reassign roles.
        /// </summary>
        [Description("#reassign")]
        Reassign,

        /// <summary>
        /// #restart - Restart mission.
        /// </summary>
        [Description("#restart")]
        Restart,

        /// <summary>
        /// #mission [missionName] - Loads the given mission on the server.
        /// </summary>
        [Description("#mission ")]
        Mission,

        /// <summary>
        /// missions - Returns a list of the available missions on the server.
        /// </summary>
        [Description("missions")]
        Missions,
        #endregion

        #region Server Access Control
        /// <summary>
        /// #lock - Locks the server, prevents new clients from joining.
        /// </summary>
        [Description("#lock")]
        Lock,

        /// <summary>
        /// #unlock - Unlocks the server, allows new clients to join.
        /// </summary>
        [Description("#unlock")]
        Unlock,

        /// <summary>
        /// RConPassword [password] - Changes the RCon password.
        /// </summary>
        [Description("RConPassword ")]
        RConPassword,

        /// <summary>
        /// MaxPing [ping] - Changes the MaxPing value. If a player has a higher ping, he will be kicked from the server.
        /// </summary>
        [Description("MaxPing ")]
        MaxPing,
        #endregion

        #region Player Management
        /// <summary>
        /// kick [player#] - Kicks a player. His # can be found in the player list using the 'players' command.
        /// </summary>
        [Description("kick ")]
        Kick,

        /// <summary>
        /// players - Displays a list of the players on the server including BE GUIDs and pings.
        /// </summary>
        [Description("players")]
        Players,

        /// <summary>
        /// Say [player#] [msg] - Say something to player #. specially -1 equals all players on server (e.g. 'Say -1 Hello World').
        /// </summary>
        [Description("Say ")]
        Say,
        #endregion

        #region Ban Management
        /// <summary>
        /// loadBans - (Re)load the BE ban list from bans.txt.
        /// </summary>
        [Description("loadBans")]
        LoadBans,

        /// <summary>
        /// bans - Show a list of all BE server bans.
        /// </summary>
        [Description("bans")]
        Bans,

        /// <summary>
        /// ban [player #] [time in minutes] [reason] - Ban a player's BE GUID from the server. If time is not specified or 0, the ban will be permanent; if reason is not specified the player will be kicked with "Banned".
        /// </summary>
        [Description("ban ")]
        Ban,

        /// <summary>
        /// addBan [GUID] [time in minutes] [reason] - Same as "ban", but allows to ban a player that is not currently on the server.
        /// </summary>
        [Description("addBan ")]
        AddBan,

        /// <summary>
        /// removeBan [ban #] - Remove ban (get the ban # from the bans command).
        /// </summary>
        [Description("removeBan ")]
        RemoveBan,

        /// <summary>
        /// writeBans - Removes expired bans from bans file.
        /// </summary>
        [Description("writeBans")]
        WriteBans,
        #endregion

        #region Script Management
        /// <summary>
        /// loadScripts - Loads the scripts.txt file without the need to restart server.
        /// </summary>
        [Description("loadScripts")]
        LoadScripts,

        /// <summary>
        /// loadEvents - (Re)load createvehicle.txt, remoteexec.txt and publicvariable.txt
        /// </summary>
        [Description("loadEvents")]
        LoadEvents,
        #endregion

        #region Admin Management
        /// <summary>
        /// admins - Gets connected RCON clients.
        /// </summary>
        [Description("admins")]
        Admins,
        #endregion
    }

    /// <summary>
    /// Provides extension methods for BattlEyeCommand validation and formatting.
    /// </summary>
    public static class BattlEyeCommandExtensions
    {
        /// <summary>
        /// Gets the command string for the specified command.
        /// </summary>
        /// <param name="command">The command to get the string for.</param>
        /// <param name="parameters">The command parameters.</param>
        /// <returns>The formatted command string.</returns>
        public static string GetCommandString(this BattlEyeCommand command, params string[] parameters)
        {
            var commandString = GetDescription(command);
            if (string.IsNullOrEmpty(commandString))
            {
                throw new ArgumentException($"No command string found for command {command}");
            }

            if (parameters == null || parameters.Length == 0)
            {
                return commandString.TrimEnd();
            }

            // Validate parameters based on command type
            if (!ValidateParameters(command, parameters))
            {
                throw new ArgumentException($"Invalid parameters for command {command}");
            }

            // Format command with parameters
            return string.Format(commandString, parameters);
        }

        /// <summary>
        /// Validates the parameters for the specified command.
        /// </summary>
        /// <param name="command">The command to validate parameters for.</param>
        /// <param name="parameters">The parameters to validate.</param>
        /// <returns>True if the parameters are valid, false otherwise.</returns>
        public static bool ValidateParameters(this BattlEyeCommand command, string[] parameters)
        {
            if (parameters == null)
            {
                return false;
            }

            return command switch
            {
                // Server Management
                BattlEyeCommand.Init => parameters.Length == 0,
                BattlEyeCommand.Shutdown => parameters.Length == 0,
                BattlEyeCommand.RestartServer => parameters.Length == 0,

                // Mission Management
                BattlEyeCommand.Reassign => parameters.Length == 0,
                BattlEyeCommand.Restart => parameters.Length == 0,
                BattlEyeCommand.Mission => parameters.Length == 1 && !string.IsNullOrWhiteSpace(parameters[0]),
                BattlEyeCommand.Missions => parameters.Length == 0,

                // Server Access Control
                BattlEyeCommand.Lock => parameters.Length == 0,
                BattlEyeCommand.Unlock => parameters.Length == 0,
                BattlEyeCommand.RConPassword => parameters.Length == 1 && !string.IsNullOrWhiteSpace(parameters[0]),
                BattlEyeCommand.MaxPing => parameters.Length == 1 && int.TryParse(parameters[0], out int ping) && ping > 0 && ping <= 1000,

                // Player Management
                BattlEyeCommand.Kick => parameters.Length >= 1 && int.TryParse(parameters[0], out int playerId) && playerId >= 0,
                BattlEyeCommand.Players => parameters.Length == 0,
                BattlEyeCommand.Say => parameters.Length >= 2 && int.TryParse(parameters[0], out int targetId) && targetId >= -1,

                // Ban Management
                BattlEyeCommand.LoadBans => parameters.Length == 0,
                BattlEyeCommand.Bans => parameters.Length == 0,
                BattlEyeCommand.Ban => parameters.Length >= 1 && int.TryParse(parameters[0], out int banPlayerId) && banPlayerId >= 0,
                BattlEyeCommand.AddBan => parameters.Length >= 1 && !string.IsNullOrWhiteSpace(parameters[0]),
                BattlEyeCommand.RemoveBan => parameters.Length == 1 && int.TryParse(parameters[0], out int banId) && banId >= 0,
                BattlEyeCommand.WriteBans => parameters.Length == 0,

                // Script Management
                BattlEyeCommand.LoadScripts => parameters.Length == 0,
                BattlEyeCommand.LoadEvents => parameters.Length == 0,

                // Admin Management
                BattlEyeCommand.Admins => parameters.Length == 0,

                _ => false
            };
        }

        /// <summary>
        /// Gets the description for the specified command.
        /// </summary>
        /// <param name="command">The command to get the description for.</param>
        /// <returns>The command description.</returns>
        private static string GetDescription(BattlEyeCommand command)
        {
            var field = command.GetType().GetField(command.ToString());
            var attribute = field?.GetCustomAttributes(typeof(DescriptionAttribute), false)
                .FirstOrDefault() as DescriptionAttribute;
            return attribute?.Description ?? string.Empty;
        }
    }
}