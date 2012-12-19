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
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SharePosh
{
    [TestClass]
    public class ValueUtilityTest
    {
        [TestMethod]
        public void TestGetLookupValue() {
            Assert.AreEqual("value", ValueUtility.GetLookupValue("1;#value"));
            Assert.AreEqual("", ValueUtility.GetLookupValue("1;#"));
            Assert.AreEqual("value", ValueUtility.GetLookupValue("value"));
        }

        [TestMethod]
        public void TestGetBool() {
            Assert.AreEqual(true, ValueUtility.GetBool("true"));
            Assert.AreEqual(true, ValueUtility.GetBool("TRUE"));
            Assert.AreEqual(false, ValueUtility.GetBool("false"));
            Assert.AreEqual(false, ValueUtility.GetBool("FALSE"));
        }

        [TestMethod]
        public void TestGetInt() {
            Assert.AreEqual(12, ValueUtility.GetInt("12"));
            try {
                ValueUtility.GetInt("");
                Assert.Fail();
            } catch {}
            try {
                ValueUtility.GetInt("1.2");
                Assert.Fail();
            } catch {}
            try {
                ValueUtility.GetInt("A");
                Assert.Fail();
            } catch {}
        }

        [TestMethod]
        public void TestTryGetInt() {
            int result;
            Assert.IsTrue(ValueUtility.TryGetInt("12", out result));
            Assert.AreEqual(12, result);
            Assert.IsFalse(ValueUtility.TryGetInt("", out result));
            Assert.IsFalse(ValueUtility.TryGetInt("1.2", out result));
            Assert.IsFalse(ValueUtility.TryGetInt("A", out result));
        }

        [TestMethod]
        public void TestGetDate() {
            Assert.AreEqual(new DateTime(2012, 11, 30), ValueUtility.GetDate("2012-11-30"));
            Assert.AreEqual(new DateTime(2012, 11, 30, 01, 02, 03),
                ValueUtility.GetDate("2012-11-30 01:02:03"));
            try {
                ValueUtility.GetDate("");
                Assert.Fail();
            } catch {}
            try {
                ValueUtility.GetDate("1");
                Assert.Fail();
            } catch {}
        }

        [TestMethod]
        public void TestTryGetDate() {
            DateTime result;
            Assert.IsTrue(ValueUtility.TryGetDate("2012-11-30", out result));
            Assert.AreEqual(new DateTime(2012, 11, 30), result);
            Assert.IsTrue(ValueUtility.TryGetDate("2012-11-30 01:02:03", out result));
            Assert.AreEqual(new DateTime(2012, 11, 30, 01, 02, 03), result);
            Assert.IsFalse(ValueUtility.TryGetDate("", out result));
            Assert.IsFalse(ValueUtility.TryGetDate("1", out result));
        }
    }
}
