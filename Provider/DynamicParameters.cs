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
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Reflection;
using System.Text;
using Microsoft.PowerShell.Commands;

namespace SharePosh
{
    public class NewDriveParameters
    {
        [Parameter]
        public string Connector { get; set; }

        [Parameter]
        public string WebUrl { get; set; }

        [Parameter]
        public int Timeout { get; set; }

        [Parameter]
        public TimeSpan CacheKeepPeriod { get; set; }
    }

    public class NewWebParameters
    {
        [Parameter]
        public string Title { get; set; }

        [Parameter]
        public string Description { get; set; }

        [Parameter]
        public string Template { get; set; }

        [Parameter]
        public uint Language { get; set; }

        [Parameter]
        public uint Locale { get; set; }

        [Parameter]
        public uint CollationLocale { get; set; }

        [Parameter]
        public SwitchParameter UniquePermissions { get; set; }

        [Parameter]
        public SwitchParameter Anonymous { get; set; }

        [Parameter]
        public SwitchParameter Presence { get; set; }
    }

    public class NewListParameters
    {
        [Parameter]
        public string Description { get; set; }

        [Parameter]
        public int Template { get; set; }
    }

    public class NewItemParameters
    {}

    public class NewFolderParameters : NewItemParameters
    {}

    public class NewFileParameters : NewItemParameters
    {
        [Parameter(HelpMessage = "Specifies the encoding of the textual content." +
            " If the input content is not passed as an array of strings this parameter is ignored.")]
        public EncodingPipeInput Encoding { get; set; }

        public Encoding GetEncoding() {
            return Encoding != null && Encoding.Encoding != null ? Encoding.Encoding :
                System.Text.Encoding.Default;
        }
    }

    public class CopyItemParameters
    {
        [Parameter]
        public string NewName { get; set; }
    }

    public class GetChildrenParameters
    {
        [Parameter]
        public int Depth { get; set; }

        [Parameter]
        public string[] Type { get; set; }

        public IEnumerable<Type> ParseChildTypes() {
            if (Type != null && !Type.Contains("All",
                                            ConfigurableComparer<string>.CaseInsensitive)) {
                var trimmed = Type.Select(item => item.Trim());
                foreach (var entry in trimmed.Where(item => !string.IsNullOrEmpty(item))) {
                    var typeName = "SharePosh." + entry + "Info";
                    var type = System.Type.GetType(typeName, false, true);
                    if (type == null)
                        throw new ApplicationException("Invalid child type filter.");
                    yield return type;
                }
            }
        }
    }

    // Extra parameters for Get-Content and Set-Content cmdlets. They can specify encoding of
    // the binary content or raw binary processing in the same way as it is done in the file
    // system drive provider.

    public abstract class ContentParameters : FileSystemContentDynamicParametersBase
    {
        public Encoding GetEncoding() {
            if (!WasStreamTypeSpecified)
                return System.Text.Encoding.Default;
            if (Encoding == FileSystemCmdletProviderEncoding.Byte)
                throw new InvalidOperationException("Byte content has no encoding.");
            var flags = BindingFlags.InvokeMethod | BindingFlags.Static | BindingFlags.NonPublic;
            return (Encoding) typeof(FileSystemContentDynamicParametersBase).InvokeMember(
                "GetEncodingFromEnum", flags, null, null, new object[] { Encoding });
        }
    }

    public class ContentReaderParameters : ContentParameters
    {
        [Parameter]
        public string Version { get; set; }
    }

    public class ContentWriterParameters : ContentParameters
    {}
}
