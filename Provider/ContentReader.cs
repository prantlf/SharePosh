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
using System.Linq;
using System.Management.Automation.Provider;
using System.Text;

namespace SharePosh
{
    class ContentReader : IContentReader
    {
        MemoryStream Content { get; set; }
        StreamReader Reader { get; set; }

        public ContentReader(Stream content) {
            if (content == null)
                throw new ArgumentNullException("content");
            Content = new MemoryStream();
            content.CopyTo(Content);
            Content.Position = 0;
        }

        public ContentReader(Stream content, Encoding encoding) : this(content) {
            if (encoding == null)
                throw new ArgumentNullException("encoding");
            Reader = new StreamReader(Content, encoding);
        }

        public void Dispose() {
            Close();
        }

        public void Close() {
            if (Reader != null) {
                Reader.Close();
                Reader = null;
            }
            if (Content != null) {
                Content.Close();
                Content = null;
            }
        }

        public IList Read(long readCount) {
            if (Content == null)
                throw new ObjectDisposedException("The reader has been closed.");
            return Reader != null ? ReadText(readCount) : ReadBytes(readCount);
        }

        IList ReadText(long readCount) {
            return Reader.ReadLines().Take((int) readCount).ToArray();
        }

        IList ReadBytes(long readCount) {
            var buffer = new byte[readCount];
            var length = Content.Read(buffer, 0, (int) readCount);
            if (readCount != length)
                Array.Resize(ref buffer, length);
            return buffer;
        }

        public void Seek(long offset, SeekOrigin origin) {
            if (Content == null)
                throw new ObjectDisposedException("The reader has been closed.");
            if (Reader != null)
                Reader.DiscardBufferedData();
            Content.Seek(offset, origin);
        }
    }
}
