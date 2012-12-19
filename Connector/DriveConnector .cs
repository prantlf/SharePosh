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

namespace SharePosh
{
    // SharePoint object model connector which gets initialized by DriveInfo. It reads and caches
    // the root object (web, web folder or list) to save querying of this object when an item
    // is being resolved by its path. It introduces enumeration of all lists on a web as a means
    // to get a list or a web folder because the SharePoint object model does not support looking
    // for them by their URL.
    abstract class DriveConnector : Connector, NavigatingConnector, CachingConnector
    {
        // Initializes a new object of this class.
        protected DriveConnector(DriveInfo drive) {
            if (drive == null)
                throw new ArgumentNullException("drive");
            Drive = drive;
        }

        // Remembers the drive information as a pointer to the SharePoint server and the object
        // to start the navigation with.
        protected DriveInfo Drive { get; private set; }

        public string WebUrl {
            get { return Drive.WebUrl; }
        }

        // The logger is set by the provider before calling a method of this connector.
        public Log Log {
            get { return log ?? DummyLog.Instance; }
            set { log = value; }
        }
        Log log;

        // Caches a web, a web folder or a list with the longest URL contained in the concatenation
        // of the DriveInfo properties specifying the root of the drive: WebUrl and Root. Because
        // SharePoint content is organized by webs and lists it avoids re-querying this uppermost
        // container to get its descendant. Mind that this property doesn't point to the drive
        // root if goes as deep as to a folder or an item.
        protected Info Root {
            get { return root ?? (root = GetRoot()); }
        }
        Info root;

        Info GetRoot() {
            // The DriveInfo.Root property may be empty or not there must always be a web
            // accessible at the URL specified by the DriveInfo.WebUrl property - the starting one.
            var web = GetWeb("");
            var parts = PathUtility.SplitPath(Drive.Root);
            if (!parts.Any())
                return web;
            // Parts of the path specified by the DriveInfo.Root property - names of containers -
            // are tried to match webs, web folders and lists storing the index of the next not
            // yet resolved part.
            var index = 0;
            // Firstly follow the names on path which represent webs.
            web = FindWeb(web, parts, ref index, false);
            if (index == parts.Length)
                return web;
            // The next part after the last web can be either a web folder or a list.
            var listOrFolder = GetListOrWebFolder(web, parts[index]);
            if (index == parts.Length)
                return listOrFolder;
            // If the root path goes higher than to a list we won't cache it; everything above a
            // list is accessed by a CAML query using the list as root. If we received a web
            // folder by the previous operation it still pays off to retrieve the following list;
            // only lists can be children of web folders.
            var list = listOrFolder as ListInfo;
            return list ?? GetList((WebFolderInfo) listOrFolder, parts[index]);
        }

        // Implementation of the object resolution. The path is supposed to contain just forward
        // slashes with the leading and trailing slash trimmed. It starts with the second part of
        // the drive root specified by the DriveInfo.Root property but not with the absolute URL
        // of the web site specified by the DriveInfo.WebUrl property.

        public Info GetObject(string path) {
            path = PathUtility.NormalizePath(path);
            return ExistsObjectInternal(path);
        }

        public bool ExistsObject(string path) {
            path = PathUtility.NormalizePath(path);
            return HasObjectInternal(path);
        }

