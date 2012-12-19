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
using System.Reflection;
using System.Xml;

namespace SharePosh
{
    // Connector using an XML document in memory to simulate content of a SharePoint web site.
    // The initial sample content is loaded from the assembly resources.
    class TestConnector : XmlConnector, TestingConnector
    {
        public TestConnector(DriveInfo drive) : base(drive) {}

        // Implementation of the direct object information retrieval which will be recognized in
        // the GetXxx methods below to extract concrete object properties from.

        protected override IEnumerable<XmlElement> QueryWebs(WebInfo web) {
            return GetWebXml(web).SelectNodes("Web").OfType<XmlElement>();
        }

        protected override IEnumerable<XmlElement> QueryLists(WebInfo web) {
            return GetWebXml(web).SelectNodes("List").OfType<XmlElement>();
        }

        protected override IEnumerable<XmlElement> QueryItems(ItemContainerInfo container) {
            // Item container can contain only items - Item elements - no need for XPath here.
            return GetItemContainerXml(container).ChildNodes.OfType<XmlElement>();
        }

        protected override XmlElement QueryWeb(string path) {
            var source = GetSite();
            if (path.Any())
                foreach (var name in PathUtility.SplitPath(path)) {
                    source = source.SelectElement(string.Format("Web[@Name={0}]",
                        XmlUtility.FormatXPathLiteral(name)));
                    if (source == null)
                        throw new ApplicationException("Web not found.");
                }
            return source;
        }

        protected override XmlElement QueryItem(ListInfo list, string path) {
            var source = GetListXml(list);
            foreach (var name in PathUtility.SplitPath(path)) {
                source = source.SelectElement(string.Format("Item[@Name={0}]",
                    XmlUtility.FormatXPathLiteral(name)));
                if (source == null)
                    throw new ApplicationException("Item not found.");
            }
            return source;
        }

        XmlElement GetWebXml(WebInfo web) {
            return QueryWeb(web.Path);
        }

        XmlElement GetItemContainerXml(ItemContainerInfo container) {
            return container == container.List ? GetListXml((ListInfo) container) :
                GetItemXml((FolderInfo) container);
        }

        XmlElement GetListXml(ListInfo list) {
            var source = GetWebXml(list.Web);
            source = source.SelectElement(string.Format("List[@Name={0}]",
                XmlUtility.FormatXPathLiteral(list.WebRelativePath)));
            if (source == null)
                throw new ApplicationException("List not found.");
            return source;
        }

        XmlElement GetItemXml(ItemInfo item) {
            return QueryItem(item.List, item.ListRelativePath);
        }

        // Implementation of the property extraction from the raw XML information returned by the
        // QueryXxx methods above.

        protected override Guid GetWebID(XmlElement source) {
            return new Guid(source.GetAttribute("ID"));
        }

        protected override string GetWebName(XmlElement source) {
            return source.GetAttribute("Name");
        }

        protected override string GetWebTitle(XmlElement source) {
            return source.GetAttribute("Title") ?? "";
        }

        protected override Guid GetListID(XmlElement source) {
            return new Guid(source.GetAttribute("ID"));
        }

        protected override Guid GetListWebID(XmlElement source) {
            // The web ID is present in the Web element. This provider needs no refresh of the
            // web ID from the list information when it first becomes available.
            throw new NotSupportedException("This operation is not supported.");
        }

        protected override string GetListName(XmlElement source) {
            return source.GetAttribute("Name");
        }

        protected override string GetListTitle(XmlElement source) {
            return source.GetAttribute("Title") ?? "";
        }

        protected override DateTime GetListCreated(XmlElement source) {
            return ValueUtility.GetDate(source.GetAttribute("Created"));
        }

        protected override DateTime GetListLastModified(XmlElement source) {
            // Last modification time of a list is computed as the most recent last modification
            // time of any of its child items. More recent of the times of creation and last
            // deletion are used as starting values.
            var date = ValueUtility.Max(GetListCreated(source), GetListLastDeleted(source));
            var children = source.ChildNodes.OfType<XmlElement>();
            if (!children.Any())
                return date;
            return ValueUtility.Max(date, children.Max(item => GetItemLastModified(item)));
        }

