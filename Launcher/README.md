# LemonVN Windows x64 launcher

This Apache-2.0 helper starts the fixed `app/LemonVN.exe` beside it. Named
`Uninstall.exe`, the same bytes add the exact `--uninstall` argument. It uses
absolute paths and the app directory as working directory, inherits the normal
user environment and exits immediately without opening data or waiting for UI.
No shell, command-line forwarding, PATH search, elevation, network or deletion.

Build with `scripts/Build-Launcher.ps1 -ZigPath <path-to-zig.exe>` using pinned
Zig 0.15.2 (official https://ziglang.org/download/). Zig is a build-only C/linker/RC
tool, not a user runtime or a ZIP payload. Build flags use `-nostdlib`, no CRT,
no builtins and no stack-protector runtime. The PE import audit permits only
KERNEL32.dll and USER32.dll; no Zig/MinGW/VC/.NET runtime is added by this helper.
The normal .NET10/WinUI application and its existing component licenses remain
inside app/. The icon is the already-approved LemonVN icon.

The generated launcher and .res/.rc files live in ignored Launcher/bin/.
Do not check in compiler binaries, import libraries or downloaded archives.
