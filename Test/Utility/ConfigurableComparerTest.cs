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

using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;

namespace SharePosh
{
    [TestFixture]
    public class ConfigurableComparerTest
    {
        [Test]
        public void TestCaseInsensitiveParameters() {
            var comparer = ConfigurableComparer<string>.CaseInsensitive;
            Assert.AreEqual(CultureInfo.InvariantCulture, comparer.Culture);
            Assert.AreEqual(CompareOptions.IgnoreCase, comparer.Options);
        }

        [Test]
        public void TestThatStringsAreEquelCaseInsensitively() {
            IEqualityComparer<string> comparer1 = new ConfigurableComparer<string>(
                CultureInfo.InvariantCulture, CompareOptions.IgnoreCase);
            Assert.IsTrue(comparer1.Equals("A1", "A1"));
            Assert.IsTrue(comparer1.Equals("A1", "a1"));
            IEqualityComparer<object> comparer2 = new ConfigurableComparer<object>(
                CultureInfo.InvariantCulture, CompareOptions.IgnoreCase);
            Assert.IsTrue(comparer2.Equals("A1", "A1"));
            Assert.IsTrue(comparer2.Equals("A1", "a1"));
            Assert.IsTrue(comparer2.Equals(12, 12));
        }

        [Test]
        public void TestThatStringsAreEquelCaseSensitively() {
            IEqualityComparer<string> comparer1 = new ConfigurableComparer<string>(
                CultureInfo.InvariantCulture, CompareOptions.None);
            Assert.IsTrue(comparer1.Equals("A1", "A1"));
            Assert.IsFalse(comparer1.Equals("A1", "a1"));
            IEqualityComparer<object> comparer2 = new ConfigurableComparer<object>(
                CultureInfo.InvariantCulture, CompareOptions.None);
            Assert.IsTrue(comparer2.Equals("A1", "A1"));
            Assert.IsFalse(comparer2.Equals("A1", "a1"));
            Assert.IsTrue(comparer2.Equals(12, 12));
        }
    }
}
