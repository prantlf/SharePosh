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
using System.Linq;
using System.Management.Automation;
using System.Reflection;
using System.Xml;
using System.Xml.XPath;

namespace SharePosh
{
    // Gets build version of the SharePoint server and tries to guess the name of the released
    // SharePoint product. It needs an accessible list on the web site because of the SharePoint
    // web service which returns this information.
    [Cmdlet(VerbsCommon.Get, "SPServerVersion")]
    public class GetServerVersion : ListCmdlet
    {
        // Writes the server build number and then the guessed product name to the pipe output.
        // If the guess was not successful it will complain on the console.
        protected override void ProcessRecord() {
            try {
                var version = GetVersion();
                WriteObject(version);
                if (version == null) {
                    WriteWarning("SharePoint version was empty.");
                    return;
                }
                string description = GetDescription(version);
                if (!string.IsNullOrEmpty(description)) {
                    WriteObject(description);
                    return;
                }
                WriteWarning("Unknown SharePoint version.");
                var previous = GetPreviousRelease(version);
                if (previous.Key != null)
                    WriteWarning("Previous version before yours was {0}: {1}.",
                        previous.Key, previous.Value);
                var next = GetNextRelease(version);
                if (next.Key != null)
                    WriteWarning("Next version after yours was {0}: {1}.",
                        next.Key, next.Value);
            } catch (Exception exception) {
                WriteError(new ErrorRecord(exception, "WebAccessFailed",
                    ErrorCategory.ResourceUnavailable, ActualWebUrl));
            }
        }

        // Looks for the element /List/ServerSettings/ServerVersion. It does not use XPath
        // to avoid specifying namespaces.
        Version GetVersion() {
            var settings = GetList().ChildNodes.OfType<XmlElement>().First(
                                    item => item.LocalName == "ServerSettings");
            var version = settings.ChildNodes.OfType<XmlElement>().First(
                                    item => item.LocalName == "ServerVersion");
            var value = version.InnerText;
            WriteVerbose("Raw server version : {0}.", value);
            return string.IsNullOrEmpty(value) ? null : new Version(value);
        }

        // Looks for the element /Releases/Release/[@Version='...'] with the product name.
        string GetDescription(Version version) {
            WriteVerbose("Known server list was last updated at {0}.",
                Releases.SelectSingleNode("/Releases/@Updated").Value);
            var release = Releases.SelectSingleNode(string.Format(
                "/Releases/Release[@Version={0}]",
                    XmlUtility.FormatXPathLiteral(version.ToString())));
            return release != null ? release.Value : null;
        }

        // Goes through all releases from the newest to the oldest and returns the first release
        // that has its version number less than the specified one.
        KeyValuePair<Version, string> GetPreviousRelease(Version version) {
            var releases = Releases.Select("/Releases/Release").OfType<XPathNavigator>().
                                Select(item => new KeyValuePair<Version, string>(
                                    new Version(item.GetAttribute("Version", "")), item.Value)).
                                OrderByDescending(item => item.Key);
            KeyValuePair<Version, string> result = releases.First();
            foreach (var release in releases) {
                if (release.Key < version)
                    break;
                result = release;
            }
            return result;
        }

        // Goes through all releases from the oldest to the newest and returns the first release
        // that has its version number greater than the specified one.
        KeyValuePair<Version, string> GetNextRelease(Version version) {
            var releases = Releases.Select("/Releases/Release").OfType<XPathNavigator>().
                                Select(item => new KeyValuePair<Version, string>(
                                    new Version(item.GetAttribute("Version", "")), item.Value)).
                                OrderBy(item => item.Key);
            KeyValuePair<Version, string> result = releases.First();
            foreach (var release in releases) {
                if (release.Key > version)
                    break;
                result = release;
            }
            return result;
        }

        // Loads the XML file with known SharePoint product releases from the assembly resource
        // Resources/ServerReleases.xml.
        static GetServerVersion() {
            var assembly = Assembly.GetExecutingAssembly();
            using (var stream = assembly.GetManifestResourceStream(
                                                "SharePosh.Resources.ServerReleases.xml"))
                Releases = new XPathDocument(stream).CreateNavigator();
        }

        static readonly XPathNavigator Releases;
    }
}
