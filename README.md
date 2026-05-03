# Quick Image Viewer

A fast and lightweight image viewer application for Windows. It allows you to quickly browse images in a folder using a grid view and supports a spread view (manga mode).

* [日本語のドキュメントはこちら](README_ja.md)

## Download

https://github.com/SoraKumo001/quick-image-viewer/releases

## Key Features

### 🖼️ Image Viewing

- **Supported Formats**: JPG, JPEG, PNG, BMP, GIF, WebP (animation supported), AVIF, HEIC, JPEG XL, SVG, PSD, ICO, DNG, NEF, CR2, ARW, TGA, PCX, etc.
- **High-Quality Rendering**: Fast image drawing using SkiaSharp
- **Zoom**: Zoom in/out using Ctrl + Mouse Wheel
- **Drag & Drop**: Open images and folders by dropping them into the window

### 🔖 Bookmark Feature

- **Add to Favorites**: Save frequently viewed folders as bookmarks
- **Quick Access**: Instantly switch folders from the side panel or context menu
- **Reordering**: Intuitive reordering by drag & drop within the bookmark panel
- **Multilingual Support**: Automatic switching between Japanese and English display

### 🖼️ View Modes

- **Single View**: Display a single image filling the screen
- **Split View (Manga Mode)**: Display two pages side by side. Supports right-to-left reading order and spread offset correction (Shift + Page Turn).
- **Quad View**: Display 4 images in a grid (2x2) or horizontally (1x4). Also supports automatic layout based on aspect ratio.
- Cycle through modes sequentially using `Ctrl+G`

### 🔲 Grid Mode

- Display all images in a folder as thumbnails
- **Dynamic Layout Optimization**: Automatically calculate the layout where images appear largest according to the number of files and window size
- **Aspect Ratio Support**: Optimize grid cell aspect ratios based on the most common aspect ratio in the folder
- **Smart Fallback**: Automatically switch to scroll mode if there are too many files
- Click a thumbnail to transition to normal view
- Toggle with `Enter` (customizable)

### 📂 Folder Navigation

- Automatically traverse the folder tree with a depth-first search
- Move to previous/next folders with Up/Down keys (in normal mode)
- Navigate folders from the first/last row using Up/Down keys in grid mode

### ⏱️ Autoplay (Slideshow)

- Automatically transition images at specified intervals (in seconds)
- **Crossfade**: Apply smooth fade effects during image transitions
- **Flexible Search Scope**: Supports options to include the next folder, sibling folders, and subfolders automatically, in addition to loop and random playback
- Auto-transition to full screen option included
- Press `A` to display the settings dialog and start

### 🛠️ Non-Destructive Image Editing

- **In-Memory Editing**: Process images in real-time while viewing without modifying the original files
- **Tone Adjustment**: Fine-tune brightness, contrast, and gamma with sliders. Changes are reflected instantly.
- **Rotate/Flip**: Rotate in 90-degree increments, and flip horizontally/vertically
- **Filters**: Apply color filters such as grayscale, sepia, and negative invert
- **Target Specification**: During split view, accurately identify the right-clicked image as the editing target. Manipulate specific images while maintaining the display position.
- **Undo/Redo**: Manage edit history and revert to the original state at any time
- Intuitive undo/redo using `Ctrl+Z` / `Ctrl+Y`
- Save as a new file or overwrite the edited state

### 🖨️ Printing

- **Print Preview**: Call the OS standard print dialog to check the preview before printing
- **Auto Size Adjustment**: Automatically fit the image within the printable area of the paper while maintaining the aspect ratio

### 🎨 UI/UX

- **Mica Backdrop**: Modern translucent design of Windows 11
- **Custom Title Bar**: Title bar design that blends seamlessly into the app
- **Full Screen**: Toggle full screen display with a double click or `F` key
- **Area Selection**: Drag to select an area and right-click to copy
- **State Preservation**: Restore window size, position (including maximized state), and the currently viewed image on the next startup
- **Pointer-Aware Operations**: When multiple images are displayed, operations like rotation, deletion, and copying target the image directly under the mouse cursor.

### ⌨️ Keybindings

| Action | Default Key |
| --- | --- |
| Next Image | `Space` |
| Previous Image | `BackSpace` |
| Next Folder | `↓` |
| Previous Folder | `↑` |
| Toggle Manga Mode | `Ctrl+G` |
| Toggle Grid Mode | `Enter` |
| Autoplay (Slideshow) | `A` |
| Toggle Bookmark Panel | `B` |
| Toggle Metadata Display | `I` |
| **Toggle Fullscreen** | `F` |
| **Zoom In / Out** | `Ctrl + +` / `Ctrl + -` |
| **Reset Zoom (Fit)** | `Ctrl + 0` |
| **Actual Size (100%)** | `Ctrl + 1` |
| **Rotate Right / Left** | `R` / `L` |
| **Flip Horizontal** | `H` |
| **Bookmark Folder** | `D` |
| **Copy File Path** | `Ctrl + C` |
| **Delete File** | `Delete` |
| Exit App | `Escape` |

- Mouse Wheel: Move to previous/next image (Press Shift to turn 1 page in manga mode)
- Left/Right Cursor Keys: Move to previous/next image (Direction is reversed in manga mode, Press Shift to turn 1 page)
- All keybindings can be customized from the "Settings" in the context menu

### 🖱️ Context Menu

- **Save As**: Save in the specified format (JPEG, PNG, WebP, BMP)
- **Overwrite**: Overwrite and save
- **Print**: Print images fitted to paper size
- **Crop**: Crop the selected area
- **Resize**: Resize the image
- **Rotation/Flip**: Rotate and flip the image
- **Tone Adjustment**: Adjust brightness, contrast, etc.
- **Filters**: Apply color filters
- **Undo/Redo**: Undo and redo editing operations
- **Show Metadata**: Detailed display of EXIF info, etc. (Cycle with `I` key)
- **Bookmark**: Add/remove current folder from bookmarks
- **Open in Explorer**: Open file location in Explorer
- **Settings**: Change keybindings and display settings

## Requirements

- Windows 10 (1809) or later
- .NET 8.0

## Build

```bash
dotnet build -c Release -a x64
```

## Tech Stack

- **UI Framework**: WinUI 3 (Windows App SDK 2.0)
- **Image Rendering**: SkiaSharp 3.x
- **Language**: C# (.NET 8.0)
- **Settings Management**: Persistence via JSON format (LocalApplicationData)
