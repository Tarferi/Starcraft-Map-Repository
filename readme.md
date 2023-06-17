# Starcraft Map Repository
<!---
Original topic: [TODO](http://www.staredit.net/todo)
-->

![Screenshot](https://rion.cz/scmdb/scr1.png "Screenshot")

![Screenshot](https://rion.cz/scmdb/scr2.png "Screenshot")

Features:
* Map preview in Remastered, Carbot and HD graphics.
* Simple map download with the possibility to open.
* Load any map terrain, even from protected maps.
* Can work as separate executable or from within a SCMDraft as a plugin.


Project structure:
* **AssetsPacker** and **AssetsPackerCS**
   * Packing Starcraft assets into binaries for use for preview.
   * **AssetsPackerCS** Turned out to be running terribly slow, so it was rewritten into C++ and is now deprecated.
* **GUILib**
   * Main project containing most of the code. Does not run on its own.
* **SCMDPlugin**
   * Project for building SCMDraft Plugin. Contains interface and some callbacks for SCMDraft.
* **WPFRunner**
   * Project for building standalone executable.

TODO:
* Sprites and units featured in preview.
* Embed forbidden compression algorithms.
* Add protection toolchain per map file basis.
* Add map publishing.
* Advanced search.


Internal TODO:
* Figure out how to space the plugin button within SCMD window.
* Design and switch to JSON API.
* Finalize configuration editing in asset packer.
* Possible x64 support for SCMD plugin.
* Multiple instances.