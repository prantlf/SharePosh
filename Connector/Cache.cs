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
    // Tamagochi-cache which needs a constant coddling so that it retains its content. As long
    // as data are being read and written from/to it it stays populated. If there is a pause
    // between subsequential accesses longer than the specified time period all the cache content
    // will be discarded. A reasonable time period is 1 or 2 seconds.
    //
    // Instead of maintaining the cache actively and checking if the particular object is still
    // valid or should be discarded because there is a newer version available on the server, this
    // cache exploits the style of work in the PowerShell console. When commands are entered
    // manually there is a natural pause among them which will flush the cache. Every entered
    // command will get the most recent data from the server and not an outdated cache content.
    // If the commands are executed rapidly one after another - in a script, for example - the
    // cache will be constantly in use and thus kept full. This can work because the provider code
    // requests access to server objects almost all the time. Provider methods can be called
    // multiple times even during execution of a single command; caching information returned by
    // the server and thus preventing repetitious server calls is important. This batch caching
    // also supports kind of atomic behavior of a single script which may even benefit from using
    // consistent data from the originating time. Changes of server objects caused by the provider
    // are reflected in the cache content by explicit removals of affected objects.
    class Cache
    {
        public Cache(TimeSpan keepPeriod) {
            KeepPeriod = keepPeriod;
        }

        public bool TryGetObject(string path, out Info item) {
            if (path == null)
                throw new ArgumentNullException("path");
            Check();
            return Objects.TryGetValue(path, out item);
        }

        public Info GetObjectOrDefault(string path) {
            if (path == null)
                throw new ArgumentNullException("path");
            Info result;
            return TryGetObject(path, out result) ? result : null;
        }

        public void PutObject(Info item) {
            if (item == null)
                throw new ArgumentNullException("item");
            Objects[item.Path] = item;
            Touch();
        }

        public void RemoveObject(Info item) {
            if (item == null)
                throw new ArgumentNullException("item");
            Objects.Remove(item.Path);
            if (item is ContainerInfo) {
                // Children of this container still can be in the cache. Once the container is
                // removed the children should go with it too.Their path will start with the
                // container's path and follow after the trailing slash.
                var root = item.Path + "/";
                var descendants = Objects.Keys.Where(key => key.StartsWithCI(root)).ToList();
                foreach (var descendant in descendants)
                    Objects.Remove(descendant);
            }
            Touch();
        }

        // If the checking call comes within the keeping period the current time is remembered
        // to check the next time against. If the call comes too late the cache content will be
        // cleared and thus the cache read which called this check will find nothing. Cache
        // write operations unconditionally store the current time.

        public bool Check() {
            // The current time must lie between the last check and the entered future time.
            var result = LastCacheCheck + KeepPeriod > DateTime.UtcNow;
            if (result)
                Touch();
            else
                Objects.Clear();
            return result;
        }

        public void Invalidate() {
            LastCacheCheck = DateTime.MinValue;
        }

        void Touch() {
            LastCacheCheck = DateTime.UtcNow;
        }

        Dictionary<string, Info> Objects = new Dictionary<string, Info>(
            ConfigurableComparer<string>.CaseInsensitive);
        DateTime LastCacheCheck = DateTime.UtcNow;
        TimeSpan KeepPeriod;
    }
}
