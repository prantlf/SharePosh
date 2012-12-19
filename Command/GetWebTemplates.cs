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
using SharePosh.SOAP.Sites;

namespace SharePosh
{
    // Gets web templates available on a specific SharePoint web site.
    [Cmdlet(VerbsCommon.Get, "SPWebTemplates")]
    public class GetWebTemplates : ConnectedCmdlet
    {
        [Parameter(Mandatory = true,
            HelpMessage = "Locale identifier of the web templates to return.")]
        public uint Locale { get; set; }

        protected override void ProcessRecord() {
            try {
                using (var service = GetService<Sites>()) {
                    WriteVerbose("Calling Sites.GetSiteTemplates.");
                    Template[] templates;
                    service.GetSiteTemplates(Locale, out templates);
                    WriteObject(templates, true);
                }
            } catch (Exception exception) {
                WriteError(new ErrorRecord(exception, "WebAccessFailed",
                    ErrorCategory.ResourceUnavailable, ActualWebUrl));
            }
        }
    }
}
