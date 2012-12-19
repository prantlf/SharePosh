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
using System.Security;
using System.Text;

namespace SharePosh
{
    // Helps formatting values and string literals before placing them to an XML content.
    class XmlUtility
    {
        // Escapes control XML characters which cannot be present raw in an XML element body.
        // & becomes &amp; < becomes &lt; > becomes &gt; ' becomes &apos; " becomes &quot;
        public static string EscapeXmlValue(string value) {
            if (value == null)
                throw new ArgumentNullException("value");
            return SecurityElement.Escape(value);
        }

        // Produces an XPath literal equal to the value if possible; if not, produces an XPath
        // expression that will match the value. The XPath literal is returned if the value
        // contains no quotes or only either single or double quotes.If it contains both an XPath
        // expression using concat() is returned that evaluates to the value.
        // 
        // Note that this function will produce very long XPath expressions if a value
        // contains a long run of double quotes.
        //
        // http://stackoverflow.com/questions/1341847/special-character-in-xpath-query
        public static string FormatXPathLiteral(string value) {
            if (value == null)
                throw new ArgumentNullException("value");
            // if the value contains only single or double quotes, construct an XPath literal.
            if (!value.Contains('"'))
                return "\"" + value + "\"";
            if (!value.Contains('\''))
                return "'" + value + "'";
            // If the value contains both single and double quotes, construct an expression
            // that concatenates all non-double-quote substrings with the quotes, e.g.:
            //   concat("foo", '"', "bar")
            var expression = new StringBuilder("concat(");
            var parts = value.Split('"');
            for (int i = 0; i < parts.Length; i++) {
                var part = parts[i];
                var comma = i > 0;
                if (part.Any()) {
                    if (i > 0)
                        expression.Append(", ");
                    expression.Append("\"").Append(part).Append("\"");
                    comma = true;
                }
                if (i < parts.Length - 1) {
                    if (comma)
                        expression.Append(", ");
                    expression.Append("'\"'");
                }
            }
            return expression.Append(")").ToString();
        }
    }
}
