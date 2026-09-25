# CDGFromYoutube

Converts a YouTube video into a karaoke pair: a `.cdg` (CD+G) graphics file and the matching `.mp3`.
Karaoke players such as Karafun pair the two up by file name, so both files are written with the same
base name.

```console
cdgfromyoutube "https://www.youtube.com/watch?v=..." -o "C:\Music\Karaoke"
```

## What it needs

* The .NET 10 SDK to build it, or the published executable to run it.
* **yt-dlp** to download the video and **ffmpeg** (with **ffprobe**) to decode it. The program looks for
  them in `tools` beside the executable, next to the executable itself, and on the `PATH`. If they are
  missing it says so and names the `winget` package for each.
* **Deno** as a JavaScript runtime for yt-dlp. YouTube hides how to reach many of its videos behind a
  JavaScript challenge; with no engine to solve it, yt-dlp either falls back to worse formats or the media
  download is refused with `HTTP Error 403: Forbidden`. Deno is looked for in the same places as the other
  tools, and `--js-runtime <path>` points at it explicitly.
* `--download-tools` fetches all three into a `tools` folder for you, which is the quickest way to get
  going:

  ```console
  cdgfromyoutube "<url>" --download-tools -o output
  ```

  yt-dlp and Deno come from their own release pages, and ffmpeg from the Windows build that gyan.dev
  publishes. They are downloaded from their project pages and never bundled with this program.

## Options

| Option | Meaning |
| --- | --- |
| `-o, --output <folder>` | Where the `.cdg` and `.mp3` files are written. Default: the current folder. |
| `-n, --name <name>` | Base name of the output files. Default: the video title. |
| `--fps <1-60>` | Frames per second taken from the video. Default: 15. |
| `--mp3-bitrate <8-320>` | MP3 bit rate in kbps. Default: 192. Reduced automatically if the sample rate cannot carry it. |
| `--mp3-sample-rate <hz>` | MP3 sample rate. Default: the source rate where MP3 carries it. |
| `--max-source-height <px>` | Do not download a source video taller than this, which saves bandwidth when the output is only 216 pixels tall anyway. |
| `--dither` | Mix the two colors inside a tile so gradients stop banding. |
| `--safe-area` | Keep the image inside the 288x192 area that every player shows. Default: use the whole 300x216 raster. |
| `--crop <auto\|l,t,r,b>` | Cut the margins off the video before scaling it, so the lyrics are drawn bigger and less blocky. `auto` finds the area where the picture keeps changing, which is the lyrics; four numbers cut those percentages from the left, top, right and bottom. Default: no cropping. |
| `--keep-temp` | Keep the downloaded video, and report where it is. |
| `--ffmpeg <path>` | Path to ffmpeg. ffprobe is expected beside it. |
| `--yt-dlp <path>` | Path to yt-dlp. |
| `--js-runtime <path>` | Path to Deno, the JavaScript runtime yt-dlp uses to reach some videos. |
| `--download-tools` | Fetch yt-dlp, ffmpeg and Deno into `./tools` when they cannot be found. |
| `-h, --help` | Show the usage text. |

## What the format allows, and why that matters

This is the part worth reading before judging the output.

A CD+G player reads exactly **300 packets per second**, and each packet draws one **6x12 tile**. A whole
screen is 900 tiles, so replacing everything on screen always takes **three seconds of playback**. That
is the entire graphics budget of the format, and no amount of computer power changes it:

* Material that changes little between frames — lyrics, titles, slides, a mostly still background —
  converts beautifully and runs at the full frame rate.
* Moving video can only be shown as a slow sequence of screens. The program spends the packets it has and
  **drops any frame it cannot pay for**, then reports how many it dropped.

A measured example, a 19 second live action clip:

```text
Graphics: 7 frames drawn and 277 dropped, which is 0.37 frames a second over 00:00:19.
```

That is not a defect in this program; it is what a 28.8 kbit/s graphics channel does with video. What the
program guarantees is that nothing drifts out of step with the audio:

* Every frame is written at the packet index that matches its timestamp, padded with no-operation packets
  where nothing has to change.
* The file holds exactly `length x 300` packets, so the graphics stay aligned with the MP3 for the whole
  track.

`--fps` sets how often frames are offered, not how many survive. Lowering it saves decoding time.

