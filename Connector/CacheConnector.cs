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
using System.Linq;

namespace SharePosh
{
    // Adds caching of the returned SharePoint objects. It uses some internal properties of the
    // Info descendants to prevent server calls even more than remembering single objects.
    abstract class CacheConnector : DriveConnector, ModifyingConnector, ContentConnector,
                                    CachingConnector
    {
        // Initializes a new object of this class.
        public CacheConnector(DriveInfo drive) : base(drive) {}

        // Reimplementation of the internal object access to utilize caching.

        protected override Info ExistsObjectInternal(string path) {
            return Cache.GetObjectOrDefault(path) ?? base.ExistsObjectInternal(path);
        }

        // Implementation of the rest of the NavigatingConnector interface.

        public override IEnumerable<WebInfo> GetWebs(WebInfo web) {
            if (web == null)
                throw new ArgumentNullException("web");
            // I wouldn't so aggressively store the child webs but calling Get-ChildItem cmdlet
            // checks multiple times the existence of a "*" child. Because this triggers the
            // children retrieval I'd better cache them. The cache stores resolved SharePoint
            // objects by their path. I could adapt the cache to accommodate the collections of
            // children too but it was easier to utilize an internal property in the parent web
            // instance, which even performs better.
            if (web.Webs == null || !Cache.Check())
                web.Webs = GetWebsDirectly(web);
            return web.Webs;
        }

        public override IEnumerable<ItemInfo> GetItems(ItemContainerInfo container) {
            // I wouldn't so aggressively store the child items but calling Get-ChildItem cmdlet
            // checks multiple times the existence of a "*" child. Because this triggers the
            // children retrieval I'd better cache them. The cache stores resolved SharePoint
            // objects by their path. I could adapt the cache to accommodate the collections of
            // children too but it was easier to utilize an internal property in the parent web
            // instance, which even performs better.
            if (container == null)
                throw new ArgumentNullException("container");
            var list = container as ListInfo;
            var folder = container as FolderInfo;
            var items = list != null ? list.ChildItems : folder.ChildItems;
            if (items == null || !Cache.Check()) {
                items = GetItemsDirectly(container);
                if (list != null)
                    list.ChildItems = items;
                else
                    folder.ChildItems = items;
            }
            return items;
        }

        // Implementation of the NavigatingConnector interface support from the parent class.
        // They look for their result first to the cache because they can be called multiple
        // times by the code in the connector, the drive provider or the originating cmdlet.

        protected override WebInfo GetWeb(string path) {
            return (WebInfo) Cache.GetObjectOrDefault(path) ?? GetWebDirectly(path);
        }

        protected override IEnumerable<ListInfo> GetAllLists(WebInfo web) {
            // The cache stores resolved SharePoint objects by their path. I could adapt the
            // cache to accommodate the collections of children too but it was easier to utilize
            // an internal property in the parent web instance, which even performs better.
            if (web.Lists == null || !Cache.Check())
                web.Lists = GetAllListsDirectly(web);
            return web.Lists;
        }

        protected override WebFolderInfo InferWebFolder(WebInfo web, string name) {
            var path = PathUtility.JoinPath(web.Path, name);
            return (WebFolderInfo) Cache.GetObjectOrDefault(path) ??
                InferWebFolderDirectly(web, name);
        }

        protected override ItemInfo GetItem(ListInfo list, string path) {
            var fullPath = PathUtility.JoinPath(list.Path, path);
            return (ItemInfo) Cache.GetObjectOrDefault(fullPath) ??
                                GetItemDirectly(list, path);
        }

        protected override bool HasItem(ListInfo list, string path) {
            if (path.EndsWith("/*")) {
                var parent = PathUtility.JoinPath(list.Path, PathUtility.GetParentPath(path));
                if (parent.IsEmpty())
                    return list.HasChildren();
            }
            var fullPath = PathUtility.JoinPath(list.Path, path);
            if (Cache.GetObjectOrDefault(fullPath) == null)
                try {
                    var container = GetItemDirectly(list, path) as ContainerInfo;
                    if (container == null)
                        return false;
                    return container.HasChildren();
                } catch {
                    return false;
                }
            return true;
        }

        // Implementation of the CachingConnector and helper caching methods.

        public override void ClearCache(bool includeRoot) {
            Cache.Invalidate();
            base.ClearCache(includeRoot);
        }

        void RemoveCachedItem(ItemInfo item) {
            Cache.RemoveObject(item);
            var path = PathUtility.GetParentPath(item.Path);
            Info container;
            if (!path.IsEmpty() && Cache.TryGetObject(path, out container)) {
                var list = container as ListInfo;
                var folder = container as FolderInfo;
                var items = list != null ? list.ChildItems : folder.ChildItems;
                if (items != null) {
                    items = items.Where(current => current.ID != item.ID).ToList();
                    if (list != null)
                        list.ChildItems = items;
                    else
                        folder.ChildItems = items;
                }
            }
        }

        void AddCachedItem(ItemInfo item, ItemContainerInfo container = null) {
            if (container == null) {
                var path = PathUtility.GetParentPath(item.Path);
                Info parent;
                if (!path.IsEmpty() && Cache.TryGetObject(path, out parent))
                    container = (ItemContainerInfo) parent;
            }
            if (container != null) {
                var list = container as ListInfo;
                var folder = container as FolderInfo;
                var items = list != null ? list.ChildItems : folder.ChildItems;
                if (items != null) {
                    items = items.Concat(new[] { item }).ToList();
                    if (list != null)
                        list.ChildItems = items;
                    else
                        folder.ChildItems = items;
                }
            }
        }

        Cache Cache {
            get { return cache ?? (cache = new Cache(Drive.CacheKeepPeriod)); }
        }
        Cache cache;

        // Abstract methods to support the direct SharePoint object retrieval. They must call the
        // Finalizeinfo method so that the returned object is correctly put to the cache.

        protected abstract IEnumerable<WebInfo> GetWebsDirectly(WebInfo web);

        protected abstract IEnumerable<ItemInfo> GetItemsDirectly(ItemContainerInfo container);

        protected abstract WebInfo GetWebDirectly(string path);

        protected abstract IEnumerable<ListInfo> GetAllListsDirectly(WebInfo web);

        protected abstract WebFolderInfo InferWebFolderDirectly(WebInfo web, string path);

        protected abstract ItemInfo GetItemDirectly(ListInfo list, string path);

        protected void FinalizeInfo(Info info) {
            Cache.PutObject(info);
        }

        // Implementation of the ModifyingConnector interface. The methods remove the existing
        // item from cache, call the descendant to perform the actual operation and/or return
        // the new object similarly to the item getting and listing methods.

        public void RemoveWeb(WebInfo web) {
            if (web == null)
                throw new ArgumentNullException("web");
            Cache.RemoveObject(web);
            if (web.Webs != null)
                web.Webs = web.Webs.Where(item => item.ID != web.ID).ToList();
            RemoveWebDirectly(web);
        }

        public void RemoveList(ListInfo list) {
            if (list == null)
                throw new ArgumentNullException("list");
            Cache.RemoveObject(list);
            if (list.Web.Lists != null)
                list.Web.Lists = list.Web.Lists.Where(item => item.ID != list.ID).ToList();
            RemoveListDirectly(list);
        }

        public void RemoveItem(ItemInfo item) {
            if (item == null)
                throw new ArgumentNullException("item");
            RemoveCachedItem(item);
            RemoveItemDirectly(item);
        }

        public ItemInfo RenameItem(ItemInfo item, string newName) {
            if (newName == null)
                throw new ArgumentNullException("newName");
            if (string.IsNullOrEmpty(newName))
                throw new ArgumentException("The new item name must not be empty.");
            Cache.RemoveObject(item);
            var renamed = RenameItemDirectly(item, newName);
            AddCachedItem(renamed);
            return renamed;
        }

        public ItemInfo CopyItem(ItemInfo item, ItemContainerInfo target,
                                         bool recurse, string newName) {
            if (target == null)
                throw new ArgumentNullException("target");
            var copy = CopyItemDirectly(item, target, recurse, newName);
            AddCachedItem(copy, target);
            return copy;
        }

        public ItemInfo MoveItem(ItemInfo item, ItemContainerInfo target) {
            if (target == null)
                throw new ArgumentNullException("target");
            var moved = MoveItemDirectly(item, target);
            RemoveCachedItem(item);
            AddCachedItem(moved, target);
            return moved;
        }

        public WebInfo AddWeb(WebInfo web, WebCreationParameters parameters) {
            if (web == null)
                throw new ArgumentNullException("web");
            if (parameters == null)
                throw new ArgumentNullException("parameters");
            parameters.Check();
            var newWeb = AddWebDirectly(web, parameters);
            if (web.Webs != null)
                web.Webs = web.Webs.Concat(new[] { newWeb }).ToList();
            return newWeb;
        }

        public ListInfo AddList(WebInfo web, ListCreationParameters parameters) {
            if (web == null)
                throw new ArgumentNullException("web");
            if (parameters == null)
                throw new ArgumentNullException("parameters");
            parameters.Check();
            var list = AddListDirectly(web, parameters);
            if (web.Lists != null)
                web.Lists = web.Lists.Concat(new[] { list }).ToList();
            return list;
        }

        public FolderInfo AddFolder(ItemContainerInfo container, string name) {
            if (container == null)
                throw new ArgumentNullException("container");
            if (name == null)
                throw new ArgumentNullException("name");
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("The name of a new folder must not be empty.");
            var folder = (FolderInfo) AddFolderDirectly(container, name);
            AddCachedItem(folder, container);
            return folder;
        }

        public ItemInfo AddItem(ItemContainerInfo container, string name) {
            if (container == null)
                throw new ArgumentNullException("container");
            if (name == null)
                throw new ArgumentNullException("name");
            if (name.IsEmpty())
                throw new ArgumentException("The name of a new item must not be empty.");
            var item = AddItemDirectly(container, name);
            AddCachedItem(item, container);
            return item;
        }

        // Abstract methods modifying the actual SharePoint objects.

        protected abstract void RemoveWebDirectly(WebInfo web);

        protected abstract void RemoveListDirectly(ListInfo list);

        protected abstract void RemoveItemDirectly(ItemInfo item);

        protected abstract ItemInfo RenameItemDirectly(ItemInfo item, string newName);

        protected abstract ItemInfo CopyItemDirectly(ItemInfo item, ItemContainerInfo target,
                                                     bool recurse, string newName);

        protected abstract ItemInfo MoveItemDirectly(ItemInfo item, ItemContainerInfo target);

        protected abstract WebInfo AddWebDirectly(WebInfo web, WebCreationParameters parameters);

        protected abstract ListInfo AddListDirectly(WebInfo web,
                                                    ListCreationParameters parameters);

        protected abstract FolderInfo AddFolderDirectly(ItemContainerInfo container, string name);

        protected abstract ItemInfo AddItemDirectly(ItemContainerInfo container, string name);

        // Implementation of the ContentConnector interface. Methods creating a new file use the
        // same caching principle as the object adding methods above.

        public abstract Stream OpenFile(FileInfo file, string version);

        public abstract void SaveFile(FileInfo file, Stream content);

        public FileInfo AddFile(ContentContainerInfo container, string name, Stream content) {
            if (container == null)
                throw new ArgumentNullException("container");
            if (name == null)
                throw new ArgumentNullException("name");
            if (name.IsEmpty())
                throw new ArgumentException("The name of a new file must not be empty.");
            if (content == null)
                throw new ArgumentNullException("content");
            var file = AddFileDirectly(container, name, content);
            AddCachedItem(file, container);
            return file;
        }

        // Abstract methods to support the direct SharePoint content creation.

        protected abstract FileInfo AddFileDirectly(ContentContainerInfo container, string name,
                                                    Stream content);
    }
}
