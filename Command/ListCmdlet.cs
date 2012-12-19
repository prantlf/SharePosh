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
using System.Linq;
using System.Xml;
using SharePosh.SOAP.Lists;

namespace SharePosh
{
    // Provides access to the first list on a SharePoint web site for the descendant classes.
    // SharePoint web services return information about the parent web and server with the list
    // information which can be used in other cmdlets; not only in those that process the list.
    public abstract class ListCmdlet : ConnectedCmdlet
    {
        // Returns information about the first list on the specified web site.
        protected XmlElement GetList() {
            using (var service = GetService<Lists>()) {
                WriteVerbose("Calling Lists.GetListCollection.");
                var output = service.GetListCollection();
                var list = output.ChildNodes.OfType<XmlElement>().FirstOrDefault();
                if (list == null)
                    throw new ApplicationException("The web has no lists.");
                var id = list.GetAttribute("ID");
                WriteVerbose("Calling Lists.GetList({0}).", id);
                return (XmlElement) service.GetList(id);
            }
        }
    }
}
