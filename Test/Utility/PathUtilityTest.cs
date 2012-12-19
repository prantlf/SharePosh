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
using NUnit.Framework;

namespace SharePosh
{
    [TestFixture]
    public class PathUtilityTest
    {
        [Test]
        public void TestConvertToPSPath() {
            Assert.AreEqual(@"test\test\test\", PathUtility.ConvertToPSPath(@"test\test/test/"));
        }

        [Test]
        public void TestNormalizePath() {
            Assert.AreEqual(@"t est/test/ test", PathUtility.NormalizePath(@"/ t est/test\ test\ "));
        }

        [Test]
        public void TestGetUrlPath() {
            Assert.AreEqual("", PathUtility.GetUrlPath("http://host", false));
            Assert.AreEqual("", PathUtility.GetUrlPath("http://host/", false));
            Assert.AreEqual("", PathUtility.GetUrlPath("http://host:80", false));
            Assert.AreEqual("", PathUtility.GetUrlPath("http://host:80/", false));
            Assert.AreEqual("path", PathUtility.GetUrlPath("http://host/path", false));
            Assert.AreEqual("path", PathUtility.GetUrlPath("http://host:80/path", false));
            Assert.AreEqual("path/", PathUtility.GetUrlPath("http://host/path/", false));
            Assert.AreEqual("path/", PathUtility.GetUrlPath("http://host:80/path/", false));
            Assert.AreEqual("/", PathUtility.GetUrlPath("http://host", true));
            Assert.AreEqual("/", PathUtility.GetUrlPath("http://host/", true));
            Assert.AreEqual("/", PathUtility.GetUrlPath("http://host:80", true));
            Assert.AreEqual("/", PathUtility.GetUrlPath("http://host:80/", true));
            Assert.AreEqual("/path", PathUtility.GetUrlPath("http://host/path", true));
            Assert.AreEqual("/path", PathUtility.GetUrlPath("http://host:80/path", true));
            Assert.AreEqual("/path/", PathUtility.GetUrlPath("http://host/path/", true));
            Assert.AreEqual("/path/", PathUtility.GetUrlPath("http://host:80/path/", true));
        }
    }
}
