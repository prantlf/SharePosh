# Module manifest for module 'SharePosh'
#
# Copyright (C) 2012 Ferdinand Prantl <prantlf@gmail.com>
# All rights reserved.       
#
# This file is part of SharePosh - SharePoint drive provider for PowerShell.
#
# This program is free software: you can redistribute it and/or modify
# it under the terms of the GNU General Public License as published by
# the Free Software Foundation, either version 3 of the License, or
# (at your option) any later version.
#
# This program is distributed in the hope that it will be useful,
# but WITHOUT ANY WARRANTY; without even the implied warranty of
# MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
# GNU General Public License for more details.
#
# You should have received a copy of the GNU General Public License
# along with this program.  If not, see <http://www.gnu.org/licenses/>.

@{

# Script module or binary module file associated with this manifest
ModuleToProcess = 'SharePosh.dll'

# Version number of this module.
ModuleVersion = '1.0'

# ID used to uniquely identify this module
GUID = 'becf5d59-eb0f-4e99-9cc3-32cec519f0f4'

# Author of this module
Author = 'Ferdinand Prantl <prantlf@gmail.com>'

# Company or vendor of this module
CompanyName = 'Ferdinand Prantl'

# Copyright statement for this module
Copyright = '(c) 2012 Ferdinand Prantl <prantlf@gmail.com>. All rights reserved.'

# Description of the functionality provided by this module
Description = 'PowerShell module to make the SharePoint content accessible in the same way as you work with the local file system.'

# Minimum version of the Windows PowerShell engine required by this module
PowerShellVersion = '2.0'

# Name of the Windows PowerShell host required by this module
PowerShellHostName = ''

# Minimum version of the Windows PowerShell host required by this module
PowerShellHostVersion = ''

# Minimum version of the .NET Framework required by this module
DotNetFrameworkVersion = '3.5'

# Minimum version of the common language runtime (CLR) required by this module
CLRVersion = '2.0'

# Processor architecture (None, X86, Amd64, IA64) required by this module
ProcessorArchitecture = ''

# Modules that must be imported into the global environment prior to importing this module
RequiredModules = @()

# Assemblies that must be loaded prior to importing this module
RequiredAssemblies = @()

# Script files (.ps1) that are run in the caller's environment prior to importing this module
ScriptsToProcess = @()

# Type files (.ps1xml) to be loaded when importing this module
TypesToProcess = @()

# Format files (.ps1xml) to be loaded when importing this module
FormatsToProcess = @( 'SharePosh.format.ps1xml' )

# Modules to import as nested modules of the module specified in ModuleToProcess
NestedModules = @()

# Functions to export from this module
FunctionsToExport = '*'

# Cmdlets to export from this module
CmdletsToExport = '*'

# Variables to export from this module
VariablesToExport = '*'

# Aliases to export from this module
AliasesToExport = '*'

# List of all modules packaged with this module
ModuleList = @()

# List of all files packaged with this module
FileList = @( 'SharePosh.dll' )

# Private data to pass to the module specified in ModuleToProcess
PrivateData = ''

}