        protected override DateTime GetListLastDeleted(XmlElement source) {
            // Last deletion time inside a list has to be stored as an attribute otherwise an
            // invalid value is returned.
            var date = source.GetAttribute("Deleted");
            return string.IsNullOrEmpty(date) ? DateTime.MinValue : ValueUtility.GetDate(date);
        }

        protected override int GetListItemCount(XmlElement source) {
            return source.SelectNodes("//Item").OfType<XmlElement>().Count();
        }

        protected override bool HasListFields(XmlElement source) {
            return false;
        }

        protected override IEnumerable<FieldInfo> GetListFields(XmlElement source) {
            // This provider returns all available item properties and needs no field list in the
            // query. Neither are there any custom fields settable by the Set-ItemProperty cmdlet.
            throw new NotSupportedException("This operation is not supported.");
        }

        protected override ItemType GetItemType(XmlElement source) {
            switch (source.GetAttribute("Type")) {
            case "File":
                return ItemType.File;
            case "Folder":
                return ItemType.Folder;
            default:
                return ItemType.Common;
            }
        }

        protected override int GetItemID(XmlElement source) {
            return ValueUtility.GetInt(source.GetAttribute("ID"));
        }

        protected override Guid GetItemUniqueID(XmlElement source) {
            return new Guid(source.GetAttribute("UniqueID"));
        }

        protected override string GetItemName(XmlElement source) {
            return source.GetAttribute("Name");
        }

        protected override string GetItemTitle(XmlElement source) {
            return source.GetAttribute("Title") ?? "";
        }

        protected override DateTime GetItemCreated(XmlElement source) {
            return ValueUtility.GetDate(source.GetAttribute("Created"));
        }

        protected override DateTime GetItemLastModified(XmlElement source) {
            // Last modification time of a folder is computed as the most recent last modification
            // time of any of its children walked recursively. Starting value is read from its
            // attribute which can be set by a direct operation like renaming. Last modification
            // of a file or of a common item has to be stored as an attribute of the item element
            // otherwise the creation date is returned - the item has not been modified yet.
            var modified = source.GetAttribute("Modified");
            var date = string.IsNullOrEmpty(modified) ? GetItemCreated(source) :
                                                        ValueUtility.GetDate(modified);
            if (GetItemType(source) == ItemType.Folder) {
                var children = source.ChildNodes.OfType<XmlElement>();
                if (children.Any()) {
                    var maximum = children.Max(item => GetItemLastModified(item));
                    if (maximum > date)
                        date = maximum;
                }
            }
            return date;
        }

        protected override int GetFolderChildCount(XmlElement source) {
            return source.ChildNodes.OfType<XmlElement>().Count();
        }

        protected override int GetFileSize(XmlElement source) {
            // Size of a file is computed as the byte length of its last version.
            var version = source.ChildNodes.OfType<XmlElement>().LastOrDefault();
            if (version == null)
                return 0;
            using (var content = OpenContent(version))
                return (int) content.Length;
        }

        // Implementation of the ModifyingConnector interface support which performs modifications
        // in the in-memory XML document initialized from assembly resources.

        protected override void RemoveWebDirectly(WebInfo web) {
            var source = GetWebXml(web);
            var parent = (XmlElement) source.ParentNode;
            source.Remove();
            SaveSite(parent);
        }

        protected override void RemoveListDirectly(ListInfo list) {
            var source = GetListXml(list);
            var parent = (XmlElement) source.ParentNode;
            source.Remove();
            SaveSite(parent);
        }

        protected override void RemoveItemDirectly(ItemInfo item) {
            // Item removal modifies the parent element and stores the deletion time to the parent
            // list; the parent list last modification time will be touched too but because it is
            // computed from the most recent modification time of any child item we don't set it.
            var source = GetItemXml(item);
            var date = DateForNow;
            var parent = (XmlElement) source.ParentNode;
            parent.SetAttribute("Modified", date);
            var list = source.SelectElement("ancestor::List");
            list.SetAttribute("Deleted", date);
            source.Remove();
            SaveSite(parent);
        }

