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
using System.Management.Automation;
using System.Net;
using System.Text;
using System.Web.Services.Protocols;
using System.Xml;
using SharePosh.SOAP.Copy;
using SharePosh.SOAP.Dws;
using SharePosh.SOAP.Lists;
using SharePosh.SOAP.Sites;
using SharePosh.SOAP.Versions;
using SharePosh.SOAP.Webs;

namespace SharePosh
{
    // Connector specialization for the communication with the SharePoint server via web services.
    class SOAPConnector : XmlConnector, IDisposable
    {
        public SOAPConnector(DriveInfo drive) : base(drive) {}

        // Direct retrieval of the XML representation of the SharePoint server objects as
        // requested by the ancestor class.

        protected override IEnumerable<XmlElement> QueryWebs(WebInfo web) {
            Log.Verbose("Querying webs of /{0}.", web.Path);
            var output = (XmlElement) GetService<Webs>(web.Path).GetWebCollection();
            return output.ChildNodes.OfType<XmlElement>();
        }

        protected override IEnumerable<XmlElement> QueryLists(WebInfo web) {
            Log.Verbose("Querying lists of /{0}.", web.Path);
            var output = (XmlElement) GetService<Lists>(web.Path).GetListCollection();
            return output.ChildNodes.OfType<XmlElement>();
        }

        protected override IEnumerable<XmlElement> QueryItems(ItemContainerInfo container) {
            Log.Verbose("Querying items of /{0}.", container.Path);
            var list = container.List;
            var input = new XmlDocument();
            var view = AddCamlFields(input, list);
            var folder = container == list ? null : container.WebRelativePath;
            var options = AddCamlOptions(input, folder);
            var output = (XmlElement) GetService<Lists>(list.Web.Path).GetListItems(
                list.StringID, null, null, view, null, options, null);
            // Pick all /listitems/data/row. Avoiding XPath with namespaces.
            return output.ChildNodes.OfType<XmlElement>().First().ChildNodes.OfType<XmlElement>();
        }

        protected override XmlElement QueryWeb(string path) {
            Log.Verbose("Querying web at /{0}.", path);
            var fullPath = PathUtility.JoinPath(Drive.WebUrl, path);
            return (XmlElement) GetService<Webs>(path).GetWeb(fullPath);
        }

        protected override XmlElement QueryItem(ListInfo list, string path) {
            Log.Verbose("Querying item at /{0}/{1}.", list.Path, path);
            string name;
            string parent = PathUtility.GetParentPath(path, out name);
            var input = new XmlDocument();
            var query = AddCamlName(input, name);
            var view = AddCamlFields(input, list);
            var folder = string.IsNullOrEmpty(parent) ? null :
                PathUtility.JoinPath(list.WebRelativePath, parent);
            var options = AddCamlOptions(input, folder);
            var output = (XmlElement) GetService<Lists>(list.Web.Path).GetListItems(
                list.StringID, null, query, view, null, options, null);
            // Pick /listitems/data/row[1]. Avoiding XPath with namespaces.
            var item = output.ChildNodes.OfType<XmlElement>().First().
                ChildNodes.OfType<XmlElement>().FirstOrDefault();
            if (item == null)
                throw new ApplicationException("No item found.");
            return item;
        }

        // Internal methods helping the direct SharePoint object retrieval above.