        protected virtual Info ExistsObjectInternal(string path) {
            // Cut out the part of the path that goes only down to the cached root. The deeper
            // half of the part has been already resolved and cached to the Root property.
            path = path.Substring(Root.Path.Length).TrimStart('/');
            var parts = PathUtility.SplitPath(path);
            if (!parts.Any())
                return Root;
            // Parts of the path above the root - names of containers - are tried to match webs,
            // web folders, lists and items storing the index of the next not yet resolved part.
            var index = 0;
            var web = Root as WebInfo;
            Info folderOrList;
            if (web != null) {
                // If the root is a web we have to start trying if the first parts of the path
                // aren't webs too. When finding the uppermost web and still not reaching the
                // end of the path the next container must be a web folder or a list.
                web = FindWeb(web, parts, ref index, false);
                if (index == parts.Length)
                    return web;
                folderOrList = GetListOrWebFolder(web, parts[index++]);
                if (index == parts.Length)
                    return folderOrList;
            } else {
                // The root isn't a web; it's a web folder or a list. We've saved the web
                // resolution now which had been done when the Root property was initialized.
                folderOrList = Root;
            }
            var folder = folderOrList as WebFolderInfo;
            ListInfo list;
            if (folder != null) {
                // If the previously resolved child is a web folder the next container must be a
                // list. Web folders are artificial containers to support lists that aren't placed
                // directly as children of the web.
                list = GetList((WebFolderInfo) folderOrList, parts[index++]);
                if (index == parts.Length)
                    return list;
            } else {
                // The previously resolved child isn't a web folder; it must be a list. We've
                // saved the list resolution now. Descendant containers are not cached because
                // everything above a list is accessed by a CAML query using the list as root.
                list = (ListInfo) folderOrList;
            }
            // The rest of the path must point to a folder, a file or a common item in the list.
            return GetItem(list, PathUtility.JoinPath("", parts, index));
        }

        // This method accepts paths ending with asterisks. PowerShell uses them to support the
        // tab-completion and that's why supporting them explicitly avoids an exception when the
        // asterisk would be sent to SharePoint as a web, list or item name to test if they exist.
        // Unfortunately, paths wildcard are sent by PowerShell in spite of this provider does not
        // declares that it supports them.
        protected virtual bool HasObjectInternal(string path) {
            // Cut out the part of the path that goes only down to the cached root. The deeper
            // half of the part has been already resolved and cached to the Root property.
            path = path.Substring(Root.Path.Length).TrimStart('/');
            var parts = PathUtility.SplitPath(path);
            if (!parts.Any())
                return true;
            // Parts of the path above the root - names of containers - are tried to match webs,
            // web folders, lists and items storing the index of the next not yet resolved part.
            var index = 0;
            var web = Root as WebInfo;
            Info folderOrList;
            if (web != null) {
                // If the root is a web we have to start trying if the first parts of the path
                // aren't webs too. When finding the uppermost web and still not reaching the
                // end of the path the next container must be a web folder or a list.
                web = FindWeb(web, parts, ref index, true);
                if (index == parts.Length)
                    return true;
                if (parts[index] == "*")
                    return web.HasChildren();
                folderOrList = GetListOrWebFolder(web, parts[index++]);
                if (index == parts.Length)
                    return true;
            } else {
                // The root isn't a web; it's a web folder or a list. We've saved the web
                // resolution now which had been done when the Root property was initialized.
                folderOrList = Root;
            }
            var folder = folderOrList as WebFolderInfo;
            if (parts[index] == "*")
                return folder.HasChildren();
            ListInfo list;
            if (folder != null) {
                // If the previously resolved child is a web folder the next container must be a
                // list. Web folders are artificial containers to support lists that aren't placed
                // directly as children of the web.
                list = GetList((WebFolderInfo) folderOrList, parts[index++]);
                if (index == parts.Length)
                    return true;
            } else {
                // The previously resolved child isn't a web folder; it must be a list. We've
                // saved the list resolution now. Descendant containers are not cached because
                // everything above a list is accessed by a CAML query using the list as root.
                list = (ListInfo) folderOrList;
            }
            if (parts[index] == "*")
                return list.HasChildren();
            // The rest of the path must point to a folder, a file or a common item in the list.
            return HasItem(list, PathUtility.JoinPath("", parts, index));
        }

        WebInfo FindWeb(WebInfo web, string[] parts, ref int index, bool skipWildcards) {
            // We continue appending the current part of the path and advancing to the next one as
            // long as we succeed in resolving a web with that path. The last successful result is
            // returned and if the already the first part failed the starting web is returned.
            try {
                do {
                    var name = parts[index];
                    if (skipWildcards && name == "*")
                        break;
                    web = GetWeb(PathUtility.JoinPath(web.Path, name));
                } while (++index < parts.Length);
            } catch {}
            return web;
        }