        protected override XmlElement RawRenameItem(ItemInfo item, string newName) {
            var source = GetItemXml(item);
            RenameItemXml(source, newName);
            TouchItemXml(source);
            SaveSite(source);
            return source;
        }

        protected override XmlElement RawCopyItem(ItemInfo item, ItemContainerInfo target,
                                                  bool recurse, string newName) {
            // Common items have no children and files can have versions as children; we should
            // always enable deep cloning for those terminal objects not to corrupt them.
            var deeply = recurse | !(item is FolderInfo);
            var source = (XmlElement) GetItemXml(item).CloneNode(deeply);
            var lastID = GetLastItemID(item.List);
            InitializeItemClones(source, ref lastID);
            PlaceItemXml(source, target, newName);
            SaveSite(source);
            return source;
        }

        protected override XmlElement RawMoveItem(ItemInfo item, ItemContainerInfo container) {
            // Item move modifies both old and new parent elements but not the moved item itself.
            // the parent is touched after the item is placed to the new container because that
            // operation can fail if an item of the same name has been already there. If the item
            // is moved to other list it needs a new ID unique in the new list.
            var source = GetItemXml(item);
            var parent = (XmlElement) source.ParentNode;
            source.Remove();
            var target = GetItemContainerXml(container);
            PlaceItemXml(source, target);
            if (!item.List.Path.EqualsCI(container.List.Path)) {
                var lastID = GetLastItemID(container.List);
                source.SetAttribute("ID", (++lastID).ToStringI());
            }
            TouchItemXml(parent);
            TouchItemXml(target);
            SaveSite(target);
            return source;
        }

        protected override XmlElement RawAddWeb(WebInfo web, WebCreationParameters parameters) {
            var target = GetWebXml(web);
            if (HasWebXml(target, parameters.Name))
                throw new ApplicationException("Web with the same name found.");
            if (HasWebFolderXml(target, parameters.Name))
                throw new ApplicationException("WebFolder with the same name found.");
            if (HasListXml(target, parameters.Name))
                throw new ApplicationException("List with the same name found.");
            var source = target.OwnerDocument.CreateElement("Web");
            source.SetAttribute("ID", Guid.NewGuid().ToString("D"));
            source.SetAttribute("Name", parameters.Name);
            source.SetAttribute("Title", parameters.Title);
            if (string.IsNullOrEmpty(parameters.Description))
                source.SetAttribute("Description", parameters.Description);
            source.SetAttribute("Template", parameters.Template);
            if (parameters.Language > 0)
                source.SetAttribute("Language", parameters.Language.ToStringI());
            if (parameters.Locale > 0)
                source.SetAttribute("Locale", parameters.Locale.ToStringI());
            if (parameters.CollationLocale > 0)
                source.SetAttribute("CollationLocale", parameters.CollationLocale.ToStringI());
            if (parameters.UniquePermissions.HasValue)
                source.SetAttribute("UniquePermissions",
                                                parameters.UniquePermissions.Value.ToStringI());
            if (parameters.Anonymous.HasValue)
                source.SetAttribute("Anonymous", parameters.Anonymous.Value.ToStringI());
            if (parameters.Presence.HasValue)
                source.SetAttribute("Presence", parameters.Presence.Value.ToStringI());
            source.SetAttribute("Created", DateForNow);
            target.AppendChild(source);
            SaveSite(target);
            return source;
        }

        protected override XmlElement RawAddList(WebInfo web, ListCreationParameters parameters) {
            var target = GetWebXml(web);
            if (HasWebXml(target, parameters.Name))
                throw new ApplicationException("Web with the same name found.");
            if (HasWebFolderXml(target, parameters.Name))
                throw new ApplicationException("WebFolder with the same name found.");
            if (HasListXml(target, parameters.Name))
                throw new ApplicationException("List with the same name found.");
            var source = target.OwnerDocument.CreateElement("List");
            source.SetAttribute("ID", Guid.NewGuid().ToString("D"));
            source.SetAttribute("Name", parameters.Name);
            if (string.IsNullOrEmpty(parameters.Description))
                source.SetAttribute("Description", parameters.Description);
            source.SetAttribute("Template", parameters.Template.ToStringI());
            source.SetAttribute("Created", DateForNow);
            target.AppendChild(source);
            SaveSite(target);
            return source;
        }

