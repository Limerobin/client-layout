# Client layout

A small Windows program that places World of Warcraft windows, removes the title bar, and keeps a chosen window on top.

It works with the Wrath 3.3.5 client and with World of Warcraft: Forever. It only moves and styles windows. It does not send keystrokes or clicks.

## Use

1. Download `ClientLayout.exe` from the [latest release](https://github.com/Limerobin/client-layout/releases/latest) and start it.
2. Browse to that game's `Wow.exe` if the path is not already filled in.
3. Open the clients and drag each window where you want it.
4. Select the small window, then click **Remove border** and **Keep on top**. Leave the main window with its border and **Not on top**.
5. Put the main window on row 1 with **Move up**. That row is the first window **Launch** places.
6. Click **Save layout**.

**Launch** starts the chosen exe until that many clients are open, then puts each new window in the next saved slot. **Apply layout** puts windows that are already open back into place.

For the Battle.net client, press Play on each account, then **Apply layout**. Saving while those windows are open stores a separate layout for that `Wow.exe`.

A window with no border moves with **Alt+Space**, then **M**. The main client has to stay windowed. Fullscreen exclusive covers the small window.

The saved layout is `layout.json` next to the program. It is not part of this repository.

## Build

From this folder, with the .NET Framework compiler that ships with Windows:

```
%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /win32manifest:app.manifest /out:ClientLayout.exe /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Runtime.Serialization.dll ClientLayout.cs
```

A push to `main` or `master` runs that same compile on GitHub Actions and publishes `ClientLayout.exe` on the [latest release](https://github.com/Limerobin/client-layout/releases/latest).
