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
using System.Collections;
using System.IO;
using System.Management.Automation.Provider;
using System.Text;

namespace SharePosh
{
    class ContentEventArgs : EventArgs
    {
        public Stream Content { get; private set; }
        public long Length { get; private set; }

        public ContentEventArgs(Stream content, long length) {
            if (content == null)
                throw new ArgumentNullException("content");
            Content = content;
            Length = length;
        }
    }

    class ContentWriter : IContentWriter
    {
        MemoryStream Content { get; set; }
        StreamWriter Writer { get; set; }

        public EventHandler<ContentEventArgs> Closed;

        public ContentWriter() {
            Content = new MemoryStream();
        }

        public ContentWriter(Encoding encoding) : this() {
            Writer = new StreamWriter(Content, encoding);
        }

        public void Dispose() {
            Close();
        }

        public void Close() {
            if (Writer != null)
                Writer.Flush();
            if (Content != null && Closed != null) {
                Content.Position = 0;
                Closed(this, new ContentEventArgs(Content, Content.Length));
            }
            if (Writer != null) {
                Writer.Close();
                Writer = null;
            }
            if (Content != null) {
                Content.Close();
                Content = null;
            }
        }

        public IList Write(IList content) {
            if (Content == null)
                throw new ObjectDisposedException("The reader has been closed.");
            if (Writer != null)
                WriteText(content);
            else
                WriteBytes(content);
            return content;
        }

        void WriteText(IList content) {
            foreach (var part in content) {
                var item = part.GetBaseObject();
                var array = item as Array;
                if (array != null) {
                    foreach (var entry in array)
                        Writer.Write(entry.GetBaseObject());
                    Writer.WriteLine();
                } else {
                    Writer.WriteLine(item);
                }
            }
        }

        void WriteBytes(IList content) {
            foreach (var part in content) {
                var item = part.GetBaseObject();
                var bytes = item as byte[];
                if (bytes != null) {
                    Content.Write(bytes, 0, bytes.Length);
                } else {
                    var array = item as Array;
                    if (array != null) {
                        foreach (var entry in array)
                            Content.WriteByte((byte) entry.GetBaseObject());
                    } else {
                        Content.WriteByte((byte) item);
                    }
                }
            }
        }

        public void Seek(long offset, SeekOrigin origin) {
            if (Content == null)
                throw new ObjectDisposedException("The reader has been closed.");
            if (Writer != null)
                Writer.Flush();
            Content.Seek(offset, origin);
        }
    }
}