## Graphics quality, and where the artefacts come from

Three limits of the format decide how close the picture can get to the video it came from:

1. The raster is 300x216, so a 720p or 1080p source is scaled down by four or six. Small lettering that
   was three or four pixels thick becomes about one pixel thick, surrounded by antialiased half shades.
2. Only sixteen colours exist, chosen once for the whole track from frames sampled across it.
3. A tile of 6x12 pixels may use **two** of those colours. This is the one that bites: a tile holding a
   glyph edge must pick two colours for it, so an edge pixel lands on one or the other and the lettering
   comes out with a stepped outline.

Those are properties of CD+G rather than of this program. The instruction fields do have room for a bigger
picture - a tile's row and column can address up to 384x384 pixels - but a conforming player keeps a
300x216 buffer and drops anything outside it, so the extra room cannot be reached. (The 288x192 figure the
format quotes is the display area; 300x216 is that area plus the one tile thick border around it, which is
what `--safe-area` is about.) There is an extended standard, CD+EG (also called CD+XG and Extended
TV-Graphics), offering up to 256 colours, but it saw almost no releases and no karaoke player implements it,
so CD+G is the target here.

The encoder works around these where it can:

* **The tile's colours are chosen by scoring every pair** of palette entries against the tile's own colour
  counts, not by taking the two most common colours. Choosing by frequency picks middling colours for a
  tile that covers part of a gradient, which is what leaves a flat patch where the picture is smooth.
* **The palette is cut by color range, not by pixel count, and refuses near duplicates.** Splitting the
  group with the widest spread of colors stops a track with a black background from spending most of the
  table on near black entries; an entry that still lands on top of another is passed over for a color that
  is genuinely different. The track above went from a table of six near blacks and one white to black,
  dark reds, greys, white and the pinks in the logo.
* **Only tiles that change are written**, so a still picture costs nothing to hold and every packet goes to
  the parts of the picture that moved.
* **`--crop auto` makes the lyrics bigger.** The biggest cause of blocky lettering is how few pixels
  each letter gets. A 16:9 video fitted into 300x216 leaves 48 rows empty, and the lyrics often use only the
  middle two thirds of the width, so a line of lyrics was about 8 pixels tall. `--crop auto` samples the
  video once a second, keeps the pixels that change in at least 4 samples (or 2% of them, if that is
  more), and crops to the box around them with a 2% border. Lyrics are drawn, highlighted and cleared page
  after page, so they change constantly; a logo that stays on screen never changes, and a title card changes
  only briefly, so neither widens the box. On the track above the lyrics grew about 1.45x and filled the
  screen. The costs: anything outside the lyrics, such as the ends of a wide title card, is cut off, and
  bigger letters take more tiles, so a page change takes longer to draw.
* **Everything dark is drawn as colour 0, the background.** Players such as KaraFun show their own backdrop
  wherever the screen holds colour 0, so colour 0 behaves as transparent. Karaoke videos often put the
  lyrics over a dim texture, and when that texture had palette entries of its own, the player drew each of
  its tiles as an opaque dark block around the lyrics. Any colour with every channel at level 4 of 15 or
  below (about 68 of 255) now becomes colour 0 and gets no palette entry. The cost is that very dark
  detail in the video, such as a dim logo or black lettering, shows as the player's backdrop.
  `scripts/render-cdg.ps1 -ShowTransparency` draws colour 0 in blue to show what a player will do.
* **A tile that really needs a third colour gets one through an XOR pass.** Where the highlight is sweeping
  across a word, one tile holds the background, the letters still to sing and the letters already sung.
  With two colours the highlight had to go, and because it is nearer black than white, the word being sung
  was drawn as a black block. The tile is now drawn with its best two colours and then an exclusive-or tile
  (instruction 38) flips the pixels of the third. That costs a second packet, so it is only used where a
  whole group of pixels would otherwise be wrong, and the second passes are written after every tile's first
  pass and only with the packets left before the next frame: a new page goes up as fast as before, and any
  second pass that did not fit is added by a later frame for one packet. On the track above this cut the
  highlighted lyric pixels drawn as black from 2.5% to 0.9% and raised the mean from 23.3 dB to 23.8 dB.

Measured on a 3:48 karaoke track, decoding the result exactly as a player would and comparing it with the
source frame by frame:

