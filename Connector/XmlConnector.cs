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
using System.Xml;

namespace SharePosh
{
    // This class serves as a common ancestor for actual and fake (mocking) implementations of
    // the SharePoint server access. They share the principal of inferring object information from
    // XML payload received from the server but the actual extraction of information is left to
    // the particular descendant which recognizes its specific XML schema.

    enum ItemType {
        Common, File, Folder
    }

    abstract class XmlConnector : CacheConnector
    {
        // Initializes a new object of this class.
        public XmlConnector(DriveInfo drive) : base(drive) {}

        // Implementation of the rest of the NavigatingConnector interface.

        protected override IEnumerable<WebInfo> GetWebsDirectly(WebInfo web) {
            return QueryWebs(web).Select(source => CreateWebInfo(web, source)).ToList();
        }

        protected override IEnumerable<ItemInfo> GetItemsDirectly(ItemContainerInfo container) {
            return QueryItems(container).Select(source =>
                        CreateItemInfo(container, source)).ToList();
        }

        // Implementation of the NavigatingConnector interface support from the parent class.

        protected override WebInfo GetWebDirectly(string path) {
            return CreateWebInfo(path, QueryWeb(path));
        }

        protected override IEnumerable<ListInfo> GetAllListsDirectly(WebInfo web) {
            return QueryLists(web).Select(source => CreateListInfo(web, source)).ToList();
        }

        protected override WebFolderInfo InferWebFolderDirectly(WebInfo web, string name) {
            return CreateWebFolderInfo(web, PathUtility.JoinPath(web.Path, name));
        }

        protected override ItemInfo GetItemDirectly(ListInfo list, string path) {
            var fullPath = PathUtility.JoinPath(list.Path, path);
            return CreateItemInfo(list, fullPath, QueryItem(list, path));
        }

        // Abstract methods to support the direct SharePoint object retrieval which return raw
        // information in the XML format as it was returned by a particular SharePoint API method.

        protected abstract IEnumerable<XmlElement> QueryWebs(WebInfo web);

        protected abstract IEnumerable<XmlElement> QueryLists(WebInfo web);

        protected abstract IEnumerable<XmlElement> QueryItems(ItemContainerInfo container);

        protected abstract XmlElement QueryWeb(string path);

        protected abstract XmlElement QueryItem(ListInfo list, string path);

        // Methods creating informational objects to be returned by the drive provider which will
        // represent particular SharePoint objects in the PowerShell space. They are responsible
        // to add the resulting object to the cache by calling the parent's FinalizeInfo.

        WebInfo CreateWebInfo(WebInfo web, XmlElement source) {
            var path = PathUtility.JoinPath(web.Path, GetWebName(source));
            return CreateWebInfo(path, source);
        }

        WebInfo CreateWebInfo(string path, XmlElement source) {
            var web = new WebInfo(this, path);
            web.ID = GetWebID(source);
            web.Title = GetWebTitle(source);
            FinalizeInfo(web, source);
            return web;
        }

        WebFolderInfo CreateWebFolderInfo(WebInfo web, string path) {
            var folder = new WebFolderInfo(web, path);
            folder.Title = "";
            FinalizeInfo(folder);
            return folder;
        }

        ListInfo CreateListInfo(WebInfo web, XmlElement source) {
            var path = PathUtility.JoinPath(web.Path, GetListName(source));
            var list = new ListInfo(web, path);
            list.ID = GetListID(source);
            list.Title = GetListTitle(source);
            list.Created = GetListCreated(source);
            list.LastModified = GetListLastModified(source);
            list.LastDeleted = GetListLastDeleted(source);
            list.ItemCount = GetListItemCount(source);
            if (HasListFields(source))
                list.Fields = GetListFields(source).ToList();
            if (web.ID.IsEmpty())
                web.ID = GetListWebID(source);
            FinalizeInfo(list, source);
            return list;
        }

        ItemInfo CreateItemInfo(ItemContainerInfo container, XmlElement source) {
            var path = PathUtility.JoinPath(container.Path, GetItemName(source));
            return CreateItemInfo(container.List, path, source);
        }

        ItemInfo CreateItemInfo(ListInfo list, string path, XmlElement source) {
            ItemInfo item;
            switch (GetItemType(source)) {
            case ItemType.Common:
                item = new ItemInfo(list, path); break;
            case ItemType.File:
                item = new FileInfo(list, path);
                ((FileInfo) item).Size = GetFileSize(source);
                break;
            default: // case ItemType.Folder:
                item = new FolderInfo(list, path);
                ((FolderInfo) item).ChildCount = GetFolderChildCount(source);
                break;
            }
            item.ID = GetItemID(source);
            item.UniqueID = GetItemUniqueID(source);
            item.Title = GetItemTitle(source);
            item.Created = GetItemCreated(source);
            item.LastModified = GetItemLastModified(source);
            FinalizeInfo(item, source);
            return item;
        }

