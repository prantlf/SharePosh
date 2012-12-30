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
using System.Text;
using NUnit.Framework;

namespace SharePosh
{
    [TestFixture]
    public class StringExtensionTest
    {
        [Test]
        public void TestThatIsEmptyFailsWithNull() {
            try {
                ((string) null).IsEmpty();
                Assert.Fail("NullReferenceException not thrown.");
            } catch (NullReferenceException) {}
        }

        [Test]
        public void TestThatIsEmptyReturnsCorrectResult() {
            Assert.IsTrue("".IsEmpty());
            Assert.IsFalse("a".IsEmpty());
        }

        [Test]
        public void TestThatIsStartsWithReturnsCorrectResult() {
            Assert.IsFalse("".StartsWith('a'));
            Assert.IsFalse("b".StartsWith('a'));
            Assert.IsFalse("ba".StartsWith('a'));
            Assert.IsTrue("a".StartsWith('a'));
            Assert.IsTrue("ab".StartsWith('a'));

            Assert.IsFalse(new StringBuilder().StartsWith('a'));
            Assert.IsFalse(new StringBuilder("b").StartsWith('a'));
            Assert.IsFalse(new StringBuilder("ba").StartsWith('a'));
            Assert.IsTrue(new StringBuilder("a").StartsWith('a'));
            Assert.IsTrue(new StringBuilder("ab").StartsWith('a'));
        }

        [Test]
        public void TestThatIsEndsWithReturnsCorrectResult() {
            Assert.IsFalse("".EndsWith('a'));
            Assert.IsFalse("b".EndsWith('a'));
            Assert.IsFalse("ab".EndsWith('a'));
            Assert.IsTrue("a".EndsWith('a'));
            Assert.IsTrue("ba".EndsWith('a'));

            Assert.IsFalse(new StringBuilder().EndsWith('a'));
            Assert.IsFalse(new StringBuilder("b").EndsWith('a'));
            Assert.IsFalse(new StringBuilder("ab").EndsWith('a'));
            Assert.IsTrue(new StringBuilder("a").EndsWith('a'));
            Assert.IsTrue(new StringBuilder("ba").EndsWith('a'));
        }
    }
}
