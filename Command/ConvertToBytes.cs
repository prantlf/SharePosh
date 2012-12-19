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
using System.Management.Automation;
using System.Text;

namespace SharePosh
{
    // Makes easier entering a text encoding as an input parameter for a cmdlet. Either a name
    // that is recognized by the .NET framework or a code page number or an Encoding object
	// of the System.Text.Encoding class are accepted.
    public class EncodingPipeInput
    {
        public Encoding Encoding { get; private set; }

        public EncodingPipeInput() {}

        public EncodingPipeInput(Encoding encoding) {
            if (encoding == null)
                throw new ArgumentNullException("encoding");
            Encoding = encoding;
        }

        public EncodingPipeInput(string encodingName) {
            if (encodingName == null)
                throw new ArgumentNullException("encodingName");
            Encoding = encodingName.EqualsII("Default") ? Encoding.Default :
                Encoding.GetEncoding(encodingName);
        }

        public EncodingPipeInput(int codePage) {
            if (codePage <= 0)
                throw new ArgumentOutOfRangeException("codePage");
            Encoding = Encoding.GetEncoding(codePage);
        }
    }

    // Converts other object representations of an array of bytes to the actual array - an object
    // of the type byte[]. For example, an array of the type object[] although containing bytes
    // only is not accepted by the standard cmdlet Get-Content. This cmdlet accepts also primitive
    // types directly convertible to bytes. Strings and objects of other types are converted to
    // strings by ToString and they to bytes controlled by specifying encoding to use for the byte conversion.
    [Cmdlet(VerbsData.ConvertTo, "Bytes")]
    public class ConvertToBytes : LoggingCmdlet
    {
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true,
            HelpMessage = "Array of bytes or objects convertible to bytes. " +
                          "Other objects are converted to string and then to bytes.")]
        public Array Data { get; set; }

        [Parameter(HelpMessage = "Encoding for the conversion of a textual content to bytes. " +
            " If the input content is an array of primitive objects this parameter is ignored.")]
        public EncodingPipeInput Encoding { get; set; }

        protected override void ProcessRecord() {
            try {
                WriteObject(GetBytes(Data), false);
            } catch (Exception exception) {
                WriteError(new ErrorRecord(exception, "ByteArrayConversionFailed",
                    ErrorCategory.ResourceUnavailable, Data));
            }
        }

        public static byte[] GetBytes(Array array, Encoding encoding) {
            if (array == null)
                return null;
            var bytes = array as byte[];
            if (bytes != null)
                return bytes;
            var objects = array.Cast<object>();
            var first = objects.FirstOrDefault();
            if (first.GetBaseObject().GetType().IsPrimitive) {
                return objects.Select(item => Convert.ToByte(item.GetBaseObject())).ToArray();
            } else {
                var text = new StringBuilder();
                foreach (var entry in array) {
                    if (text.Length > 0)
                        text.AppendLine();
                    var item = entry.GetBaseObject();
                    if (item != null)
                        text.Append(item);
                }
                return encoding.GetBytes(text.ToString());
            }
        }

        byte[] GetBytes(Array array) {
            return GetBytes(array, GetEncoding());
        }

        System.Text.Encoding GetEncoding() {
            return Encoding != null && Encoding.Encoding != null ? Encoding.Encoding :
                System.Text.Encoding.Default;
        }
    }
}
