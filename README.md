# CDGFromYoutube

>**Disclaimer:** Using this tool to create CDGs for profit or for illegal purposes is **PROHIBITED**. I will NOT be liable for any illegal activity resulting from the use of this tool.

Turns a YouTube karaoke video into a karaoke file pair: a `.cdg` graphics file and the matching
`.mp3`. Karaoke players such as KaraFun pick the two up automatically, because they share the same file
name.

```console
cdgfromyoutube "https://www.youtube.com/watch?v=..." -o "C:\Music\Karaoke"
```

## Quick start

1. Run the installer, `CDGFromYoutubeSetup.exe` (get it from the releases section on Github). It installs the program and, if it is
   missing, the .NET runtime it needs.
2. Open a new Command Prompt. (One that was already open won't see the change the installer just made, so
   open a fresh one.)
3. Convert a video:

   ```console
   cdgfromyoutube "https://www.youtube.com/watch?v=..." -o "C:\Music\Karaoke"
   ```

   Replace the URL with a real YouTube video. The first time, it asks whether to download the extra tools
   it needs; press Enter to say yes. That only happens once, however many times or folders you run it from
   after that.
4. Copy the `.cdg` and `.mp3` files it wrote into your karaoke player's song folder. Most players,
   including KaraFun, pick up the pair automatically because the file names match.

That's it. Run `cdgfromyoutube -h` to see every other option, and try `--crop auto` if the lyrics come out
small on screen (see "Getting the best results" below).

## Getting the best results

* **If the lyrics look small, add `--crop auto`.** Most karaoke videos only use part of the screen for the
  words; this crops the rest away so the lyrics are drawn bigger and clearer.
* **If the video is just lyrics on a plain dark background, add `--antialias`.** It smooths out the letters
  so they look less blocky. It takes a little longer for a new line of lyrics to appear in full, which is
  rarely noticeable.
* **If you want crisp, flat-coloured lyrics instead, try `--flat-colours`.** It draws each lyric colour
  solidly, with no shading, which suits some videos better than `--antialias` does. The letters can look a
  little jagged on curves and diagonals as a trade-off.
* **Fast-moving video, such as a real music video, will never look smooth.** This is a genuine limit of the
  1980s karaoke graphics format this program targets, not a bug: it can redraw the whole screen only once
  every three seconds. It's built for lyrics, slides and still pictures, which it handles very well; it was
  never going to handle a car chase. See [docs/technical-details.md](docs/technical-details.md) if you want
  to know exactly why.

## What you need installed

* **Windows.** Using the installer, that's all you need: it fetches the plain .NET 10 runtime for you if
  your machine doesn't already have it. Building and running from source instead needs the full **.NET 10
  SDK**, not just the runtime.
* **yt-dlp** and **ffmpeg**, which do the actual downloading and video conversion. Neither comes bundled
  with the program. It looks for them next to itself, then on your `PATH`; if it can't find them, it
  offers to download them for you the first time you run it (or run `cdgfromyoutube --download-tools` to
  fetch them up front).
* **Deno**, a small JavaScript engine that yt-dlp sometimes needs to reach a video at all. It's optional,
  but without it some videos may fail to download with an error mentioning "403 Forbidden". The tool
  downloader above fetches this too.

## Building and running from source

Everything below is for building and running the program directly from this folder, with no installer.

1. **Build and run it once**, fetching the tools it needs the first time:

   ```console
   dotnet run --project src/CdgFromYoutube -- "https://www.youtube.com/watch?v=..." --download-tools -o output
   ```

   Replace the URL with a real YouTube video. `-o output` writes the `.cdg` and `.mp3` into an `output`
   folder inside this one; use a full path such as `-o "C:\Music\Karaoke"` to write somewhere else.

2. **Copy `.cdg` and `.mp3` from the `output` folder** into your karaoke player's song folder.

3. **Run it again without `--download-tools`** once the tools are there:

   ```console
   dotnet run --project src/CdgFromYoutube -- "https://www.youtube.com/watch?v=..." -o output
   ```

Run `cdgfromyoutube -h` (or `dotnet run --project src/CdgFromYoutube -- -h`) any time to see every option.
Every run also prints the program's name and version first, such as `CDGFromYoutube (1.1.0)`, so a bug
report can say which build it came from.

## Options

| Option | Meaning |
| --- | --- |
| `-o, --output <folder>` | Where the `.cdg` and `.mp3` files are written. Default: the current folder. |
| `-n, --name <name>` | Base name of the output files. Default: the video's title. |
| `--fps <1-60>` | How many pictures a second are taken from the video. Default: 15. Lower is faster to convert; it rarely changes how the result looks. |
| `--mp3-bitrate <8-320>` | MP3 quality, in kbps. Default: 192, which is good enough that higher rarely helps. |
| `--mp3-sample-rate <hz>` | MP3 sample rate. Default: matches the source, so you shouldn't normally need this. |
| `--max-source-height <1-4320>` | Downloads a smaller version of the video when one is available, which is faster and uses less data. The output is tiny anyway, so there's rarely a reason to download more than, say, 360 or 480. |
| `--dither` | Softens colour banding in gradients by blending colours together. Rarely needed for karaoke lyrics. |
| `--antialias` | Smooths the edges of lyrics. See "Getting the best results" above. |
| `--flat-colours` (or `--flat-colors`) | Draws lyrics in solid, flat colour instead of smoothed shading. See "Getting the best results" above. Cannot be used together with `--antialias`. |
| `--safe-area` | Keeps the picture within the smaller area every player is guaranteed to show. Only worth trying if you notice the edges of the picture being cut off on your player. |
| `--crop <auto\|l,t,r,b>` | Crops the video before resizing it, so the lyrics fill more of the screen. `auto` finds the lyrics automatically; see "Getting the best results" above. |
| `--keep-temp` | Keeps the downloaded video instead of deleting it afterwards, and tells you where it is. |
| `--ffmpeg <path>` | Use a specific copy of ffmpeg instead of searching for one. |
| `--yt-dlp <path>` | Use a specific copy of yt-dlp instead of searching for one. |
| `--js-runtime <path>` | Use a specific copy of Deno instead of searching for one. |
| `--download-tools` | Fetch yt-dlp, ffmpeg and Deno without asking first, if any are missing. |
| `-h, --help` | Show the built-in usage text. |
| `-v, --version` | Show the program's version, on its own with nothing else, so a script can read it. |

## More about how this works

CD+G is a real, quite old karaoke format with some hard limits, and this program works around several of
them to get the best picture it can out of it. If you're curious how, or you're looking to contribute,
[docs/technical-details.md](docs/technical-details.md) covers the format's limits, how the picture is
processed, the conversion pipeline step by step, the project's folder layout, and the test suite.

## Tests

```console
dotnet test
```
