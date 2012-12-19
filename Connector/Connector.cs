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
using System.IO;

namespace SharePosh
{
    // Every connector must be capable of resolving an URL path to a server object.
    public interface Connector
    {
        string WebUrl { get; }

        Log Log { get; set; }

        Info GetObject(string path);

        bool ExistsObject(string path);
    }

    // Navigating providers need enumerating children. Various containers need different API
    // methods to list their children; while the server objects offer homogenous interface
    // the connector has to be approached on a container-specific base.
    public interface NavigatingConnector
    {
        IEnumerable<WebInfo> GetWebs(WebInfo web);

        IEnumerable<WebFolderInfo> GetWebFolders(WebInfo web);

        IEnumerable<ListInfo> GetLists(ListContainerInfo container);

        IEnumerable<ItemInfo> GetItems(ItemContainerInfo container);
    }

    // Modifications and binary content access come as an optional extensions testable by casting
    // to the particular interface. Creating new items needs quite a few arguments which are
    // gathered together in parameter classes for convenience. The offer also conversion from the
    // dynamic parameter classes which directly consume the PowerShell input.

    public class WebCreationParameters
    {
        public string Name { get; set; }

        public string Title { get; set; }

        public string Description { get; set; }

        public string Template { get; set; }

        public uint Language { get; set; }

        public uint Locale { get; set; }

        public uint CollationLocale { get; set; }

        public bool? UniquePermissions { get; set; }

        public bool? Anonymous { get; set; }

        public bool? Presence{ get; set; }

        public static WebCreationParameters Create(string name, NewWebParameters parameters) {
            if (name == null)
                throw new ArgumentNullException("name");
            if (parameters == null)
                throw new ArgumentNullException("parameters");
            var created = new WebCreationParameters {
                Name = name, Title = parameters.Title, Description = parameters.Description,
                Template = parameters.Template, Language = parameters.Language,
                Locale = parameters.Locale, CollationLocale = parameters.CollationLocale,
            };
            if (parameters.UniquePermissions.IsPresent)
                created.UniquePermissions = parameters.UniquePermissions;
            if (parameters.Anonymous.IsPresent)
                created.Anonymous = parameters.Anonymous;
            if (parameters.Presence.IsPresent)
                created.Presence = parameters.Presence;
            return created;
        }

        public void Check() {
            if (string.IsNullOrEmpty(Name) && string.IsNullOrEmpty(Title))
                throw new ArgumentException("The name or title of the new web must be set.");
            if (string.IsNullOrEmpty(Template))
                throw new ArgumentException("The template of the new web must be specified.");
        }
    }

    public class ListCreationParameters
    {
        public string Name { get; set; }

        public string Description { get; set; }

        public int Template { get; set; }

        public static ListCreationParameters Create(string name, NewListParameters parameters) {
            if (name == null)
                throw new ArgumentNullException("name");
            if (parameters == null)
                throw new ArgumentNullException("parameters");
            return new ListCreationParameters {
                Name = name, Description = parameters.Description, Template = parameters.Template
            };
        }

        public void Check() {
            if (string.IsNullOrEmpty(Name))
                throw new ArgumentException("The name of the new list must be provided.");
            if (Template == 0)
                throw new ArgumentException("The template of the new list must be specified.");
        }
    }

    public interface ModifyingConnector
    {
        ItemInfo RenameItem(ItemInfo item, string newName);

        ItemInfo CopyItem(ItemInfo item, ItemContainerInfo target, bool recurse, string newName);

        ItemInfo MoveItem(ItemInfo item, ItemContainerInfo target);

        void RemoveWeb(WebInfo web);

        void RemoveList(ListInfo list);

        void RemoveItem(ItemInfo item);

        WebInfo AddWeb(WebInfo web, WebCreationParameters parameters);

        ListInfo AddList(WebInfo web, ListCreationParameters parameters);

        FolderInfo AddFolder(ItemContainerInfo container, string name);

        ItemInfo AddItem(ItemContainerInfo container, string name);
    }

    public interface ContentConnector
    {
        Stream OpenFile(FileInfo file, string version);

        void SaveFile(FileInfo file, Stream content);

        FileInfo AddFile(ContentContainerInfo container, string name, Stream content);
    }

    public interface CachingConnector
    {
        void ClearCache(bool includeRoot);
    }

    public interface TestingConnector
    {}

    // Simplifies getting from the common connector interface to a specific interface for a
    // particular usage scenario.
    static class ConnectorExtension
    {
        public static NavigatingConnector GetNavigating(this Connector connector) {
            var navigating = connector as NavigatingConnector;
            if (navigating == null)
                throw new ApplicationException("Navigation is not supported.");
            return navigating;
        }

        public static ModifyingConnector GetModifying(this Connector connector) {
            var modifying = connector as ModifyingConnector;
            if (modifying == null)
                throw new ApplicationException("Modifications are not supported.");
            return modifying;
        }

        public static ContentConnector GetContent(this Connector connector) {
            var content = connector as ContentConnector;
            if (content == null)
                throw new ApplicationException("Content access is not supported.");
            return content;
        }
    }
}
