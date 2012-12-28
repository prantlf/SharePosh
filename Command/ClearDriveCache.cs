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

namespace SharePosh
{
    // Discards all objects from the cache used by the particular SharePosh drive provider
    // to improve performance if the default cache throwaway mode is not enough.
    [Cmdlet(VerbsCommon.Clear, "SPDriveCache")]
    public class ClearDrivecache : LoggingCmdlet
    {
        [Parameter(Mandatory = true, Position = 1,
            HelpMessage = "SharePosh drive. " +
                "Provide an instance returned by Get-PSProvider or just its name.")]
        public DrivePipeInput Drive { get; set; }

        [Parameter(
            HelpMessage = "Discards also cached root web, list or folder from memory.")]
        public SwitchParameter IncludeRoot { get; set; }

        protected override void ProcessRecord() {
            try {
                var connector = DriveInfo.Connector as CachingConnector;
                if (connector != null) {
                    connector.ClearCache(IncludeRoot);
                    WriteVerbose("The cache of the drive {0} has been cleared.", DriveInfo.Name);
                } else {
                    WriteVerbose("The specified drive provider does not use caching.");
                }
            } catch (Exception exception) {
                WriteError(new ErrorRecord(exception, "DriveAccessFailed",
                    ErrorCategory.ResourceUnavailable, Drive));
            }
        }

        // Returns a DriveInfo instance if the drive provider was specified either by name or as
        // the object instance itself.
        DriveInfo DriveInfo {
            get {
                if (driveInfo == null)
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
