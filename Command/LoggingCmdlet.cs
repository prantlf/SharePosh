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
    // Base class for cmdlets providing convenience methods for logging.
    public abstract class LoggingCmdlet : PSCmdlet
    {
        protected new void WriteVerbose(string message) {
            base.WriteVerbose(message);
        }

        protected void WriteVerbose(string format, params object[] args) {
            WriteVerbose(string.Format(format, args));
        }

        protected new void WriteWarning(string message) {
            base.WriteWarning(message);
        }

        protected void WriteWarning(string format, params object[] args) {
            WriteWarning(string.Format(format, args));
        }
    }
}
