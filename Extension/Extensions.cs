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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Xml;

namespace SharePosh
{
    // Additional methods and method overloads which would be useful in particular classes. They
    // are organized by the class which they attach to.

    static class AssemblyExtension
    {
        public static T GetAssemblyAttribute<T>(this Assembly assembly) {
            var attributes = assembly.GetCustomAttributes(false);
            return (T) attributes.First(item => item is T);
        }
    }

    static class StringExtension
    {
        public static bool IsEmpty(this string s) {
            return s.Length > 0;
        }

        public static bool StartsWith(this string s, char c) {
            return s.Length > 0 && s[0] == c;
        }

        public static bool EndsWith(this string s, char c) {
            return s.Length > 0 && s[s.Length - 1] == c;
        }

        public static bool StartsWith(this StringBuilder s, char c) {
            return s.Length > 0 && s[0] == c;
        }

        public static bool EndsWith(this StringBuilder s, char c) {
            return s.Length > 0 && s[s.Length - 1] == c;
        }
        
        // The abbreviation CI means using the flag CurrentCultureIgnoreCase for the string
        // comparison, the abbreviation II the InvariantCultureIgnoreCase flag.

        public static bool EqualsCI(this string left, string right) {
            return string.Equals(left, right, StringComparison.CurrentCultureIgnoreCase);
        }

        public static bool EqualsII(this string left, string right) {
            return string.Equals(left, right, StringComparison.InvariantCultureIgnoreCase);
        }

        public static bool StartsWithCI(this string hay, string needle) {
            if (hay == null)
                throw new ArgumentNullException("hay");
            return hay.StartsWith(needle, StringComparison.CurrentCultureIgnoreCase);
        }

        public static bool StartsWithII(this string hay, string needle) {
            if (hay == null)
                throw new ArgumentNullException("hay");
            return hay.StartsWith(needle, StringComparison.InvariantCultureIgnoreCase);
        }

        public static bool EndsWithCI(this string hay, string needle) {
            if (hay == null)
                throw new ArgumentNullException("hay");
            return hay.EndsWith(needle, StringComparison.CurrentCultureIgnoreCase);
        }

        public static bool EndsWithII(this string hay, string needle) {
            if (hay == null)
                throw new ArgumentNullException("hay");
            return hay.EndsWith(needle, StringComparison.InvariantCultureIgnoreCase);
        }

        public static int IndexOfCI(this string hay, string needle) {
            if (hay == null)
                throw new ArgumentNullException("hay");
            return hay.IndexOf(needle, StringComparison.CurrentCultureIgnoreCase);
        }

        public static int IndexOfII(this string hay, string needle) {
            if (hay == null)
                throw new ArgumentNullException("hay");
            return hay.IndexOf(needle, StringComparison.InvariantCultureIgnoreCase);
        }

        public static string ToInsecureString(this SecureString input) {
            IntPtr pointer = Marshal.SecureStringToBSTR(input);
            try {
                return Marshal.PtrToStringBSTR(pointer);
            } finally {
                Marshal.FreeBSTR(pointer);
            }
        }

        public static SecureString FromInsecureString(this SecureString target, string source) {
            target.Clear();
            if (source != null)
                foreach (var c in source)
                    target.AppendChar(c);
            return target;
        }
    }

    static class GuidExtension
    {
        public static bool IsEmpty(this Guid input) {
            return input == Guid.Empty;
        }
    }

    static class PrimitivesExtension
    {
        // The abbreviation I means using the InvariantCulture culture info.

        public static string ToStringI(this bool input) {
            return input.ToString(CultureInfo.InvariantCulture);
        }

        public static string ToStringI(this int input) {
            return input.ToString(CultureInfo.InvariantCulture);
        }

        public static string ToStringI(this uint input) {
            return input.ToString(CultureInfo.InvariantCulture);
        }
    }

    static class IOExtension
    {
        public static void CopyTo(this Stream input, Stream output) {
            byte[] buffer = new byte[65536];
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                output.Write(buffer, 0, read);
        }

        public static byte[] ReadBytes(this Stream input) {
            var memory = input as MemoryStream;
            if (memory != null)
                return memory.ToArray();
            byte[] buffer = new byte[input.Length];
            var read = input.Read(buffer, 0, buffer.Length);
            if (read != buffer.Length)
                throw new ApplicationException("Premature end of the stream.");
            return buffer;
        }

        public static IEnumerable<string> ReadLines(this TextReader input) {
            for (string line; (line = input.ReadLine()) != null;)
                yield return line;
        }
    }

    static class XmlExtension
    {
        public static void Remove(this XmlNode node) {
            node.ParentNode.RemoveChild(node);
        }

        public static XmlElement SelectElement(this XmlNode node, string xpath) {
            return (XmlElement) node.SelectSingleNode(xpath);
        }
    }

    static class CredentialExtension
    {
        public static ICredentials GetCredentials(this PSCredential credential) {
            var parts = credential.UserName.Split(new[] { '\\' },
                StringSplitOptions.RemoveEmptyEntries);
            string domain, user;
            if (parts.Length > 1) {
                domain = parts[0];
                user = parts[1];
            } else {
                domain = "";
                user = parts[0];
            }
            return new NetworkCredential(user, credential.Password.ToInsecureString(), domain);
        }
    }

    static class PSObjectExtension
    {
        public static object GetBaseObject(this object source) {
            var wrapper = source as PSObject;
            return wrapper != null ? wrapper.BaseObject : source;
        }
    }
}
