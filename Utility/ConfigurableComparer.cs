// Copyright (C) 2011-2012 Ferdinand Prantl <prantlf@gmail.com>
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
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace SharePosh
{
    // Implements IComparer and IEqualityComparer handling strings in a configurable way - i.e.
    // the letter-case sensitivity - for any type that is castable to string; other types are
    // handled by the default comparer.
    [Serializable]
    public class ConfigurableComparer<T> : IComparer, IEqualityComparer, IComparer<T>,
                                           IEqualityComparer<T>
    {
        public CultureInfo Culture { get { return culture; } }

        public CompareOptions Options { get { return options; } }

        public ConfigurableComparer(CultureInfo culture, CompareOptions options) {
            this.culture = culture;
            this.options = options;
        }

        int IComparer.Compare(object left, object right) {
            if (left == right)
                return 0;
            if (left == null)
                return -1;
            if (right == null)
                return 1;
            var leftString = left as string;
            if (leftString != null) {
                var rightString = right as string;
                if (rightString != null)
                    return string.Compare(leftString, rightString, Culture, Options);
            }
            var comparable = left as IComparable;
            if (comparable == null) {
                var typedComparable = left as IComparable<T>;
                if (typedComparable == null)
                    throw new ArgumentException("The left argument does not implement " +
                        "interfaces IComparable or IComparable<T>.");
                return right is T ? typedComparable.CompareTo((T) right) : 1;
            }
            return comparable.CompareTo(right);
        }

        bool IEqualityComparer.Equals(object left, object right) {
            if (left == right)
                return true;
            if (left == null || right == null)
                return false;
            var leftString = left as string;
            if (leftString != null) {
                var rightString = right as string;
                if (rightString != null)
                    return string.Compare(leftString, rightString, Culture, Options) == 0;
            }
            return left.Equals(right);
        }

        int IEqualityComparer.GetHashCode(object obj) {
            var value = obj as string;
            return value != null && (Options &
                (CompareOptions.IgnoreCase | CompareOptions.OrdinalIgnoreCase)) != 0 ?
                    value.ToUpper(Culture).GetHashCode() : obj.GetHashCode();
        }

        int IComparer<T>.Compare(T left, T right) {
            return ((IComparer) this).Compare(left, right);
        }

        bool IEqualityComparer<T>.Equals(T left, T right) {
            return ((IEqualityComparer) this).Equals(left, right);
        }

        int IEqualityComparer<T>.GetHashCode(T obj) {
            return ((IEqualityComparer) this).GetHashCode(obj);
        }

        public static ConfigurableComparer<T> CaseInsensitive {
            get {
                return caseInsensitive ?? (caseInsensitive = new ConfigurableComparer<T>(
                    CultureInfo.InvariantCulture, CompareOptions.IgnoreCase));
            }
        }

        CultureInfo culture;
        CompareOptions options;
        static ConfigurableComparer<T> caseInsensitive;
    }
}
