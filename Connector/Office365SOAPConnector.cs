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
using System.Net;
using System.Web.Services.Protocols;
using System.Xml;
using SharePosh.SOAP.SiteData;
using SharePosh.SOAP.Webs;

namespace SharePosh
{
    // Connector specialization for the communication with the Office 365 (SharePoint Online)
    // server via web services. The WebUrl property must point to the root site collection of
    // the Office 365 web site otherwise the authentication fails. Use the Root property if
    // you want to start with a sub-site.
    class Office365SOAPConnector : SOAPConnector
    {
        public Office365SOAPConnector(DriveInfo drive) : base(drive) {}

        // Overridden methods to handle different behaviour of the Office 365 web services.

        protected override XmlElement QueryWeb(string path) {
            // Workaround for the failure to call Webs.GetWeb for a root Office 365 web site.
            // It throws SoapException without no other details. Luckily, the three attributes
            // we need are returned also by SiteData.GetWeb (with other information). Let's use
            // this for the root web site only; sub-sites have no problem with the Webs.GetWeb.
            if (path.IsEmpty()) {
                Log.Verbose("Querying root web at /{0}.", path);
                _sWebMetadata web;
                _sWebWithTime[] webs;
                _sListWithTime[] lists;
                _sFPUrl[] fps;
                string roles;
                string[] users;
                string[] groups;
                GetService<SiteData>(path).GetWeb(out web, out webs, out lists,
                    out fps, out roles, out users, out groups);
                var document = new XmlDocument();
                document.LoadXml(string.Format(
                    @"<Web Name="""" Id=""{0}"" Url=""{1}"" Title=""{2}"" />",
                    web.WebID, Drive.WebUrl, web.Title));
                return document.DocumentElement;
            }
            Log.Verbose("Querying web at /{0}.", path);
            var fullPath = PathUtility.JoinPath(Drive.WebUrl, path);
            return (XmlElement) GetService<Webs>(path).GetWeb(fullPath);
        }

        // Helpers getting network communication objects. They use an alternative authentication
        // because the standard HTTP authentication doesn't work with Office 365.

        protected override WebClient GetClient() {
            var client = new WebClient();
            client.Headers.Add(HttpRequestHeader.Cookie,
                                    Cookies.GetCookieHeader(new Uri(Drive.WebUrl)));
            return client;
        }

        protected override T CreateService<T>(string url) {
            return GetService<T>(url, Cookies, Drive.Timeout);
        }

        internal static T GetService<T>(string url, CookieContainer cookies, int timeout)
                                where T : SoapHttpClientProtocol, new() {
            var name = typeof(T).Name;
            var service = new T();
            service.Url = PathUtility.JoinPath(url, "_vti_bin", name + ".asmx");
            service.CookieContainer = cookies;
            if (timeout > 0)
                service.Timeout = timeout;
            return (T) service;
        }

        CookieContainer Cookies {
            get {
                return cookies ?? (cookies = Office365CookieHelper.GetCookies(
                    Drive.WebUrl, Drive.Credential));
            }
        }
        CookieContainer cookies;
    }
}