        Info GetListOrWebFolder(WebInfo web, string name) {
            // Direct children of a web are either web folders or lists. Although the GetAllLists
            // methods returns the same count of objects - web folders are inferred from lists
            // which are not placed directly below the web - we want to walk through objects that
            // are placed directly below the web.
            var folders = GetWebFolders(web).Cast<Info>();
            var lists = GetLists(web).Cast<Info>();
            var listOrFolder = folders.Concat(lists).FirstOrDefault(item =>
                                                                        item.Name.EqualsCI(name));
            if (listOrFolder == null)
                throw new ApplicationException("No list or web folder found.");
            return listOrFolder;
        }

        ListInfo GetList(WebFolderInfo folder, string name) {
            // Web folders are artificial objects that cannot be queried by the SharePoint object
            // model. They can contain only lists and because all lists on a web can be obtained
            // we can look for the particular list with a relative path combined from the folder
            // and the list names.
            name = PathUtility.JoinPath(folder.Name, name);
            var list = GetAllLists(folder.Web).FirstOrDefault(item =>
                                                    item.WebRelativePath.EqualsCI(name));
            if (list == null)
                throw new ApplicationException("No list found.");
            return list;
        }

        // Implementation of the NavigatingConnector. Methods retrieving lists and web folders
        // can be implemented here already because of the protected abstract GetAllLists method.
        // The SharePoint object model doesn't offer getter for lists by their URL and that's why
        // even getting a single web folder or list is performed by enumerating all lists of the
        // web. Descendants are encouraged to cache the lists at least for a single PowerShell
        // operation because the GetAllLists can be called multiple times even within this class.

        public abstract IEnumerable<WebInfo> GetWebs(WebInfo web);

        public IEnumerable<WebFolderInfo> GetWebFolders(WebInfo web) {
            if (web == null)
                throw new ArgumentNullException("web");
            // Web folders are artificial objects that cannot be queried by the SharePoint object
            // model. They can contain only lists and because all lists on a web can be obtained
            // we infer web folders by cutting parent container names from list relative paths.
            var names = GetAllLists(web).Select(list =>
                    PathUtility.GetParentPath(list.WebRelativePath)).
                Distinct(ConfigurableComparer<string>.CaseInsensitive).Where(name => name.Any());
            return names.Select(name => InferWebFolder(web, name)).ToList();
        }

        public IEnumerable<ListInfo> GetLists(ListContainerInfo container) {
            if (container == null)
                throw new ArgumentNullException("container");
            Func<ListInfo, bool> filter;
            if (container == container.Web) {
                // Lists placed directly on the web have their relative path equal to their name
                // which means that it contains no slash. Because the SharePoint object model
                // returns only all lists on a web regardless in what web folder they are we have
                // to filter them.
                filter = list => !list.WebRelativePath.Contains('/');
            } else {
                // Lists placed not directly on the web must have their relative path concatenated
                // from their parent web folder name and their actual list name which means that
                // it must start with the web folder name and slash. Because the SharePoint object
                // model returns only all lists on a web regardless in what web folder they are we
                // have to filter them.
                var start = container.Name + "/";
                filter = list => list.WebRelativePath.StartsWithCI(start);
            }
            return GetAllLists(container.Web).Where(filter).ToList();
        }

        public abstract IEnumerable<ItemInfo> GetItems(ItemContainerInfo container);

        // Abstract methods to support the general SharePoint object access and children
        // navigation implemented here. Descendants are supposed to implement them together with
        // the rest of the NavigatingConnector interface declared abstract above.

        protected abstract WebInfo GetWeb(string path);

        protected abstract IEnumerable<ListInfo> GetAllLists(WebInfo web);

        protected abstract WebFolderInfo InferWebFolder(WebInfo web, string name);

        protected abstract ItemInfo GetItem(ListInfo list, string path);

        protected abstract bool HasItem(ListInfo list, string path);

        // Implementation of the CachingConnector interface.

        public virtual void ClearCache(bool includeRoot) {
            if (includeRoot)
                root = null;
        }
    }
}
