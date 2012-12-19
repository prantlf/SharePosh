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
    public class DriveInfo : PSDriveInfo, IDisposable
    {
        public string ConnectorType { get; private set; }

        public string WebUrl { get; private set; }

        public int Timeout { get; private set; }

        public TimeSpan CacheKeepPeriod { get; private set; }

        internal Connector Connector { get; private set; }

        public DriveInfo(PSDriveInfo driveInfo, NewDriveParameters parameters) : base(driveInfo) {
            if (parameters == null)
                throw new ArgumentNullException("parameters");
            if (string.IsNullOrEmpty(parameters.WebUrl))
                throw new ArgumentException(
                    "The WebUrl parameter cannot be empty. It must point to a SharePoint web site.");
            ConnectorType = !string.IsNullOrEmpty(parameters.Connector) ?
                                    parameters.Connector : "SOAP";
            WebUrl = parameters.WebUrl;
            Timeout = parameters.Timeout;
            CacheKeepPeriod = parameters.CacheKeepPeriod.Ticks == 0 ?
                                    new TimeSpan(0, 0, 2) : parameters.CacheKeepPeriod;
            if (string.IsNullOrEmpty(Description))
                Description = string.Format("Makes the SharePoint content at {0} " +
                    "accessible as you work with the local file system.",
                    PathUtility.JoinPath(WebUrl, Root));
            Connector = CreateConnector();
        }

        Connector CreateConnector() {
            var typeName = ConnectorType;
            if (!typeName.Contains(".") && !typeName.Contains(",")) {
                typeName = "SharePosh." + typeName;
                if (!typeName.EndsWith("Connector"))
                    typeName += "Connector";
            }
            var type = Type.GetType(typeName, true, true);
            return (Connector) Activator.CreateInstance(type, this);
        }

		public void Dispose() {
            if (Connector != null) {
                var disposable = Connector as IDisposable;
                if (disposable != null)
                    disposable.Dispose();
                Connector = null;
            }
            GC.SuppressFinalize(this);
		}
    }
}