        protected override XmlElement RawAddFolder(ItemContainerInfo container, string name) {
            var item = RawAddItem(container, name, ItemType.Folder);
            SaveSite(item);
            return item;
        }

        protected override XmlElement RawAddItem(ItemContainerInfo container, string name) {
            var item = RawAddItem(container, name, ItemType.Common);
            SaveSite(item);
            return item;
        }

        XmlElement RawAddItem(ItemContainerInfo container, string name, ItemType type) {
            var target = GetItemContainerXml(container);
            if (HasItemXml(target, name))
                throw new ApplicationException("Item with the same name found.");
            var source = target.OwnerDocument.CreateElement("Item");
            var id = GetLastItemID(container.List) + 1;
            if (type != ItemType.Common)
                source.SetAttribute("Type", type.ToString());
            source.SetAttribute("ID", id.ToStringI());
            source.SetAttribute("UniqueID", Guid.NewGuid().ToString("D"));
            source.SetAttribute("Name", name);
            source.SetAttribute("Created", DateForNow);
            target.AppendChild(source);
            return source;
        }

        int GetLastItemID(ListInfo list) {
            var items = GetListXml(list).SelectNodes("//Item").OfType<XmlElement>();
            return items.Any() ? items.Max(item =>
                                            ValueUtility.GetInt(item.GetAttribute("ID"))) : 0;
        }

        void InitializeItemClones(XmlElement source, ref int lastID) {
            source.SetAttribute("ID", (++lastID).ToStringI());
            source.SetAttribute("UniqueID", Guid.NewGuid().ToString("D"));
            source.SetAttribute("Created", DateForNow);
            if (!string.IsNullOrEmpty(source.GetAttribute("Modified")))
                source.RemoveAttribute("Modified");
            var items = source.ChildNodes.OfType<XmlElement>().Where(
                    item => item.LocalName == "Item");
            foreach (var child in items)
                InitializeItemClones(child, ref lastID);
        }

        void RenameItemXml(XmlElement source, string newName) {
            source.SetAttribute("Name", newName);
            if (!string.IsNullOrEmpty(source.GetAttribute("Title")))
                source.SetAttribute("Title", newName);
        }

        void TouchItemXml(XmlElement source) {
            source.SetAttribute("Modified", DateForNow);
        }

        string DateForNow {
            get { return DateTime.UtcNow.ToString("s") + "Z"; }
        }

        void PlaceItemXml(XmlElement item, ItemContainerInfo container, string newName = null) {
            PlaceItemXml(item, GetItemContainerXml(container), newName);
        }

        void PlaceItemXml(XmlElement item, XmlElement target, string newName = null) {
            if (HasItemXml(target, newName ?? item.GetAttribute("Name")))
                throw new ApplicationException("Item with the same name found.");
            if (newName != null)
                RenameItemXml(item, newName);
            target.AppendChild(item);
        }

        bool HasWebXml(XmlElement source, string name) {
            return source.SelectNodes("Web").OfType<XmlElement>().Any(item =>
                                item.GetAttribute("Name").EqualsCI(name));
        }

        bool HasListXml(XmlElement source, string name) {
            return source.SelectNodes("List").OfType<XmlElement>().Any(list => {
                var listName = list.GetAttribute("Name");
                return listName.EqualsCI(name) || listName.EndsWithCI("/" + name);
            });
        }

        bool HasWebFolderXml(XmlElement source, string name) {
            return source.SelectNodes("List").OfType<XmlElement>().Any(list =>
                list.GetAttribute("Name").StartsWithCI(name + "/"));
        }

