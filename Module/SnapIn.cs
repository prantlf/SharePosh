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

using System.Management.Automation;
using System.Reflection;

namespace SharePosh
{
    public class SnapIn : PSSnapIn 
    {
        public override string Name {
            get { return "SharePosh"; }
        }

        public override string Vendor {
            get {
                return Assembly.GetAssemblyAttribute<
                    AssemblyCompanyAttribute>().Company;
            }
        }

        public override string Description {
            get {
                return Assembly.GetAssemblyAttribute<
                    AssemblyDescriptionAttribute>().Description;
            }
        }

        Assembly Assembly {
            get { return Assembly.GetExecutingAssembly(); }
        }
    }
}
