# Pseudo Localize UWP

A UWP (C#) port of [**pseudolocalize**](https://github.com/gailium119/pseudolocalize

The original is a Win32 command-line tool written in C++. This port keeps the same transformation rules and character tables, wraps them in a C# library.

```
Save changes before closing?   →   [0ioSo][Šаʋз ċћáñġзŝ вέƒбгē ¢ℓǿşιпģ? !!! !!!]
```

---

## Why pseudo-localization?

Wrap every translatable string in obvious markers and replace letters with look-alikes from other alphabets, then run the app:

| What you're testing | How the marks reveal it |
|---|---|
| **Hard-coded strings** | Anything still in plain ASCII was never localized |
| **Clipping / truncation** | The closing `]` gets cut off, so you see it immediately |
| **Layout overflow** | The `!!!` padding simulates text growing ~30 % (German, Finnish…) |
| **String concatenation** | Adjacent pieces carry different `[IDs]` — a sign the sentence was assembled from parts |
| **Missing translations** | An empty slot becomes `[abcde][]` instead of vanishing |

## Output anatomy

```
[abcde][Šаʋз ċћáñġзŝ вέƒбгē ¢ℓǿşιпģ? !!! !!!]
 └─┬─┘└──────────────────────┬──────┘└──┬──┘
   ID               converted text    padding
```

## Features

* **String mode** — paste any text, get the pseudo-localized version, copy it or save it.
* **File mode** — pick a localization file, convert it, preview the result as plain text, then save it anywhere.

Supported file types:

| Extension | Upstream command | What gets converted |
|---|---|---|
| `.txt` | `file` | every line, as a whole |
| `.inf` | `infloc` | only double-quoted values, after a `; Localizable` comment switches it on |
| `.ini` | `ini` | values (`= ...`) inside `[Objects]` / `[Text]` sections |
| `.mfl` | `mfl` | only the strings inside `DisplayName("…")` and `Description("…")` |
| `.psd1` | `psd1` | the right-hand side of every `key = value` line |
| `.xml` | `xml` | `//String` text nodes, plus attributes declared by `//_locTag` elements |
| `.adml` | `adml` | a fixed list of elements: `string`, `label`, `displayName`, `description`, `checkBox`, … |
| `.resx` | `resx` | `<data><value>` for UI-like names; `System.Drawing.Size/Point` widths are scaled by 1.4 |

## Build

Requirements:

* Visual Studio 2019 or 2022 with the **Universal Windows Platform development** workload
* Windows 10 SDK **10.0.19041** (the project targets `10.0.19041.0`, minimum `10.0.15063.0`)
* C# 7.3 — the project is a classic (non-SDK-style) UWP project

```
Open PseudolocalizeUWP.sln → select x86 / x64 / ARM / ARM64 → F5
```

If you get a stale-generated-code error after renaming a control (for example a complaint about a control name that no longer exists), delete the `obj` folder or use **Build → Clean Solution**, then rebuild.

## Known limitations

* **UWP sandbox.** Only files chosen through the file picker can be read. Typing an arbitrary path into the path box will usually fail with `UnauthorizedAccessException`; adding the restricted `broadFileSystemAccess` capability would be required to make arbitrary paths work.
* Large files are converted synchronously on the UI thread.

## Credits

* **Original project:** [gailium119/pseudolocalize](https://github.com/gailium119/pseudolocalize) — the C++ command-line tool this port is based on. The character tables, the transformation rules and the per-format heuristics all come from its `pseudo_utf8.cpp` / `pseudo.cpp`.
* **Upstream credits:** the original project thanks [**RisohEditor**](https://github.com/katahiromz/RisohEditor) by Katayama Hirofumi MZ (katahiromz), from which its resource-handling code derives. This port does not include any RisohEditor code, since the resource/`rc` path was not ported.
