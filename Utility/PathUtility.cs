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
using System.Text;

namespace SharePosh
{
    // Helps dealing with file paths, names and extensions and URLs.
    class PathUtility
    {
        // Forces backslashes in paths returned to PowerShell because all paths are normalized
        // with them by default. I may change it for the SharePosh provider in the future.
        public static string ConvertToPSPath(string path) {
            if (path == null)
                throw new ArgumentNullException("path");
            return path.Replace('/', '\\');
        }

        // Forces slashes in paths coming from the PowerShell input because all paths come in
        // normalized with backslashes by default. I may change it for the SharePosh provider in
        // the future. Additionally, the starting slash is trimmed; paths used internally by the
        // connectors are relative to the root web site specified when creating a new drive.
        public static string NormalizePath(string path) {
            if (path == null)
                throw new ArgumentNullException("path");
            return path.Trim().Replace('\\', '/').Trim('/', ' ');
        }

        // Joins the first part (root) and all additional parts with slashes. Any part can start
        // or end with slash; separating slashes are added as necessary not to make them doubled.

        public static string JoinPath(string root, params string[] parts) {
            return JoinPath(root, parts, 0);
        }

        public static string JoinPath(string[] parts, int start) {
            return JoinPath("", parts, start);
        }

        public static string JoinPath(string[] parts, int start, int count) {
            return JoinPath("", parts, start, count);
        }

        public static string JoinPath(string root, string[] parts, int start) {
            if (parts == null)
                throw new ArgumentNullException("right");
            return JoinPath(root, parts, start, parts.Length - start);
        }

        public static string JoinPath(string root, string[] parts, int start, int count) {
            if (root == null)
                throw new ArgumentNullException("root");
            if (parts == null)
                throw new ArgumentNullException("right");
            if (start < 0 || start > parts.Length)
                throw new ArgumentOutOfRangeException("start");
            if (count < 0 || count > parts.Length - start)
                throw new ArgumentOutOfRangeException("count");
            var result = new StringBuilder(root.Trim());
            var trimmed = parts.Skip(start).Take(count).Select(item => item.Trim());
            foreach (var part in trimmed.Where(item => item.Any()))
                // This seems complicated but the idea is to ensure the single separating slashes
                // saving the append slash operation if not necessary.
                if (part.StartsWith('/')) {
                    if (result.Length > 0 && !result.EndsWith('/'))
                        result.Append(part);
                    else
                        result.Append(part, 1, part.Length - 1);
                } else {
                    if (result.Length > 0 && !result.EndsWith('/'))
                        result.Append('/');
                    result.Append(part);
                }
            return result.ToString();
        }

        // Splits the path separated by slashes to its parts. It returns only parts that are not
        // empty and that don't consist only of whitespace.
        public static string[] SplitPath(string path) {
            if (path == null)
                throw new ArgumentNullException("path");
            var parts = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Select(part => part.Trim()).Where(part => part.Any()).ToArray();
        }

        // Gets parent and child paths of a path. The child is the part after the very last slash.
        // If there is no slash in the path it is already the child name. The parent is the part
        // from the beginning up to the very last slash. If there is no slash in the path the
        // parent is empty because the path contains only the child then.

        public static string GetParentPath(string path) {
            if (path == null)
                throw new ArgumentNullException("path");
            var separator = path.LastIndexOf('/');
            return separator > 0 ? path.Substring(0, separator) : "";
        }

        public static string GetParentPath(string path, out string name) {
            if (path == null)
                throw new ArgumentNullException("path");
            var separator = path.LastIndexOf('/');
            if (separator > 0) {
                name = path.Substring(separator + 1);
                return path.Substring(0, separator);
            }
            name = path;
            return "";
        }

        public static string GetChildName(string path) {
            if (path == null)
                throw new ArgumentNullException("path");
            var separator = path.LastIndexOf('/');
            return separator >= 0 ? path.Substring(separator + 1) : path;
        }

        // Gets name and extension parts of a file name. They are separated by the very last dot
        // in the full file name. The returned extension includes the (preceding) dot. If there is
        // no dot there the name is already without extension and the extension is empty.

        public static string GetNameWithoutExtension(string name) {
            if (name == null)
                throw new ArgumentNullException("name");
            var separator = name.LastIndexOf('.');
            return separator >= 0 ? name.Substring(0, separator) : name;
        }

        public static string GetNameWithoutExtension(string name, out string extension) {
            if (name == null)
                throw new ArgumentNullException("name");
            var separator = name.LastIndexOf('.');
            if (separator >= 0) {
                extension = name.Substring(separator);
                return name.Substring(0, separator);
            }
            extension = "";
            return name;
        }

        public static string GetExtension(string name) {
            if (name == null)
                throw new ArgumentNullException("name");
            var separator = name.LastIndexOf('.');
            return separator >= 0 ? name.Substring(separator) : "";
        }

        // Trims off the starting scheme, host and port up to the starting slash from the URL
        // leaving just the absolute path (and query if present). For example, it returns
        // "sites/team" from "http://server/sites/team".
        public static string GetUrlPath(string url, bool startWithSlash) {
            if (url == null)
                throw new ArgumentNullException("url");
            // Look for the first slash following the first slash pair. This assumes a valid URL.
            var slash = url.IndexOf("/", url.IndexOf("//") + 2);
            if (startWithSlash)
                return slash > 0 ? url.Substring(slash) : "/";
            return slash > 0 ? url.Substring(slash + 1) : "";
        }
    }
}
