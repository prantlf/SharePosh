// Copyright (C) 2012 Ferdinand Prantl <prantlf@gmail.com>
// All rights reserved.       
//
// This file is part of SharePosh - SharePoint drive provider for PowerShell.
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Management.Automation;
using System.Net;
using MSDN.Samples.ClaimsAuth;

namespace SharePosh
{
    // Helps obtaining authentication cookies for Office 365 web sites. It uses the provided
    // credentials to obtain the security token directly from the Office 365 STS.
    static class Office365CookieHelper
    {
        public static CookieContainer GetCookies(string url, PSCredential credential) {
            if (credential == null || string.IsNullOrEmpty(credential.UserName) ||
                credential.Password == null || credential.Password.Length == 0)
                throw new ArgumentException(
                    "Explicit credentials are needed to connect to Office 365.");
            var credentials = credential.GetCredentials();
            var helper = new MsOnlineClaimsHelper(credential.UserName,
                credential.Password.ToInsecureString(), url);
            return helper.CookieContainer;
        }
    }
}
