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
using System.Globalization;

namespace SharePosh
{
    // Helps parsing strings to primitive types so that the invariant culture needs not to be
    // specified all the time.
    class ValueUtility
    {
        // Cuts the lookup ID off the compound value leaving just the displayable value itself.
        // For example, it returns "value" from "1;#value".
        public static string GetLookupValue(string value) {
            var separator = value.IndexOf('#');
            return separator < value.Length ? value.Substring(separator + 1) : "";
        }

        // Parses boolean, integer or date from a string for the deserialization purposes; it
        // avoids using locale-specific conversions without the need of specifying the invariant
        // culture all the time.

        public static bool GetBool(string input) {
            return "TRUE".EqualsII(input);
        }

        public static int GetInt(string input) {
            return int.Parse(input, CultureInfo.InvariantCulture);
        }

        public static bool TryGetInt(string value, out int result) {
            return int.TryParse(value, NumberStyles.Integer,
                                            CultureInfo.InvariantCulture, out result);
        }

        public static DateTime GetDate(string input) {
            return DateTime.Parse(input, CultureInfo.InvariantCulture);
        }

        public static bool TryGetDate(string value, out DateTime result) {
            return DateTime.TryParse(value, CultureInfo.InvariantCulture,
                                                DateTimeStyles.None, out result);
        }

        // Returns the more recent time value of the two values entered.
        public static DateTime Max(DateTime first, DateTime second) {
            return first > second ? first : second;
        }
    }
}
