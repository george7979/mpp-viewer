# Third-Party Notices

MPP Viewer's own source code is licensed under the MIT License (see [LICENSE](LICENSE)).
The application bundles the following third-party components.

## MPXJ

- **Project:** https://www.mpxj.org/
- **Source:** https://github.com/joniles/mpxj
- **Package:** `net.sf.mpxj` 13.12.0 (NuGet)
- **Author:** Jon Iles
- **License:** GNU Lesser General Public License, version 2.1 or later (`LGPL-2.1-or-later`) — https://www.gnu.org/licenses/old-licenses/lgpl-2.1.html

MPXJ is used to read Microsoft Project files (`.mpp` and MSPDI `.xml`). It is licensed
under the LGPL. The MPP Viewer executable is a **self-contained single file that bundles
MPXJ**. In accordance with the LGPL, the complete source code of MPP Viewer is available at
https://github.com/george7979/mpp-viewer together with build instructions (see the README),
so you can rebuild the application after modifying or replacing the bundled MPXJ library with
a compatible version.

## .NET runtime

The published executable is self-contained and redistributes the .NET 8 runtime, licensed
under the MIT License (© .NET Foundation and contributors) — https://github.com/dotnet/runtime/blob/main/LICENSE.TXT

## Trademarks

Not affiliated with or endorsed by Microsoft. *Microsoft Project*, *MS Project*, and related
names are trademarks of Microsoft Corporation. MPP Viewer is an independent, third-party tool
that reads the `.mpp` file format for interoperability purposes.
