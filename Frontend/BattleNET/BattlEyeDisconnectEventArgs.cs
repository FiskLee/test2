﻿/* * * * * * * * * * * * * * * * * * * * * * * * * * * * * *
 * BattleNET v1.3.4 - BattlEye Library and Client            *
 *                                                         *
 *  Copyright (C) 2018 by it's authors.                    *
 *  Some rights reserved. See license.txt, authors.txt.    *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */

using System;
using ArmaReforgerServerMonitor.Frontend.BattleNET.Helpers;

namespace BattleNET
{
    public delegate void BattlEyeConnectEventHandler(BattlEyeConnectEventArgs args);
    public delegate void BattlEyeDisconnectEventHandler(BattlEyeDisconnectEventArgs args);

    public class BattlEyeConnectEventArgs : EventArgs
    {
        public BattlEyeConnectEventArgs(BattlEyeLoginCredentials loginDetails, BattlEyeConnectionResult connectionResult)
        {
            LoginDetails = loginDetails;
            ConnectionResult = connectionResult;
            Message = connectionResult.Message ?? connectionResult.GetMessage();
        }

        public BattlEyeLoginCredentials LoginDetails { get; }
        public BattlEyeConnectionResult ConnectionResult { get; }
        public string Message { get; }
    }

    public class BattlEyeDisconnectEventArgs : EventArgs
    {
        public BattlEyeDisconnectEventArgs(BattlEyeLoginCredentials loginDetails, BattlEyeDisconnectionType? disconnectionType)
        {
            LoginDetails = loginDetails;
            DisconnectionType = disconnectionType;
            // Fix: Pass a non-null enum value; if disconnectionType is null, use an empty string.
            Message = disconnectionType.HasValue ? Helpers.StringValueOf(disconnectionType.Value) : string.Empty;
        }

        public BattlEyeLoginCredentials LoginDetails { get; }
        public BattlEyeDisconnectionType? DisconnectionType { get; }
        public string Message { get; }
    }
}