| Change | Mean error against the source |
| --- | --- |
| Pairs chosen by frequency, palette cut by pixel count | 23.8 dB |
| Pairs scored, palette cut by color range and spread out | **24.1 dB** |
| ...with `--dither` | 23.8 dB |
| ...with the picture sharpened before reduction | 23.1 dB |

Two things that sound like they should help were measured and rejected. Dithering mixes the two colours of
a tile, which softens gradients, but at this resolution it costs accuracy and turns block edges into
visible speckle, so it stays off unless `--dither` asks for it. Sharpening before reduction also measured
worse, because the half shades it removes are real information that the palette was representing.

An amplified difference image against the source shows where the remaining error sits: the background is
pixel exact, and everything that is wrong is on the lettering itself, along its antialiased edges. That is
limit 3 above and no encoder can get past it. Practically, if the blocks you see are in smooth areas such
as a plasma background, `--dither` trades them for a fine pattern; if they are on the edges of letters,
that is the format.

`scripts/compare-cdg.ps1` produces that measurement for any pair of files, and `scripts/render-cdg.ps1`
writes the picture out at chosen moments so it can be looked at without a karaoke player.

## How the conversion works

1. **Download.** yt-dlp fetches the best video and audio into a temporary folder, merged into Matroska so
   that any combination of codecs works.
2. **Probe.** ffprobe reports the length, the video size, and the audio sample rate and channel count.
3. **Resolution rule.** Frames are scaled into the raster with their shape preserved and padded with
   black, so widescreen video gains bars instead of being stretched. Anything larger than 300x216 is
   scaled down; anything smaller is scaled up, because the raster is a fixed size.
4. **Sample rate rule.** The MP3 keeps the source sample rate whenever MP3 carries it (8, 11.025, 12, 16,
   22.05, 24, 32, 44.1 or 48 kHz). A source above 48 kHz is reduced to the highest rate that MP3 carries,
   and the bit rate is reduced too if the chosen rate cannot carry it.
5. **Palette.** One sixteen color palette is chosen for the whole track by median cut, from frames sampled
   at one per second. A fixed palette is what makes incremental drawing possible: changing the color table
   part way through would force every tile to be redrawn.
6. **Tiles.** Each frame is reduced to 6x12 tiles. A tile may only use two of the sixteen colors, so each
   tile is rebuilt from the pair that best explains the pixels inside it, and the twelve scanline bytes say
   which pixels take which of the two. Only the tiles that differ from what is already on screen are
   written, which is why a still image costs nothing to hold.
7. **Audio.** ffmpeg decodes the audio to sixteen bit stereo PCM at the chosen rate, and LAME (through
   NAudio.Lame) encodes it to MP3.
8. **Finish.** The graphics stream is padded to exactly the length of the track and the temporary folder is
   removed, unless `--keep-temp` was given.

## Project layout

```text
src/CdgFromYoutube/
  Cdg/          the packet format, color table, palette building, tile encoding and packet budget
  Imaging/      pixel layout, aspect fitting and the ordered dither matrix
  Media/        yt-dlp, ffprobe, ffmpeg and LAME: everything that touches the outside world
  Tooling/      finding, and if asked, downloading the external tools
  CommandLine/  options and the hand written argument parser
  Pipeline/     the conversion itself, in the order it happens
tests/CdgFromYoutube.Tests/
scripts/       helpers for looking at the graphics and measuring them against the source
```

The CD+G details come from "CD+G Revealed" by Jim Bumgardner (https://jbum.com/cdg_revealed.html), checked
against the CD+G decoder in VLC, which agrees on the tile size, the tile addressing and the color table
layout. The classes that depend on the format say so in their `<remarks>`.

## Tests

```console
dotnet test
```

The suite covers the color table packing, the tile bit order, the packet budget and frame dropping, the
palette reduction, the ffprobe parsing and the command line.

## Checking a file yourself

Every packet is 24 bytes: `0x09` in byte 0, the instruction in byte 1, and the instruction's data from byte
4. A `.cdg` file therefore holds `length x 300` packets, and reading byte 1 of each one shows the shape of
the file: 1 and 2 are the memory and border presets, 30 and 31 load the color table, 6 draws a tile,
38 draws a tile by XOR-ing it onto the screen, and 0 is a packet that carries no command at all.