        bool HasItemXml(XmlElement source, string name) {
            // Item container can contain only items - Item elements - no need for XPath here.
            return source.ChildNodes.OfType<XmlElement>().Any(item =>
                item.GetAttribute("Name").EqualsCI(name));
        }

        // Implementation of the rest of the ContentConnector interface which needed no cache
        // handling and no XML information conversion in the parent class.

        public override Stream OpenFile(FileInfo file, string version) {
            if (file == null)
                throw new ArgumentNullException("file");
            return OpenContent(GetVersionXml(file, version));
        }

        public override void SaveFile(FileInfo file, Stream content) {
            if (file == null)
                throw new ArgumentNullException("file");
            if (content == null)
                throw new ArgumentNullException("content");
            var target = GetItemXml(file);
            SaveContent(target, content);
            TouchItemXml(target);
            SaveSite(target);
        }

        // Implementation of the ContentConnector interface support from the parent class.

        protected override XmlElement UploadFile(ContentContainerInfo container, string name,
                                                 Stream content) {
            var source = RawAddItem(container, name, ItemType.File);
            SaveContent(source, content);
            SaveSite(source);
            return source;
        }

        XmlElement GetVersionXml(FileInfo file, string version) {
            var source = GetItemXml(file);
            if (!string.IsNullOrEmpty(version)) {
                source = source.SelectElement(string.Format("Version[@Number={0}]",
                                                XmlUtility.FormatXPathLiteral(version)));
                if (source == null)
                    throw new ApplicationException("Version not found.");
            } else {
                // File can contain only versions - Version elements - no need for XPath here.
                source = source.ChildNodes.OfType<XmlElement>().LastOrDefault();
                if (source == null)
                    throw new ApplicationException("No version found.");
            }
            return source;
        }

        Stream OpenContent(XmlElement source) {
            var value = source.InnerText != null ? source.InnerText.Trim() : null;
            return string.IsNullOrEmpty(value) ? new MemoryStream(new byte[0]) :
                new MemoryStream(Convert.FromBase64String(value));
        }

        void SaveContent(XmlElement target, Stream content) {
            // The created element looks like <Version Number="1">...</Version>.
            var source = target.OwnerDocument.CreateElement("Version");
            source.SetAttribute("Number", GetNextVersionNumber(target));
            source.InnerText = Convert.ToBase64String(content.ReadBytes(),
                Base64FormattingOptions.InsertLineBreaks);
            target.AppendChild(source);
        }

        string GetNextVersionNumber(XmlElement target) {
            // File can contain only versions - Version elements - no need for XPath here.
            // If the last version is deleted its number is "recycled" later for a new version.
            // The XPath computed is max(Version/@Number).
            var last = target.ChildNodes.OfType<XmlElement>().LastOrDefault();
            if (last == null)
                return "1";
            var next = ValueUtility.GetInt(last.GetAttribute("Number")) + 1;
            return next.ToStringI();
        }

        // Loads the XML file simulating content of a SharePoint web site from the assembly
        // resource Resources/FakeSite.xml or from the specified WebUrl if it points to the
        // local file system. The file content is not kept cached in memory to allow content
        // changes outside this connector.
        XmlElement GetSite() {
            if (WebUrl.StartsWithCI("file:")) {
                Log.Verbose("Loading the file {0}.", WebUrl);
                var site = new XmlDocument();
                site.Load(WebUrl);
                return site.DocumentElement;
            }
            if (immutableSite == null) {
                Log.Verbose("Loading the content from resources.");
                var assembly = Assembly.GetExecutingAssembly();
                var site = new XmlDocument();
                using (var stream = assembly.GetManifestResourceStream(
                                        "SharePosh.Resources.FakeSite.xml"))
                    site.Load(stream);
                immutableSite = site.DocumentElement;
            }
            return immutableSite;
        }

        void SaveSite(XmlElement target) {
            if (WebUrl.StartsWithCI("file:")) {
                var file = WebUrl.Substring(5).TrimStart('/');
                Log.Verbose("Saving the file {0}.", file);
                target.OwnerDocument.Save(file);
            }
        }

        XmlElement immutableSite;
    }
}
