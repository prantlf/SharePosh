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
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Provider;

namespace SharePosh
{
    [CmdletProvider("SharePosh", ProviderCapabilities.Credentials |
        ProviderCapabilities.ShouldProcess),
     OutputType(new Type[] { typeof(ItemInfo), typeof(FileInfo), typeof(FolderInfo),
         typeof(ListInfo), typeof(WebInfo), typeof(string) }, ProviderCmdlet = "Get-ChildItem"),
     OutputType(new Type[] { typeof(ItemInfo), typeof(FileInfo), typeof(FolderInfo),
         typeof(ListInfo), typeof(WebInfo) }, ProviderCmdlet = "Get-Item")]
    public class DriveProvider : NavigationCmdletProvider, IContentCmdletProvider,
        IPropertyCmdletProvider
    {
        // New drive creation and disposal.

        // base.PSDriveInfo sometimes returns null. It is weird because when it behaves and
        // returns the object it is the same object returned by the NewDrive method. It causes
        // exceptions in ItemsExist which are swallowed by PowerShell.
        DriveInfo DriveInfo {
            get { return (DriveInfo) base.PSDriveInfo; }
        }

        protected override PSDriveInfo NewDrive(PSDriveInfo drive) {
            if (drive == null)
                throw new ArgumentNullException("drive");
            return new DriveInfo(drive, (NewDriveParameters) DynamicParameters);
        }

        protected override object NewDriveDynamicParameters() {
            return new NewDriveParameters();
        }

        protected override PSDriveInfo RemoveDrive(PSDriveInfo drive) {
            var disposable = drive as IDisposable;
            if (disposable != null)
                disposable.Dispose();
            return base.RemoveDrive(drive);
        }

        protected override ProviderInfo Start(ProviderInfo providerInfo) {
            // I didn't find better place to set the default description; if it is not set
            // by the drive creator to some value for its purposes it would be empty.
            if (string.IsNullOrEmpty(providerInfo.Description))
                providerInfo.Description = "Provides an access to SharePoint farms " +
                    "to browse their hierarchical content like the file system.";
            return providerInfo;
        }

        // Item manipulation.

        protected override void ClearItem(string path) {
            throw new NotSupportedException("This operation is not supported.");
        }

        protected override void GetItem(string path) {
            EnsureLog();
            WriteItemObject(GetObject(path));
        }

        protected override void CopyItem(string path, string copyPath, bool recurse) {
            EnsureLog();
            var source = GetObject(path);
            var manipulable = source as ManipulableInfo;
            if (manipulable == null)
                throw new ApplicationException("Copying is not supported.");
            var target = GetObject(copyPath);
            var container = target as ContainerInfo;
            if (container == null)
                throw new ApplicationException("Target is not a container.");
            container.CheckCanBeChild(source);
            var subject = string.Format("\"{0}\" to \"{1}\"", source.Path, target.Path);
            if (ShouldProcess(subject, "Copy Item")) {
                var parameters = (CopyItemParameters) DynamicParameters;
                WriteItemObject(manipulable.Copy(container, recurse, parameters.NewName));
            }
        }

        protected override object CopyItemDynamicParameters(string path, string destination,
                                                            bool recurse) {
            return new CopyItemParameters();
        }

        protected override void MoveItem(string path, string destination) {
            EnsureLog();
            var source = GetObject(path);
            var manipulable = source as ManipulableInfo;
            if (manipulable == null)
                throw new ApplicationException("Moving is not supported.");
            var target = GetObject(destination);
            var container = target as ContainerInfo;
            if (container == null)
                throw new ApplicationException("Target is not a container.");
            container.CheckCanBeChild(source);
            var subject = string.Format("\"{0}\" to \"{1}\"", source.Path, target.Path);
            if (ShouldProcess(subject, "Move Item"))
                WriteItemObject(manipulable.Move(container));
        }

        protected override void RemoveItem(string path, bool recurse) {
            EnsureLog();
            var item = GetObject(path);
            var removable = item as RemovableInfo;
            if (removable == null)
                throw new ApplicationException("Removing is not supported.");
            if (ShouldProcess(item.Path, "Remove Item"))
                removable.Remove(recurse);
        }

        protected override void RenameItem(string path, string newName) {
            EnsureLog();
            var item = GetObject(path);
            var manipulable = item as ManipulableInfo;
            if (manipulable == null)
                throw new ApplicationException("Renaming is not supported.");
            var subject = string.Format("\"{0}\" to \"{1}\"", item.Path, newName);
            if (ShouldProcess(subject, "Rename Item"))
                WriteItemObject(manipulable.Rename(newName));
        }

        protected override void SetItem(string path, object value) {
            throw new NotSupportedException("This operation is not supported.");
        }

        protected override bool ItemExists(string path) {
            EnsureLog();
            return HasObject(path);
        }

        protected override bool IsItemContainer(string path) {
            EnsureLog();
            return GetObject(path) is ContainerInfo;
        }

        protected override void InvokeDefaultAction(string path) {
            EnsureLog();
            var item = GetObject(path);
            var url = PathUtility.JoinPath(DriveInfo.WebUrl, item.Path);
            var subject = string.Format("\"{0}\" ({1})", item.Title, url);
            if (ShouldProcess(subject, "Open Item URL"))
                Process.Start(url);
        }

        protected override object InvokeDefaultActionDynamicParameters(string path) {
            return null;
        }

        // Item creation

        protected override void NewItem(string path, string itemTypeName, object newItemValue) {
            EnsureLog();
            path = PathUtility.NormalizePath(path);
            if (ShouldProcess(path, "New Item")) {
                string name;
                var parent = PathUtility.GetParentPath(path, out name);
                var target = GetObject(parent);
                // The type of the parameters object returned by the NewItemDynamicParameters
                // method decides the type of the object that is going to be created.
                Info added;
                var webParameters = DynamicParameters as NewWebParameters;
                if (webParameters != null) {
                    var web = target as WebInfo;
                    if (web == null)
                        throw new ApplicationException("Parent cannot contain a web.");
                    added = web.AddWeb(WebCreationParameters.Create(name, webParameters));
                } else {
                    var listParameters = DynamicParameters as NewListParameters;
                    if (listParameters != null) {
                        var listContainer = target as ListContainerInfo;
                        if (listContainer == null)
                            throw new ApplicationException("Parent cannot contain a list.");
                        added = listContainer.AddList(
                                    ListCreationParameters.Create(name, listParameters));
                    } else {
                        var itemContainer = target as ItemContainerInfo;
                        if (itemContainer == null)
                            throw new ApplicationException("Parent cannot contain an item.");
                        var folderParameters = DynamicParameters as NewFolderParameters;
                        if (folderParameters != null) {
                            added = itemContainer.AddFolder(name);
                        } else {
                            var fileParameters = DynamicParameters as NewFileParameters;
                            if (fileParameters != null) {
                                var contentContainer = target as ContentContainerInfo;
                                if (contentContainer == null)
                                    throw new ApplicationException(
                                                    "Parent cannot contain a file.");
                                newItemValue = newItemValue.GetBaseObject();
                                using (var content = GetContent(newItemValue, fileParameters))
                                    added = contentContainer.AddFile(name, content);
                            } else {
                                var itemParameters = (NewItemParameters) DynamicParameters;
                                added = itemContainer.AddItem(name);
                            }
                        }
                    }
                }
                WriteItemObject(added);
            }
        }

        protected override object NewItemDynamicParameters(string path, string itemTypeName,
                                                           object newItemValue) {
            if (string.IsNullOrEmpty(itemTypeName))
                throw new ApplicationException("Item type cannot be empty.");
            var typeName = "SharePosh.New" + itemTypeName + "Parameters";
            var type = Type.GetType(typeName, false, true);
            if (type == null)
                throw new ApplicationException("Invalid item type.");
            return Activator.CreateInstance(type);
        }

        // Converts the file content passed as any supported object to a binary stream;
        Stream GetContent(object value, NewFileParameters parameters) {
            if (value == null)
                throw new ApplicationException("Content passed as value cannot be empty.");
            value = value.GetBaseObject();
            var file = value as System.IO.FileInfo;
            if (file != null)
                return file.OpenRead();
            if (value is byte)
                return new MemoryStream(new byte[] { (byte) value });
            var encoding = parameters.GetEncoding();
            var text = value as string;
            if (text != null) {
                if (text.Length == 0)
                    throw new ApplicationException("Content passed as value cannot be empty.");
                return new MemoryStream(encoding.GetBytes(text));
            }
            var array = value as Array;
            if (array == null)
                throw new ApplicationException("Content passed as value " +
                    "must be a file, byte or string or an array of them.");
            var content = ConvertToBytes.GetBytes(array, encoding);
            if (content.Length == 0)
                throw new ApplicationException("Content passed as value cannot be empty.");
            return new MemoryStream(content);
        }

        // Child items handling.

        protected override bool HasChildItems(string path) {
            EnsureLog();
            var container = GetObject(path) as ContainerInfo;
            return container != null && container.HasChildren();
        }

        protected override void GetChildItems(string path, bool recurse) {
            GetChildren(path, recurse, false);
        }

        protected override object GetChildItemsDynamicParameters(string path, bool recurse) {
            return new GetChildrenParameters();
        }

        protected override void GetChildNames(string path, ReturnContainers returnContainers) {
            GetChildren(path, false, true);
        }

        protected override object GetChildNamesDynamicParameters(string path) {
            return new GetChildrenParameters();
        }

        void GetChildren(string path, bool recurse, bool nameOnly) {
            EnsureLog();
            GetChildren((ContainerInfo) GetObject(path), recurse, nameOnly);
        }

        void GetChildren(ContainerInfo parent, bool recurse, bool nameOnly, int depth = 0) {
            var parameters = (GetChildrenParameters) DynamicParameters;
            var children = parent.GetChildren();
            var filtered = children;
            if (parameters != null) {
                var childTypes = parameters.ParseChildTypes().ToList();
                if (childTypes.Any())
                    filtered = children.Where(item => childTypes.Contains(item.GetType()));
            }
            foreach (var child in filtered) {
                var entry = nameOnly ? (object) child.Name : child;
                var path = PathUtility.ConvertToPSPath(child.Path);
                WriteItemObject(entry, path, child is ContainerInfo);
            }
            if (recurse && !Stopping) {
                // If the maximum depth was specified use it otherwise set the value to maximum
                // integer - no path will be deeper...
                if (depth == 0)
                    depth = parameters != null && parameters.Depth > 0 ?
                        parameters.Depth : int.MaxValue;
                if (--depth > 0)
                    foreach (ContainerInfo child in children.OfType<ContainerInfo>())
                        GetChildren(child, recurse, nameOnly, depth);
            }
        }

        protected override bool IsValidPath(string path) {
            // Everything is allowed; mistakes like pairs of slashes are dealt with in the
            // PathUtility.NormalizePath method.
            return !string.IsNullOrEmpty(path);
        }

        //protected override string MakePath(string parent, string child) {
        //    // Workaround for the bug in PowerShell 2.0. If the Root of the drive is empty
        //    // the tab-completion offers the complete child path instead of the child name.
        //    if (parent == ".") {
        //        var providerPath = SessionState.Path.CurrentLocation.ProviderPath;
        //        if (string.IsNullOrEmpty(providerPath))
        //            return Utility.ConvertToPSPath(Utility.JoinPath(parent, child));
        //        if (child.StartsWithCI(providerPath + "\\"))
        //            return Utility.ConvertToPSPath(Utility.JoinPath(parent,
        //                child.Substring(providerPath.Length + 1)));
        //    }
        //    return base.MakePath(parent, child);
        //}

        // Content access.

        public void ClearContent(string path) {
            // This method is called not only by the Clear-Content cmdlet but also by the
            // Set-Content cmdlet. This is unfortunate because clearing content before setting
            // it is either not supported by some devices or services or it is unnecessary
            // performance loss at least. Luckily we can recognize the caller by the type of
            // the dynamic parameters.
            if (DynamicParameters == null)
                throw new NotSupportedException("This operation is not supported.");
        }

        public object ClearContentDynamicParameters(string path) {
            return null;
        }

        public IContentReader GetContentReader(string path) {
            EnsureLog();
            var parameters = (ContentReaderParameters) DynamicParameters;
            var item = GetObject(path);
            var content = item as ContentInfo;
            if (content == null)
                throw new ApplicationException("This item supports no content.");
            using (var stream = content.Open(parameters.Version))
                return parameters.UsingByteEncoding ? new ContentReader(stream) :
                    new ContentReader(stream, parameters.GetEncoding());
        }

        public object GetContentReaderDynamicParameters(string path) {
            return new ContentReaderParameters();
        }

        public IContentWriter GetContentWriter(string path) {
            EnsureLog();
            var parameters = (ContentWriterParameters) DynamicParameters;
            ContentContainerInfo container = null;
            string name = null;
            Info item;
            // The Set-Content cmdlet should be able to create a file too; not only to overwrite
            // an existing one or add a new version. If the item is not found its parent will be
            // tried to create a new file in.
            try {
                item = GetObject(path);
            } catch {
                var parent = PathUtility.GetParentPath(path, out name);
                item = GetObject(parent);
                container = item as ContentContainerInfo;
                if (container == null)
                    throw new ApplicationException(
                        "The parent item on the path is no file container.");
            }
            ContentInfo content = null;
            // If an item was found at the entered path it must be a file.
            if (container == null) {
                content = item as ContentInfo;
                if (content == null)
                    throw new ApplicationException("This item supports no content.");
            }
            var writer = parameters.UsingByteEncoding ? new ContentWriter() :
                new ContentWriter(parameters.GetEncoding());
            // The provider is no notified when the caller finishes sending the content.
            // It is not possible to open a channel to SharePoint and send there byte after byte.
            // SharePoint has the interface which accepts a stream to read from. That's why we
            // unfortunately have to buffer the entire content and when it is complete send it
            // to SharePoint. Closing the returned writer is already five minutes after twelve
            // o'clock but I didn't find anything better.
            writer.Closed += (sender, args) => {
                if (ShouldProcess(path, "Set Content"))
                    if (content != null)
                        try {
                            content.Save(args.Content);
                        } catch (Exception exception) {
                            WriteError(new ErrorRecord(exception, "WritingContentFailed",
                                ErrorCategory.ResourceUnavailable, content));
                        }
                    else
                        try {
                            container.AddFile(name, args.Content);
                        } catch (Exception exception) {
                            WriteError(new ErrorRecord(exception, "WritingContentFailed",
                                ErrorCategory.ResourceUnavailable, path));
                        }
            };
            return writer;
        }

        public object GetContentWriterDynamicParameters(string path) {
            return new ContentWriterParameters();
        }

        // Property support

        public void ClearProperty(string path, Collection<string> propertyToClear) {
            throw new NotImplementedException("This operation is not implemented yet.");
        }

        public object ClearPropertyDynamicParameters(string path,
            Collection<string> propertyToClear) {
            return null;
        }

        public void GetProperty(string path, Collection<string> providerSpecificPickList) {
            EnsureLog();
            var item = GetObject(path);
            var result = new PSObject();
            var propertyNames = providerSpecificPickList != null ? providerSpecificPickList.Where(
                entry => !string.IsNullOrEmpty(entry)) : null;
            var properties = item.Properties.Where(entry => entry.Value != null);
            if (propertyNames == null || !propertyNames.Any()) {
                foreach (var property in properties)
                    result.Properties.Add(new PSNoteProperty(property.Key, property.Value));
            } else {
                foreach (var name in propertyNames) {
                    IEnumerable<KeyValuePair<string, object>> matching;
                    if (WildcardPattern.ContainsWildcardCharacters(name)) {
                        var pattern = new WildcardPattern(name, WildcardOptions.IgnoreCase);
                        matching = properties.Where(entry => pattern.IsMatch(name));
                    } else {
                        matching = properties.Where(entry => name.EqualsCI(entry.Key));
                    }
                    foreach (var property in matching)
                        result.Properties.Add(new PSNoteProperty(property.Key, property.Value));
                }
            }
            WritePropertyObject(result, PathUtility.ConvertToPSPath(item.Path));
        }

        public object GetPropertyDynamicParameters(string path,
                            Collection<string> providerSpecificPickList) {
            return null;
        }

        public void SetProperty(string path, PSObject propertyValue) {
            throw new NotImplementedException();
        }

        public object SetPropertyDynamicParameters(string path, PSObject propertyValue) {
            return null;
        }

        // Internal members

        void EnsureLog() {
            if (DriveInfo != null) {
                var log = DriveInfo.Connector.Log as DriveLog;
                if (log == null || log.Provider != this)
                    DriveInfo.Connector.Log = new DriveLog(this);
            }
        }

        Info GetObject(string path) {
            if (DriveInfo == null)
                throw new InvalidOperationException("DriveInfo was null.");
            return DriveInfo.Connector.GetObject(path);
        }

        bool HasObject(string path) {
            if (DriveInfo == null)
                throw new InvalidOperationException("DriveInfo was null.");
            return DriveInfo.Connector.ExistsObject(path);
        }

        void WriteItemObject(Info item) {
            var path = PathUtility.ConvertToPSPath(item.Path);
            WriteItemObject(item, path, item is ContainerInfo);
        }
    }
}
