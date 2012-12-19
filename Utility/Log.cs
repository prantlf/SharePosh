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

namespace SharePosh
{
    // Encapsulates logging functionality so that it can be used from other components than
    // the drive provider itself (which only has an access to the console).
    public interface Log
    {
        void Verbose(string message);

        void Verbose(string format, params object[] args);
    }

    // Does nothing; it just implements the interface. It can be used at times when no drive
    // provider is available.
    public class DummyLog : Log
    {
        DummyLog() {}

        public void Verbose(string message) {}

        public void Verbose(string format, params object[] args) {}

        public static readonly Log Instance = new DummyLog();
    }

    // Uses the functionality of the PowerShell provider to perform logging.
    public class DriveLog : Log
    {
        public DriveProvider Provider { get; private set; }

        public DriveLog(DriveProvider provider) {
            if (provider == null)
                throw new ArgumentNullException("provider");
            Provider = provider;
        }

        public void Verbose(string message) {
            Provider.WriteVerbose(message);
        }

        public void Verbose(string format, params object[] args) {
            Provider.WriteVerbose(string.Format(format, args));
        }
    }
}