        void FinalizeInfo(Info info, XmlElement source) {
            foreach (XmlAttribute attribute in source.Attributes)
                info.Properties[attribute.Name] = attribute.Value;
            FinalizeInfo(info);
        }

        // Abstract methods extracting particular SharePoint object properties from the raw XML
        // information about the same object. Their implementation must understand the XML format
        // returned by the querying methods declared above.

        protected abstract Guid GetWebID(XmlElement source);

        protected abstract string GetWebName(XmlElement source);

        protected abstract string GetWebTitle(XmlElement source);

        protected abstract Guid GetListID(XmlElement source);

        protected abstract Guid GetListWebID(XmlElement source);

        protected abstract string GetListName(XmlElement source);

        protected abstract string GetListTitle(XmlElement source);

        protected abstract DateTime GetListCreated(XmlElement source);

        protected abstract DateTime GetListLastModified(XmlElement source);

        protected abstract DateTime GetListLastDeleted(XmlElement source);

        protected abstract bool HasListFields(XmlElement source);

        protected abstract int GetListItemCount(XmlElement source);

        protected abstract IEnumerable<FieldInfo> GetListFields(XmlElement source);

        protected abstract ItemType GetItemType(XmlElement source);

        protected abstract int GetItemID(XmlElement source);

        protected abstract Guid GetItemUniqueID(XmlElement source);

        protected abstract string GetItemName(XmlElement source);

        protected abstract string GetItemTitle(XmlElement source);

        protected abstract DateTime GetItemCreated(XmlElement source);

        protected abstract DateTime GetItemLastModified(XmlElement source);

        protected abstract int GetFolderChildCount(XmlElement source);

        protected abstract int GetFileSize(XmlElement source);

        // Implementation of the ModifyingConnector interface. The methods perform the operation
        // and use its raw XML result to create a new item similarly to the item getting and
        // listing methods.

        protected override ItemInfo RenameItemDirectly(ItemInfo item, string newName) {
            return CreateItemInfo(item.List, RawRenameItem(item, newName));
        }

        protected override ItemInfo CopyItemDirectly(ItemInfo item, ItemContainerInfo target,
                                                     bool recurse, string newName) {
            return CreateItemInfo(item.List, RawCopyItem(item, target, recurse, newName));
        }

        protected override ItemInfo MoveItemDirectly(ItemInfo item, ItemContainerInfo target) {
            return CreateItemInfo(item.List, RawMoveItem(item, target));
        }

        protected override WebInfo AddWebDirectly(WebInfo web, WebCreationParameters parameters) {
            var path = PathUtility.JoinPath(web.Path, parameters.Name);
            return CreateWebInfo(path, RawAddWeb(web, parameters));
        }

        protected override ListInfo AddListDirectly(WebInfo web,
                                                    ListCreationParameters parameters) {
            return CreateListInfo(web, RawAddList(web, parameters));
        }

        protected override FolderInfo AddFolderDirectly(ItemContainerInfo container, string name) {
            return (FolderInfo) CreateItemInfo(container, RawAddFolder(container, name));
        }

        protected override ItemInfo AddItemDirectly(ItemContainerInfo container, string name) {
            return CreateItemInfo(container, RawAddItem(container, name));
        }

        // Abstract methods modifying the actual SharePoint objects. Their implementation must
        // return the same raw XML information as the querying methods above.

        protected abstract XmlElement RawRenameItem(ItemInfo item, string newName);

        protected abstract XmlElement RawCopyItem(ItemInfo item, ItemContainerInfo target,
                                                  bool recurse, string newName);

        protected abstract XmlElement RawMoveItem(ItemInfo item, ItemContainerInfo target);

        protected abstract XmlElement RawAddWeb(WebInfo web, WebCreationParameters parameters);

        protected abstract XmlElement RawAddList(WebInfo web, ListCreationParameters parameters);

        protected abstract XmlElement RawAddFolder(ItemContainerInfo container, string name);

        protected abstract XmlElement RawAddItem(ItemContainerInfo container, string name);

        // Implementation of the ContentConnector interface support. Methods creating a new file
        // use the same raw XML information to handle caching and returning the new file as the
        // object creating methods above.

        protected override FileInfo AddFileDirectly(ContentContainerInfo container, string name,
                                                    Stream content) {
            return (FileInfo) CreateItemInfo(container.List, UploadFile(container, name, content));
        }

        // Abstract methods to support the direct SharePoint content creation which return raw
        // information about the new object in same XML format as the querying methods above.

        protected abstract XmlElement UploadFile(ContentContainerInfo container, string name,
                                                 Stream content);
    }
}
