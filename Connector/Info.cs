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
    // Common information about a server object: path (relative to the drive root web URL and
    // including the name), title for displaying purposes and the property bag with the data
    // received from the server.
    public interface Info
    {
        string Path { get; }

        string Name { get; }

        string Title { get; }

        string ServerRelativePath { get; }

        Dictionary<string, object> Properties { get; }
    }

    // Implements the common server object interface for objects obtained by a server connector.
    public abstract class ConnectedInfo : Info
    {
        protected ConnectedInfo(Connector connector, string path) : this(path) {
            if (connector == null)
                throw new ArgumentNullException("connector");
            if (path == null)
                throw new ArgumentNullException("path");
            Connector = connector;
        }

        protected ConnectedInfo(ConnectedInfo parent, string path) : this(path) {
            if (parent == null)
                throw new ArgumentNullException("parent");
            if (path == null)
                throw new ArgumentNullException("path");
            Connector = parent.Connector;
        }

        ConnectedInfo(string path) {
            if (path == null)
                throw new ArgumentNullException("path");
            Path = path;
            Properties = new Dictionary<string, object>(
                ConfigurableComparer<string>.CaseInsensitive);
        }

        protected Connector Connector { get; private set; }

        public string Path { get; private set; }

        public string Name {
            get { return PathUtility.GetChildName(Path); }
        }

        public string Title { get; internal set; }

        public Dictionary<string, object> Properties { get; private set; }

        public string ServerRelativePath {
            get {
                var url = PathUtility.JoinPath(Connector.WebUrl, Path);
                return PathUtility.GetUrlPath(url, false);
            }
        }
    }

    // Extends the common interface by removability and other manipulations. Most objects can be
    // deleted but some of them are after creation kind of immutable; no renaming or moving.
    // While the interfaces include all methods that make sense for the particular object type
    // not all connectors may support all the operations. The connector capabilities are checked
    // by casting to the particular interface when such operation is executed.

    public interface RemovableInfo : Info
    {
        void Remove(bool recurse);
    }

    public interface ManipulableInfo : RemovableInfo
    {
        Info Rename(string newName);

        Info Copy(ContainerInfo target, bool recurse, string newName);

        Info Move(ContainerInfo target);
    }

    // Some server objects can carry binary content - files and attachments, for example.
    public interface ContentInfo : Info
    {
        Stream Open(string version);

        void Save(Stream content);
    }

    // Containers contain children of different types but specific container types usually
    // allow only specific child types. Depending on which children can be contained offers
    // the specific container children manipulation methods.

    public interface ContainerInfo : Info
    {
        bool HasChildren();

        IEnumerable<Info> GetChildren();

        void CheckCanBeChild(Info child);
    }

    public interface ListContainerInfo : ContainerInfo
    {
        WebInfo Web { get; }

        string WebRelativePath { get; }

        ListInfo AddList(ListCreationParameters parameters);
    }

    public interface ItemContainerInfo : ContainerInfo
    {
        ListInfo List { get; }

        string ListRelativePath { get; }

        string WebRelativePath { get; }

        FolderInfo AddFolder(string name);

        ItemInfo AddItem(string name);
    }

    public interface ContentContainerInfo : ItemContainerInfo
    {
        ContentInfo AddFile(string name, Stream content);
    }

    // List item carrying only meta-data - neither a folder nor a file.
    public class ItemInfo : ConnectedInfo, ManipulableInfo
    {
        public ItemInfo(ListInfo list, string path) : base(list, path) {
            if (list == null)
                throw new ArgumentNullException("list");
            List = list;
        }

        public ListInfo List { get; private set; }

        public int ID { get; internal set; }

        public Guid UniqueID { get; internal set; }

        public DateTime Created { get; internal set; }

        public DateTime LastModified { get; internal set; }

        public string ListRelativePath {
            get { return Path.Substring(List.Path.Length).TrimStart('/'); }
        }

        public string WebRelativePath {
            get { return Path.Substring(List.Web.Path.Length).TrimStart('/'); }
        }

        public Info Rename(string newName) {
            return Connector.GetModifying().RenameItem(this, newName);
        }

        public Info Copy(ContainerInfo target, bool recurse, string newName) {
            var container = target as ItemContainerInfo;
            if (container == null)
                throw new ApplicationException("Target cannot contain items.");
            return Connector.GetModifying().CopyItem(this, container, recurse, newName);
        }

        public Info Move(ContainerInfo target) {
            var container = target as ItemContainerInfo;
            if (container == null)
                throw new ApplicationException("Target cannot contain items.");
            return Connector.GetModifying().MoveItem(this, container);
        }

        public virtual void Remove(bool recurse) {
            Connector.GetModifying().RemoveItem(this);
        }
    }

    // File - list item in a document library with binary content.
    public class FileInfo : ItemInfo, ContentInfo
    {
        public FileInfo(ListInfo list, string path) : base(list, path) {}

        public int Size { get; internal set; }

        public Stream Open(string version) {
            return Connector.GetContent().OpenFile(this, version);
        }

        public void Save(Stream content) {
            Connector.GetContent().SaveFile(this, content);
        }
    }

    // Folder - list item in a list or document library which can contain other list items.
    public class FolderInfo : ItemInfo, ContentContainerInfo
    {
        public FolderInfo(ListInfo list, string path) : base(list, path) {}

        public int ChildCount { get; internal set; }

        internal IEnumerable<ItemInfo> ChildItems { get; set; }

        public bool HasChildren() {
            return GetChildren().Any();
        }

        public IEnumerable<Info> GetChildren() {
            return Connector.GetNavigating().GetItems(this).Cast<Info>();
        }

        public void CheckCanBeChild(Info child) {
            if (!(child is ItemInfo))
                throw new ApplicationException("Target can contain only items.");
        }

        public override void Remove(bool recurse) {
            base.Remove(recurse);
        }

        public FolderInfo AddFolder(string name) {
            return Connector.GetModifying().AddFolder(this, name);
        }

        public ItemInfo AddItem(string name) {
            return Connector.GetModifying().AddItem(this, name);
        }

        public ContentInfo AddFile(string name, Stream content) {
            return Connector.GetContent().AddFile(this, name, content);
        }
    }

    // Meta-data of list items are stored in fields which are defined by the containing list.
    public class FieldInfo
    {
        public string Name { get; internal set; }

        public string Title { get; internal set; }

        public bool Hidden { get; internal set; }

        public bool ReadOnly { get; internal set; }
    }

    // List or document library containing list items.
    public class ListInfo : ConnectedInfo, RemovableInfo, ContentContainerInfo
    {
        public ListInfo(WebInfo web, string path) : base(web, path) {
            if (web == null)
                throw new ArgumentNullException("web");
            Web = web;
        }

        public WebInfo Web { get; private set; }

        public Guid ID { get; internal set; }

        public DateTime Created { get; internal set; }

        public DateTime LastModified { get; internal set; }

        public DateTime LastDeleted { get; internal set; }

        public int ItemCount { get; internal set; }

        internal string StringID {
            get { return ID.ToString("B"); }
        }

        internal IEnumerable<FieldInfo> Fields { get; set; }

        internal IEnumerable<ItemInfo> ChildItems { get; set; }

        ListInfo ItemContainerInfo.List {
            get { return this; }
        }

        string ItemContainerInfo.ListRelativePath {
            get { return ""; }
        }

        public string WebRelativePath {
            get { return Path.Substring(Web.Path.Length).TrimStart('/'); }
        }

        public bool HasChildren() {
            return ItemCount > 0;
        }

        public IEnumerable<Info> GetChildren() {
            return Connector.GetNavigating().GetItems(this).Cast<Info>();
        }

        public void CheckCanBeChild(Info child) {
            if (!(child is ItemInfo))
                throw new ApplicationException("Target can contain only items.");
        }

        public void Remove(bool recurse) {
            Connector.GetModifying().RemoveList(this);
        }

        public FolderInfo AddFolder(string name) {
            return Connector.GetModifying().AddFolder(this, name);
        }

        public ItemInfo AddItem(string name) {
            return Connector.GetModifying().AddItem(this, name);
        }

        public ContentInfo AddFile(string name, Stream content) {
            return Connector.GetContent().AddFile(this, name, content);
        }
    }

    // Web folder - an organizational folder at the path to a list. Some lists are placed not
    // directly after the web - they have an extra folder name on their relative path to the
    // parent web. For example, while document libraries are usually created with URL like
    // http://server/sites/mysite/Shared Documents, you will see lists created with URL like
    // http://server/sites/mysite/Lists/Annoucements. The "Lists" part is no sub-site - it just
    // helps organizing the site content.
    //
    // The provider tries hard to show the same path to an object as you see in your web browser.
    // While advancing from the parent web to its child list is a single operation it actually
    // means skipping two names in the path. PowerShell doesn't expect that and thus the relation
    // parent-child in the server object model must correspond with names in path sitting next to
    // each other. That is why the "Lists" in the example above becomes an artificial folder
    // between a web and a list - a web folder. It offers only browsing its children to support
    // PowerShell navigation; creating children - lists - is to be asked from its parent web.
    public class WebFolderInfo : ConnectedInfo, ListContainerInfo
    {
        public WebFolderInfo(WebInfo web, string path) : base(web, path) {
            Web = web;
        }

        public WebInfo Web { get; private set; }

        public string WebRelativePath {
            get { return Name; }
        }

        public void CheckCanBeChild(Info child) {
            if (!(child is ListInfo))
                throw new ApplicationException("Target can contain only lists.");
        }

        public bool HasChildren() {
            return GetChildren().Any();
        }

        public IEnumerable<Info> GetChildren() {
            return Connector.GetNavigating().GetLists(this).Cast<Info>();
        }

        public ListInfo AddList(ListCreationParameters parameters) {
            throw new InvalidOperationException("Lists can be added to a web only.");
        }
    }

    // Web - a web site is a deepest container on the path browsable by this provider. It can
    // contain other sub-webs or lists; the lists either directly or indirectly placed in web
    // folders (which are owned directly by the web).
    public class WebInfo : ConnectedInfo, RemovableInfo, ListContainerInfo
    {
        public WebInfo(Connector connector, string path) : base(connector, path) {}

        public Guid ID { get; internal set; }

        WebInfo ListContainerInfo.Web {
            get { return this; }
        }

        string ListContainerInfo.WebRelativePath {
            get { return ""; }
        }

        internal IEnumerable<WebInfo> Webs { get; set; }

        internal IEnumerable<ListInfo> Lists { get; set; }

        public bool HasChildren() {
            var navigator = Connector.GetNavigating();
            return navigator.GetWebs(this).Any() || navigator.GetLists(this).Any() ||
                navigator.GetWebFolders(this).Any();
        }

        public IEnumerable<Info> GetChildren() {
            var navigator = Connector.GetNavigating();
            return navigator.GetWebs(this).Cast<Info>().Concat(
                navigator.GetLists(this).Cast<Info>()).Concat(
                    navigator.GetWebFolders(this).Cast<Info>());
        }

        public void CheckCanBeChild(Info child) {
            if (!(child is WebFolderInfo || child is ListInfo))
                throw new ApplicationException("Target can contain only web folders or lists.");
        }

        public void Remove(bool recurse) {
            Connector.GetModifying().RemoveWeb(this);
        }

        public WebInfo AddWeb(WebCreationParameters parameters) {
            return Connector.GetModifying().AddWeb(this, parameters);
        }

        public ListInfo AddList(ListCreationParameters parameters) {
            return Connector.GetModifying().AddList(this, parameters);
        }
    }
}
