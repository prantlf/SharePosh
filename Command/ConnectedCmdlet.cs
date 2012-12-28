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
using System.Web.Services.Protocols;

namespace SharePosh
{
    // Makes easier entering a SharePosh drive as an input parameter for a cmdlet. Either a drive
    // name or a DriveInfo object retrieved by Get-PSDrive are accepted.
    public class DrivePipeInput
    {
        public DriveInfo DriveInfo { get; private set; }
        public string Name { get; private set; }

        public DrivePipeInput() {}

        public DrivePipeInput(DriveInfo drive) {
            if (drive == null)
                throw new ArgumentNullException("drive");
            DriveInfo = drive;
        }

        public DrivePipeInput(string name) {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("PowerShell drive name must not be empty.");
            Name = name;
        }

        public override string ToString() {
            if (Name != null)
                return Name;
            if (DriveInfo != null)
                return DriveInfo.ToString();
            return "no drive";
        }
    }

    // Base class for cmdlets which need a SharePoint connection; either passed directly by the
    // web URL and user credentials or by a SharePosh drive (name or DriveInfo object).
    public abstract class ConnectedCmdlet : LoggingCmdlet
    {
        [Parameter(Mandatory = true, ParameterSetName = "ImplicitWebSpecification",
            HelpMessage = "SharePosh drive. " +
                "Provide an instance returned by Get-PSProvider or just its name.")]
        public DrivePipeInput Drive { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = "ExplicitWebSpecification",
           HelpMessage = "URL of a SharePoint web site.")]
        public string WebUrl { get; set; }

        [Parameter(Mandatory = false, ParameterSetName = "ExplicitWebSpecification",
            HelpMessage = "User credentials to access the web site with. " +
                "If not provided the current user will be used."), Credential]
        public PSCredential Credential { get; set; }

        [Parameter(Mandatory = false, ParameterSetName = "ExplicitWebSpecification",
            HelpMessage = "Turns on the Office 365 (SharePoint Online) authentication mode.")]
        public SwitchParameter Office365 { get; set; }

        // This property should be used to get the web site used by the cmdlet. If the web site
        // URL was not specified explicitly the root web URL of the drive will be returned.
        protected string ActualWebUrl {
            get { return Drive != null ? DriveInfo.WebUrl : WebUrl; }
        }

        // The caller is responsible for disposing the service instance when not needed anymore.
        protected T GetService<T>() where T : SoapHttpClientProtocol, new() {
            CheckNoTestingConnector();
            string webUrl;
            PSCredential credential;
            int timeout;
            bool office365;
            if (Drive != null) {
                webUrl = DriveInfo.WebUrl;
                credential = DriveInfo.Credential;
                timeout = DriveInfo.Timeout;
                office365 = DriveInfo.ConnectorType.StartsWithII("Office365");
            } else {
                webUrl = WebUrl;
                credential = Credential;
                timeout = 0;
                office365 = Office365;
            }
            WriteVerbose("Connecting to {0}.", webUrl);
            if (office365) {
                var cookies = Office365CookieHelper.GetCookies(webUrl, credential);
                return Office365SOAPConnector.GetService<T>(webUrl, cookies, timeout);
            }
            return SOAPConnector.GetService<T>(webUrl, credential, timeout);
        }

        protected void CheckNoTestingConnector() {
            if (Drive != null && DriveInfo.Connector is TestingConnector)
                throw new ArgumentException("The drive was a testing one without an actual " +
                                            "connection to SharePoint.");
        }

        // Returns a DriveInfo instance if the drive provider was specified either by name or as
        // the object instance itself.
        protected DriveInfo DriveInfo {
            get {
                if (driveInfo == null && Drive != null)
                    if (Drive.DriveInfo != null) {
                        driveInfo = Drive.DriveInfo;
                    } else {
                        driveInfo = SessionState.Drive.Get(Drive.Name) as DriveInfo;
                        if (driveInfo == null)
                            throw new ApplicationException("A SharePosh drive was expected.");
                    }
                return driveInfo;
            }
        }
        DriveInfo driveInfo;
    }
}