        XmlNode AddCamlName(XmlDocument input, string name) {
            var query = input.CreateElement("Query");
            query.InnerXml = string.Format(@"<Where>
  <Eq><FieldRef Name=""FileLeafRef"" /><Value Type=""Url"">{0}</Value></Eq>
</Where>", XmlUtility.EscapeXmlValue(name));
            return query;
        }

        XmlNode AddCamlFields(XmlDocument input, ListInfo list) {
            if (list.Fields == null)
                list.Fields = QueryFields(list).ToList();
            if (!list.Fields.Any())
                return null;
            var fields = new StringBuilder();
            foreach (var field in list.Fields)
                fields.AppendFormat(@"<FieldRef Name=""{0}"" />",
                            XmlUtility.EscapeXmlValue(field.Name));
            var view = input.CreateElement("ViewFields");
            view.InnerXml = fields.ToString();
            return view;
        }

        XmlNode AddCamlOptions(XmlDocument input, string folder = null) {
            var xml = new StringBuilder(@"<IncludeMandatoryColumns>TRUE</IncludeMandatoryColumns>
<DateInUtc>TRUE</DateInUtc>");
            var options = input.CreateElement("QueryOptions");
            if (!string.IsNullOrEmpty(folder))
                xml.AppendFormat(@"<Folder>{0}</Folder>", XmlUtility.EscapeXmlValue(folder));
            options.InnerXml = xml.ToString();
            return options;
        }

        IEnumerable<FieldInfo> QueryFields(ListInfo list) {
            Log.Verbose("Querying fields of /{0}.", list.Path);
            var output = (XmlElement) GetService<Lists>(list.Web.Path).GetList(list.StringID);
            return GetListFields(output);
        }

        // Extracting of SharePoint object-specific properties from the XML object representation.
        // These methods expect the XML content returned by other methods of this class.

        protected override Guid GetWebID(XmlElement source) {
            // Web ID is not returned from Webs.GetWebCollection(); only from Webs.GetWeb(). In
            // case of the former call the web ID stays empty as long as its child lists are not
            // queried which contain their owning web ID in their information. Usually it doesn't
            // take long because PowerShell requests more information about the container web
            // almost immediately.
            var id = source.GetAttribute("Id");
            return string.IsNullOrEmpty(id) ? Guid.Empty : new Guid(id);
        }

        protected override string GetWebName(XmlElement source) {
            return source.GetAttribute("Url").Substring(Drive.WebUrl.Length).Trim('/');
        }

        protected override string GetWebTitle(XmlElement source) {
            return source.GetAttribute("Title") ?? "";
        }

        protected override Guid GetListID(XmlElement source) {
            return new Guid(source.GetAttribute("ID"));
        }

        protected override Guid GetListWebID(XmlElement source) {
            // Web ID is not returned from Webs.GetWebCollection(). As long as its child lists are
            // not queried the web ID stays empty. Usually it is not long because PowerShell
            // requests more information about the container web almost immediately. Child list
            // information contains their owning web ID.
            var id = source.GetAttribute("WebId");
            return string.IsNullOrEmpty(id) ? Guid.Empty : new Guid(id);
        }

        protected override string GetListName(XmlElement source) {
            // The server-relative URL of the list can be used later if the name is cut from a
            // list child URL and it is always available in the web servie results.
            var webUrl = source.GetAttribute("WebFullUrl");
            if (!string.IsNullOrEmpty(webUrl))
                webUrl = webUrl.TrimStart('/');
            // The root folder URL is not empty in the response of Lists.GetList() but it is empty
            // in the response of Lists.GetListCollection(). It is a server-relative URL.
            var name = source.GetAttribute("RootFolder");
            if (!string.IsNullOrEmpty(name))
                return name.TrimStart('/').Substring(webUrl.Length).TrimStart('/');
            // URL of the default view is always available but it is not clear which part belongs
            // to the web-relative path of the list and the list-relative path of the page. For
            // example: both /Lists/Announcements/Default.aspx and /Documents/Forms/AllItems.aspx
            // are valid web-relative default view URLs but the web-relative URLs of their lists
            // are /Lists/Announcements and /Documents respectively. There is no rule to pick the
            // right part of path considering the path in general. As far as I learnt there are
            // only two folders on te web level that host lists: Lists and _catalogs. Also, if the
            // default view URL contains /Forms/AllItems.aspx than the view page is placed in the
            // folder Forms and the rest below the folder is the web-relative list URL. Otherwise
            // just the root part of the URL is taken.
            name = source.GetAttribute("DefaultViewUrl");
            if (!string.IsNullOrEmpty(name)) {
                name = name.TrimStart('/').Substring(webUrl.Length).TrimStart('/');
                int index;
                if (name.StartsWithCI("Lists/"))
                    index = 6;
                else if (name.StartsWithCI("_catalogs/"))
                    index = 10;
                else {
                    index = name.IndexOfCI("/Forms/");
                    if (index > 0)
                        return name.Substring(0, index);
                    index = 0;
                }
                return name.Substring(0, name.IndexOf('/', index));
            }
            // Sometimes the title is used for the URL too - especially for lists created by the
            // end-user - if we have no other clue let's try it as the last resort.
            return source.GetAttribute("Title");
        }

        protected override string GetListTitle(XmlElement source) {
            return source.GetAttribute("Title") ?? "";
        }

        protected override DateTime GetListCreated(XmlElement source) {
            return GetListDate(source, "Created");
        }

        protected override DateTime GetListLastModified(XmlElement source) {
            return GetListDate(source, "Modified");
        }

        protected override DateTime GetListLastDeleted(XmlElement source) {
            return GetListDate(source, "LastDeleted");
        }

        protected override bool HasListFields(XmlElement source) {
            return source.ChildNodes.OfType<XmlElement>().Any(
                entry => entry.LocalName == "Fields");
        }

        protected override IEnumerable<FieldInfo> GetListFields(XmlElement source) {
            // Extract the field list from /List/Fields. Avoiding XPath with namespaces.
            var fields = source.ChildNodes.OfType<XmlElement>().FirstOrDefault(
                entry => entry.LocalName == "Fields");
            return fields == null ? null : fields.ChildNodes.OfType<XmlElement>().Select(
                field => new FieldInfo {
                                Name = field.GetAttribute("Name"),
                                Title = field.GetAttribute("Title"),
                                Hidden = ValueUtility.GetBool(field.GetAttribute("Hidden")),
                                ReadOnly = ValueUtility.GetBool(field.GetAttribute("ReadOnly"))
                            });
        }

        protected override int GetListItemCount(XmlElement source) {
            var value = source.GetAttribute("ItemCount");
            return string.IsNullOrEmpty(value) ? 0 : ValueUtility.GetInt(value);
        }

        DateTime GetListDate(XmlElement source, string name) {
            // List Properties Created and Modified come in the format YYYYMMDD HH:MM:SS. Other
            // properties with a date value come in the format YYYY-MM-DD HH:MM:SS which is
            // acceptable for the DateTime.Parse() method. Let's ensure the latter format.
            var value = source.GetAttribute(name);
            if (!value.Contains('-'))
                value = value.Insert(6, "-").Insert(4, "-");
            return ValueUtility.GetDate(value);
        }

        protected override ItemType GetItemType(XmlElement source) {
            var type = source.GetAttribute("ows_FSObjType");
            if (!string.IsNullOrEmpty(type)) {
                if (type.EndsWith('1'))
                    return ItemType.Folder;
                // A file cannot be recognized from a common item by an explicit flag. Files are
                // usually stored in document libraries and common items in pure lists but this is
                // no general rule. I found that files have some extra system properties like
                // LinkFilename which is equal fo FileLeafRef and cannot be empty.
                if (type.EndsWith('0') &&
                        !string.IsNullOrEmpty(source.GetAttribute("ows_LinkFilename")))
                    return ItemType.File;
            }
            return ItemType.Common;
        }

        protected override int GetItemID(XmlElement source) {
            return ValueUtility.GetInt(source.GetAttribute("ows_ID"));
        }

        protected override Guid GetItemUniqueID(XmlElement source) {
            return new Guid(ValueUtility.GetLookupValue(source.GetAttribute("ows_UniqueId")));
        }

        protected override string GetItemName(XmlElement source) {
            // Looking for the FileLeafRef should be enough. Somehow I feel better having the
            // LinkFilename checked first...
            var name = source.GetAttribute("ows_LinkFilename");
            if (!string.IsNullOrEmpty(name))
                return name;
            return ValueUtility.GetLookupValue(source.GetAttribute("ows_FileLeafRef"));
        }

        protected override string GetItemTitle(XmlElement source) {
            var title = source.GetAttribute("ows_Title");
            if (!string.IsNullOrEmpty(title))
                return title;
            title = source.GetAttribute("ows_LinkTitle");
            if (!string.IsNullOrEmpty(title))
                return title;
            return source.GetAttribute("Title") ?? "";
        }

        protected override DateTime GetItemCreated(XmlElement source) {
            return GetItemDate(source, "ows_Created", "ows_Created_x0020_Date");
        }

        protected override DateTime GetItemLastModified(XmlElement source) {
            return GetItemDate(source, "ows_Modified", "ows_Last_x0020_Modified");
        }

        DateTime GetItemDate(XmlElement source, string rawName, string displayName) {
            var date = source.GetAttribute(rawName);
            if (!string.IsNullOrEmpty(date))
                return ValueUtility.GetDate(date);
            date = source.GetAttribute(displayName);
            if (!string.IsNullOrEmpty(date))
                return ValueUtility.GetDate(ValueUtility.GetLookupValue(date));
            return DateTime.MinValue;
        }

        protected override int GetFolderChildCount(XmlElement source) {
            var value = source.GetAttribute("ows_ItemChildCount");
            return string.IsNullOrEmpty(value) ? 0 :
                ValueUtility.GetInt(ValueUtility.GetLookupValue(value));
        }

        protected override int GetFileSize(XmlElement source) {
            var value = source.GetAttribute("ows_FileSizeDisplay");
            int result;
            if (!string.IsNullOrEmpty(value) && ValueUtility.TryGetInt(value, out result))
                return result;
            value = source.GetAttribute("ows_File_x0020_Size");
            return string.IsNullOrEmpty(value) ? 0 :
                ValueUtility.GetInt(ValueUtility.GetLookupValue(value));
        }

        // Implementation of the ModifyingConnector interface support which performs modifications
        // by calling SharePoint web services.

        protected override void RemoveWebDirectly(WebInfo web) {
            // A web cannot be deleted if it contains any child webs. All child webs have to be
            // deleted before the parent web. Let's get all webs on the entire site collection
            // and filter out all ancestor webs which shouldn't be deleted.
            var webUrl = PathUtility.JoinPath(Drive.WebUrl, web.Path);
            var webs = GetAllWebs(web).Where(item => item.StartsWithCI(webUrl + "/"));
            webs = webs.Concat(new[] { webUrl });
            foreach (var child in webs.OrderByDescending(item => item.Length)) {
                var childPath = child.Substring(webUrl.Length).TrimStart('/');
                childPath = PathUtility.JoinPath(web.Path, childPath);
                // Deleting a web site can be done by Sites.DeleteWeb() but this method is not
                // available in SharePoint 2007. Deleting  document workspace - which is actually
                // a web - is a nice alternative working on any SharePoint server version.
                Log.Verbose("Removing web at /{0}.", childPath);
                GetService<Dws>(childPath).DeleteDws();
            }
        }

        IEnumerable<string> GetAllWebs(WebInfo web) {
            Log.Verbose("Listing all webs below /{0}.", web.Path);
            var output = (XmlElement) GetService<Webs>(web.Path).GetAllSubWebCollection();
            return output.ChildNodes.OfType<XmlElement>().Select(
                        item => item.GetAttribute("Url")).ToList();
        }

        protected override void RemoveListDirectly(ListInfo list) {
            Log.Verbose("Removing list at /{0}.", list.Path);
            GetService<Lists>(list.Web.Path).DeleteList(list.StringID);
        }

        protected override void RemoveItemDirectly(ItemInfo item) {
            Log.Verbose("Removing item at /{0}.", item.Path);
            var input = new XmlDocument();
            var updates = AddCamlDelete(input, item);
            var output = (XmlElement) GetService<Lists>(item.List.Web.Path).UpdateListItems(
                item.List.StringID, updates);
            CheckUpdateResult(output);
        }

        XmlNode AddCamlDelete(XmlDocument input, ItemInfo item) {
            var updates = input.CreateElement("Batch");
            updates.InnerXml = string.Format(@"<Method ID=""1"" Cmd=""Delete"">
  <Field Name=""ID"">{0}</Field><Field Name=""FileRef"">{1}</Field>
</Method>", item.ID, XmlUtility.EscapeXmlValue(item.ServerRelativePath));
            return updates;
        }

        XmlElement GetUpdateResult(XmlElement output) {
            CheckUpdateResult(output);
            // Extract the item properties from /list/data/row[1]. Avoiding XPath with namespaces.
            var result = output.ChildNodes.OfType<XmlElement>().First();
            return result.ChildNodes.OfType<XmlElement>().First(node => node.LocalName == "row");
        }

        void CheckUpdateResult(XmlElement output) {
            // Check /Update/Method/ErrorCode and /Update/Method/ErrorText to detect an error.
            var result = output.ChildNodes.OfType<XmlElement>().First().
                    ChildNodes.OfType<XmlElement>();
            var code = result.First(node => node.LocalName == "ErrorCode").InnerText.Trim();
            if (code != "0x00000000") {
                var message = result.First(node => node.LocalName == "ErrorText").InnerText.Trim();
                if (!message.Contains(code)) {
                    var builder = new StringBuilder(message);
                    if (!builder.EndsWith('.'))
                        builder.Append('.');
                    message = builder.Append(" (").Append(code).Append(")").ToString();
                }
                throw new ApplicationException(message);
            }
        }

        protected override XmlElement RawRenameItem(ItemInfo item, string newName) {
            string extension;
            newName = PathUtility.GetNameWithoutExtension(newName, out extension);
            Log.Verbose("Renaming item at /{0} to {1}.", item.Path, newName);
            var input = new XmlDocument();
            var updates = AddCamlRename(input, item, newName);
            var output = (XmlElement) GetService<Lists>(item.List.Web.Path).UpdateListItems(
                item.List.StringID, updates);
            return GetUpdateResult(output);
        }

        XmlNode AddCamlRename(XmlDocument input, ItemInfo item, string newName) {
            var updates = input.CreateElement("Batch");
            var fields = new StringBuilder();
            fields.AppendFormat(@"<Method ID=""1"" Cmd=""Update"">
  <Field Name=""ID"">{0}</Field><Field Name=""FileRef"">{1}</Field>
  <Field Name=""BaseName"">{2}</Field>", item.ID, XmlUtility.EscapeXmlValue(item.ServerRelativePath),
                                         XmlUtility.EscapeXmlValue(newName));
            if (!string.IsNullOrEmpty(item.Title))
                fields.AppendFormat(@"<Field Name=""Title"">{0}</Field>",
                    XmlUtility.EscapeXmlValue(newName));
            fields.Append(@"</Method>");
            updates.InnerXml = fields.ToString();
            return updates;
        }

        protected override XmlElement RawCopyItem(ItemInfo item, ItemContainerInfo target,
                                                  bool recurse, string newName) {
            if (string.IsNullOrEmpty(newName))
                newName = item.Name;
            Log.Verbose("Copying item at /{0} to /{1} as {2}.", item.Path, target.Path, newName);
            CopyResult[] results;
            var originalUrl = PathUtility.JoinPath(Drive.WebUrl, item.Path);
            var copyUrl = PathUtility.JoinPath(Drive.WebUrl, target.Path, newName);
            GetService<Copy>(item.List.Web.Path).CopyIntoItemsLocal(
                originalUrl, new[] { copyUrl }, out results);
            return GetCopiedItem(target, results);
        }

        XmlElement GetCopiedItem(ItemContainerInfo container, CopyResult[] results) {
            var result = results[0];
            if (result.ErrorCode != CopyErrorCode.Success)
                throw new ApplicationException(result.ErrorMessage +
                                                " (" + result.ErrorCode + ")");
            var name = PathUtility.GetChildName(result.DestinationUrl);
            var relativePath = PathUtility.JoinPath(container.ListRelativePath, name);
            return QueryItem(container.List, relativePath);
        }

        protected override XmlElement RawMoveItem(ItemInfo item, ItemContainerInfo target) {
            // If the item is moved to other list it must be copied and the original deleted.
            if (item.List.Path.EqualsCI(target.List.Path)) {
                Log.Verbose("Moving item at /{0} to /{1}.", item.Path, target.Path);
                var input = new XmlDocument();
                var updates = AddCamlMove(input, item, target);
                var output = (XmlElement) GetService<Lists>(item.List.Web.Path).UpdateListItems(
                    item.List.StringID, updates);
                CheckUpdateResult(output);
                var relativePath = PathUtility.JoinPath(target.ListRelativePath, item.Name);
                return QueryItem(target.List, relativePath);
            }
            var copy = RawCopyItem(item, target, true, null);
            RemoveItem(item);
            return copy;
        }

        XmlNode AddCamlMove(XmlDocument input, ItemInfo item, ItemContainerInfo target) {
            var updates = input.CreateElement("Batch");
            var fields = new StringBuilder();
            var newUrl = PathUtility.JoinPath(target.ServerRelativePath, item.Name);
            fields.AppendFormat(@"<Method ID=""1"" Cmd=""Move"">
  <Field Name=""ID"">{0}</Field><Field Name=""FileRef"">{1}</Field>
  <Field Name=""MoveNewUrl"">{2}</Field>", item.ID,
                XmlUtility.EscapeXmlValue(item.ServerRelativePath), XmlUtility.EscapeXmlValue(newUrl));
            fields.Append(@"</Method>");
            updates.InnerXml = fields.ToString();
            return updates;
        }

        protected override XmlElement RawAddWeb(WebInfo web, WebCreationParameters parameters) {
            var name = web.Name;
            if (string.IsNullOrEmpty(name))
                name = parameters.Title;
            var title = web.Title;
            if (string.IsNullOrEmpty(title))
                title = name;
            Log.Verbose("Adding the web {0} to /{1}.", name, web.Path);
            var result = GetService<Sites>(web.Path).CreateWeb(
                name, title, parameters.Description, parameters.Template,
                parameters.Language, parameters.Language > 0,
                parameters.Locale, parameters.Locale > 0,
                parameters.CollationLocale, parameters.CollationLocale > 0,
                parameters.UniquePermissions.GetValueOrDefault(),
                parameters.UniquePermissions.HasValue,
                parameters.Anonymous.GetValueOrDefault(), parameters.Anonymous.HasValue,
                parameters.Presence.GetValueOrDefault(), parameters.Presence.HasValue);
            return QueryWeb(PathUtility.JoinPath(web.Path, parameters.Name));
        }

        protected override XmlElement RawAddList(WebInfo web, ListCreationParameters parameters) {
            Log.Verbose("Adding the list {0} to /{1}.", parameters.Name, web.Path);
            return (XmlElement) GetService<Lists>(web.Path).AddList(
                parameters.Name, parameters.Description, parameters.Template);
        }

        protected override XmlElement RawAddFolder(ItemContainerInfo container, string name) {
            Log.Verbose("Adding the folder {0} to /{1}.", name, container.Path);
            var input = new XmlDocument();
            var updates = AddCamlNewFolder(input, container, name);
            var output = (XmlElement) GetService<Lists>(container.List.Web.Path).UpdateListItems(
                container.List.StringID, updates);
            return GetUpdateResult(output);
        }

        XmlNode AddCamlNewFolder(XmlDocument input, ItemContainerInfo container, string name) {
            var updates = input.CreateElement("Batch");
            updates.SetAttribute("DateInUtc", "TRUE");
            updates.SetAttribute("RootFolder", "/" + container.WebRelativePath);
            updates.InnerXml = string.Format(@"<Method ID=""1"" Cmd=""New"">
  <Field Name=""FSObjType"">1</Field>
  <Field Name=""BaseName"">{0}</Field><Field Name=""Title"">{0}</Field>
</Method>", XmlUtility.EscapeXmlValue(name));
            return updates;
        }

        protected override XmlElement RawAddItem(ItemContainerInfo container, string name) {
            Log.Verbose("Adding the item {0} to /{1}.", name, container.Path);
            var input = new XmlDocument();
            var updates = AddCamlNewItem(input, container, name);
            var output = (XmlElement) GetService<Lists>(container.List.Web.Path).UpdateListItems(
                container.List.StringID, updates);
            return GetUpdateResult(output);
        }

        XmlNode AddCamlNewItem(XmlDocument input, ItemContainerInfo container, string name) {
            var updates = input.CreateElement("Batch");
            updates.SetAttribute("DateInUtc", "TRUE");
            updates.SetAttribute("RootFolder", "/" + container.WebRelativePath);
            updates.InnerXml = string.Format(@"<Method ID=""1"" Cmd=""New"">
  <Field Name=""BaseName"">{0}</Field><Field Name=""Title"">{0}</Field>
</Method>", XmlUtility.EscapeXmlValue(name));
            return updates;
        }

        // Implementation of the rest of the ContentConnector interface which needed no cache
        // handling and no XML information conversion in the parent class.

        public override Stream OpenFile(FileInfo file, string version) {
            if (file == null)
                throw new ArgumentNullException("file");
            string url = null;
            if (!string.IsNullOrEmpty(version)) {
                Log.Verbose("Querying versions of /{0}.", file.Path);
                var output = (XmlElement) GetService<Versions>(file.List.Web.Path).GetVersions(
                    file.WebRelativePath);
                var source = output.SelectElement(string.Format(
                    "results/result[@version={0} or @version={1}]",
                        XmlUtility.FormatXPathLiteral(version),
                        XmlUtility.FormatXPathLiteral("@" + version)));
                if (source == null)
                    throw new ApplicationException("Version not found.");
                url = source.GetAttribute("url");
            } else {
                url = PathUtility.JoinPath(Drive.WebUrl, file.Path);
            }
            Log.Verbose("Opening file at /{0}.", file.Path);
            return GetClient().OpenRead(url);
        }

        public override void SaveFile(FileInfo file, Stream content) {
            throw new NotImplementedException();
        }

        // Implementation of the ContentConnector interface support from the parent class.

        protected override XmlElement UploadFile(ContentContainerInfo container, string name,
                                                 Stream content) {
            Log.Verbose("Adding the file {0} to /{1}.", name, container.Path);
            var url = PathUtility.JoinPath(Drive.WebUrl, container.Path, name);
            using (var client = GetClient()) {
                var response = client.UploadData(url, "PUT", content.ReadBytes()); 
            }
            var relativePath = PathUtility.JoinPath(container.ListRelativePath, name);
            return QueryItem(container.List, relativePath);
            //CopyResult[] results;
            //GetService<Copy>(container.List.Web.Path).CopyIntoItems(
            //    name, new[] { url }, null, content.ReadBytes(), out results);
            //return GetCopiedItem(container, results);
        }

        // Helpers getting network communication objects.

        protected virtual WebClient GetClient() {
            var client = new WebClient();
            if (Drive.Credential != null)
                client.Credentials = Drive.Credential.GetCredentials();
            else
                client.UseDefaultCredentials = true;
            return client;
        }

        protected T GetService<T>(string url) where T : SoapHttpClientProtocol, new() {
            // Connecting and authenticating to a service takes some time. Better to reuse the
            // service for future calls. Service URL must be the same which means that the web
            // URL and the service type must be the same.
            var name = typeof(T).Name;
            var key = name + ":" + url;
            SoapHttpClientProtocol service;
            if (Services.TryGetValue(key, out service))
                return (T) service;
            Log.Verbose("Initializing {0} service for /{1}.", name, url);
            url = PathUtility.JoinPath(Drive.WebUrl, url);
            service = CreateService<T>(url);
            Services.Add(key, service);
            return (T) service;
        }

        protected virtual T CreateService<T>(string url) where T : SoapHttpClientProtocol, new() {
            return GetService<T>(url, Drive.Credential, Drive.Timeout);
        }

        internal static T GetService<T>(string url, PSCredential credential, int timeout)
                                where T : SoapHttpClientProtocol, new() {
            var name = typeof(T).Name;
            var service = new T();
            service.Url = PathUtility.JoinPath(url, "_vti_bin", name + ".asmx");
            if (credential != null)
                service.Credentials = credential.GetCredentials();
            else
                service.UseDefaultCredentials = true;
            if (timeout > 0)
                service.Timeout = timeout;
            return (T) service;
        }

        Dictionary<string, SoapHttpClientProtocol> Services =
            new Dictionary<string, SoapHttpClientProtocol>(
                ConfigurableComparer<string>.CaseInsensitive);

        void IDisposable.Dispose() {
            foreach (var service in Services.Values)
                service.Dispose();
            Services.Clear();
            GC.SuppressFinalize(this);
        }
    }
}
