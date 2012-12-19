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
using System.Xml;
using NUnit.Framework;

namespace SharePosh
{
    [TestFixture]
    public class XmlUtilityTest
    {
        [Test]
        public void TestThatFormatXmlValueFailsWithNull() {
            try {
                XmlUtility.EscapeXmlValue(null);
                Assert.Fail("ArgumentNullException not thrown.");
            } catch (ArgumentNullException) {}
        }

        [Test]
        public void TestThatFormatXmlValueWorksWithManyValues() {
            var document = new XmlDocument();
            var element = document.CreateElement("test");
            element.InnerXml = XmlUtility.EscapeXmlValue("&<>'\"");
        }

        [Test]
        public void TestThatFormatXPathLiteralFailsWithNull() {
            try {
                XmlUtility.FormatXPathLiteral(null);
                Assert.Fail("ArgumentNullException not thrown.");
            } catch (ArgumentNullException) {}
        }

        // http://stackoverflow.com/questions/1341847/special-character-in-xpath-query
        [Test]
        public void TestThatFormatXPathLiteralWorksWithManyValues() {
            foreach (string value in new[] {
                        "",                 // empty
                        "foo",              // no quotes
                        "\"foo",            // double quotes only
                        "'foo",             // single quotes only
                        "'foo\"bar",        // both; double quotes in mid-string
                        "'foo\"bar\"baz",   // multiple double quotes in mid-string
                        "'foo\"",           // string ends with double quotes
                        "'foo\"\"",         // string ends with run of double quotes
                        "\"'foo",           // string begins with double quotes
                        "\"\"'foo",         // string begins with run of double quotes
                        "'foo\"\"bar"       // run of double quotes in mid-string
                    }) {
                var document = new XmlDocument();
                var element = document.CreateElement("test");
                element.SetAttribute("value", value);
                document.AppendChild(element);
                string expression = string.Format("/test[@value={0}]",
                    XmlUtility.FormatXPathLiteral(value));
                Assert.IsNotNull(document.SelectSingleNode(expression),
                    string.Format("Value {0} not matched.", value));
            }
        }
    }
}
