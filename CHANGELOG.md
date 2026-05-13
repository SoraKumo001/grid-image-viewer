# Changelog

All notable changes to this project will be documented in this file.

## v1.5.10 - 2026-05-13

- **Reading Direction & Layout Improvements**:
  - [Feature] Added a "Reading Direction" toggle for multi-page layouts (Manga/Quad mode), allowing users to switch between "Right to Left" (Manga) and "Left to Right" (Western) display orders.
  - [Feature] Added a customizable keyboard shortcut (Ctrl+R by default) to quickly toggle the reading direction.
  - [UI/UX] Arrow key navigation now dynamically adapts to the selected reading direction (e.g., in Left to Right mode, the Right Arrow goes to the next page).
  - [Fix] Fixed an issue where switching split modes could result in visual gaps or incorrect image alignment due to incomplete layout resets and timing conflicts.

## v1.5.9 - 2026-05-13

- **PDF Support (Virtual Folder Mode)**:
  - [Feature] Implemented comprehensive PDF support: PDF files are now treated as "virtual folders," where each page is expanded as an individual high-quality image in the playlist.
  - [Feature] Integrated Windows native PDF rendering engine (`Windows.Data.Pdf`) to ensure fast and reliable page display.
  - [UI/UX] Added PDF thumbnail support for Grid Mode, allowing users to preview PDF pages in the gallery view.
  - [Integration] Enabled PDF file association: apps now appear in the "Open with" menu for PDF files, and dragging/dropping a PDF into the app correctly loads all its pages.
  - [Navigation] Improved folder navigation: "Next/Previous Folder" actions now correctly include PDF files as valid navigation targets.
- **Settings & Localization**:
  - [Feature] Added a dedicated "Documents" group in settings to toggle PDF support.
  - [Localization] Updated English and Japanese resource files to include new PDF-related UI labels.

## v1.5.8 - 2026-05-12

- **Save As & Menu Improvements**:
  - [Improvement] Overhauled "Save As" functionality to allow users to select the target file format (JPEG, PNG, WebP, BMP) directly within the standard file save dialog, rather than choosing it from a sub-menu.
  - [UI/UX] Improved context menu targeting: operations like "Save As", "Crop", "Resize", and "Tone Adjustment" now correctly target the specific image under the mouse cursor in multi-view (Manga/Quad) modes.
- **Image Editing & Stability**:
  - [Fix] Fixed a critical issue where image transformations (Resize, Filters, Tone Adjustment) and Undo/Redo operations were not visually updating the UI.
  - [Optimization] Improved "Tone Adjustment" responsiveness: eliminated image flickering during slider movement by optimizing the re-rendering pipeline.
  - [Improvement] Refined Undo history for Tone Adjustment: multiple adjustments within a single session are now treated as a single undoable action to prevent history bloat.
- **Context Menu Context-Awareness**:
  - [Logic] Implemented smarter menu state management: save and edit actions are now automatically disabled for video files or files within archives where such operations are not supported.
  - [Fix] Fixed a bug where Overwrite would not correctly clear the editing session, leading to stale data when returning to the same image.

## v1.5.7 - 2026-05-12

- **Performance & Grid Improvements**:
  - [Optimization] Improved thumbnail loading performance in Grid Mode.
  - [Optimization] Offloaded image metadata extraction (dimensions and file size) to background threads, preventing UI micro-stutters during folder navigation.
- **Video & Archive Enhancements**:
  - [Feature] Integrated FFmpeg-based frame extraction for video thumbnails, significantly improving thumbnail reliability and quality for various formats.
  - [Feature] Added support for extracting video thumbnails directly from files located within archives (ZIP/RAR).
  - [Stability] Implemented a semaphore-based throttling mechanism for video thumbnail generation to prevent GPU/CPU resource exhaustion.

## v1.5.6 - 2026-05-11

- **Bug Fixes**:
  - [Fix] Fixed an issue where the aspect ratio of video files was ignored in the 4-split layout's "Auto (Aspect Ratio)" mode. Now correctly fetches actual video dimensions and respects rotation metadata.

## v1.5.5 - 2026-05-11

- **Store Compliance & Permissions**:
  - [Security] Updated permissions to align with Microsoft Store security policies while maintaining full functionality.
- **Deployment & Distribution**:
  - [Improvement] Transitioned to a **Self-contained** deployment model, including the **.NET Desktop Runtime** directly in the package to ensure a seamless "out-of-the-box" experience for all users.

## v1.5.4 - 2026-05-10

- **Settings & Localization**:
  - [Feature] Implemented a modular settings overlay with localization support and various customization options.
- **Performance & Caching**:
  - [Feature] Introduced `ViewerCacheManager` for background preloading, significantly improving navigation responsiveness.
  - [Feature] Updated playlist navigation to leverage preloaded folder data.

## v1.5.3 - 2026-05-10

- **Localization & App Identity**:
  - [Feature] Added localized resource files for English (en-US) and Japanese (ja-JP) to support multi-language application environments.

## v1.5.2 - 2026-05-10

- **UI Improvements**:
  - [Improvement] Refined the synchronization between display logic and layout transitions within the viewer.

## v1.5.1 - 2026-05-09

- **New Features & UI Improvements**:
  - [Feature] Added a **"Reset Pan & Zoom"** button to the top operation panel, allowing for quick restoration of image scale and position.
  - [Improvement] Enhanced **Zoom Reset (Ctrl + 0)** to support both the number row and Numpad 0.
  - [Improvement] Improved keyboard compatibility for Zoom In/Out shortcuts.
  - [Improvement] Integrated the **Top Operation Panel** into the main viewer for easier access to common actions.
- **Technical Fixes & Stability**:
  - [Fix] Optimized Zoom Reset behavior to ensure immediate visual updates.
  - [Fix] Resolved build errors related to interface type mismatches.

## v1.5.0 - 2026-05-09

- **UI & UX Enhancements**:
  - [Improvement] Refined Bookmark Panel behavior: the panel now automatically closes after selecting a bookmark.
  - [Improvement] Enhanced Bookmark Panel auto-show logic to prevent accidental UI overlapping during video playback.
- **Video Playback Improvements**:
  - [Fix] Improved Video Transport Controls reliability: controls respond more consistently to mouse movement and clicks.
  - [Fix] Fixed a rendering issue where video controls could fail to receive input events.

## v1.4.9 - 2026-05-09

- **Stability & Bug Fixes**:
  - [Fix] Fixed an issue where images failed to load during fast backward navigation.
  - [Fix] Fixed an application crash caused by background tasks attempting to read UI-bound properties.
  - [Fix] Resolved a bug where the viewer would occasionally display a black screen after stopping a slideshow.

## v1.4.8 - 2026-05-09

- **Slideshow Enhancements**:
  - [Feature] Added "Stretch Mode" selection to the slideshow setup dialog.
  - [Stability] Fixed several crashes and mapping issues in the slideshow dialog.

## v1.4.7 - 2026-05-09

- **Grid Mode Enhancements**:
  - [Feature] Implemented a dedicated context menu (right-click) for Grid Mode.
  - [Improvement] Tailored the grid context menu to include grid-specific actions like "Refresh Thumbnails" and "Sort By".
- **File Management Features**:
  - [Feature] Added "Rename" functionality for files.
  - [Feature] Added "Move to Folder" functionality to relocate files.
  - [Improvement] Enhanced the Delete confirmation dialog to display the target filename.
  - [Security] Implemented guards to disable file modification operations for files within archives.

## v1.4.6 - 2026-05-09

- **Stability & Crash Fixes**:
  - [Fix] Fixed an application crash when attempting to open multiple Autoplay dialogs.
  - [Stability] Improved media player control re-initialization for better long-term stability.
  - [Fix] Added global guards to prevent multiple overlapping overlays.
- **Performance Optimization**:
  - [Optimization] Disabled redundant video thumbnail extraction during slideshows to save CPU.
  - [Optimization] Streamlined the video loading sequence for cleaner transitions.

## v1.4.5 - 2026-05-08

- **Slideshow System**:
  - [Feature] Introduced a robust slideshow lifecycle management system.
  - [Fix] Resolved video playback issues during slideshow transitions.
- **Media Rendering & Performance**:
  - [Feature] Implemented enhanced media playback and high-performance rendering.
  - [Feature] Added a thumbnail panel with smooth layout animations and optimized scroll tracking.

## v1.4.4 - 2026-05-07

- **Video Stability & GPU Hardening**:
  - [Stability] Improved mutual exclusion to prevent resource contention during rapid page switching.
  - [Stability] Implemented a initialization lock to reduce GPU driver hangs and crashes.
- **UI & UX Improvements**:
  - [Fix] Resolved playback transport panel visibility issues on non-video files.
  - [Improvement] Eliminated visual "jumping" of video position and size.
  - [Improvement] Enhanced playback transitions with better opacity control during loading.
- **Visual & Rendering Fixes**:
  - [Visual] Unified background color to Black, eliminating gray borders.
  - [Fix] Fixed race conditions and ghosting effects during video playback.

## v1.4.3 - 2026-05-07

- **Video Playback & Settings**:
  - [Feature] Added a **Video Master Volume** slider in settings.
  - [Improvement] Video volume settings are now applied in real-time.
- **UI & Navigation**:
  - [Fix] Resolved an issue where the "Searching..." overlay was not correctly displayed.

## v1.4.2 - 2026-05-06

- **Bug Fixes & Stability**:
  - [Fix] Resolved critical launch issues related to file layout.
  - [Fix] Fixed Inno Setup script compilation errors.

## v1.4.1 - 2026-05-06

- **Deployment & Distribution**:
  - [Feature] Added support for **Inno Setup** to generate a non-packaged installer (`.exe`).
  - [Improvement] The installer runs with **user-level privileges**, no admin required.

## v1.4.0 - 2026-05-06

- **Navigation & Media Support**:
  - [Fix] Resolved an issue where folders containing only video files were skipped.
  - [Improvement] Integrated extension filters into folder navigation logic.
- **Settings & UI**:
  - [Feature] Added "ALL" and "NONE" toggle buttons for extension groups in settings.
  - [Improvement] Optimized extension filtering to apply changes immediately.

## v1.3.1 - 2026-05-06

- **Slideshow Optimization**:
  - [Fix] Resolved redundant crossfade effects during slideshow playback.
  - [Improvement] Optimized display update logic to prevent unnecessary re-rendering.

## v1.3.0 - 2026-05-05

- **General Improvements**:
  - [Branding] Renamed project internal naming to fully reflect the application's identity.
  - [Cleanup] Streamlined the project by consolidating UI components.

## v1.2.2 - 2026-05-04

- **Compliance & Optimization**:
  - [Fix] Corrected splash screen and icon dimensions.
  - [Feature] Implemented background preloading for folders, eliminating wait times.
- **Branding & Visuals**:
  - [Improvement] Overhauled application icon transparency for a cleaner look.

## v1.2.1 - 2026-05-04

- **Navigation Improvements**:
  - [Improvement] Enhanced folder navigation to verify content within archives.
  - [Fix] Resolved notification suppression bugs.
- **Enhanced Persistence**:
  - [Feature] Implemented folder memory to remember the last viewed directory.

## v1.2.0 - 2026-05-03

- **Archive Support**: [Feature] Added direct viewing support for **ZIP, CBZ, RAR, CBR, and 7z** archives.
- **Window Persistence**: [Feature] Implemented window size and position preservation across sessions.
- **UI/UX Improvements**:
  - Added on-screen overlay notifications for mode changes.
  - Added keybinding (`S`) to cycle through Stretch Modes.
  - Improved right-click menu accessibility.
- **Technical Improvements**:
  - [Feature] Implemented pointer-aware keybindings for targeted image actions.

## v1.1.0 - 2026-05-03

- **App Renaming**: Renamed the application to "**Quick Image Viewer**".
- **Printing Support**: Implemented image printing functionality with auto-size adjustment.
- **Bug Fixes**:
  - Fixed blank print previews.
  - Resolved hangs during preview generation.

## v1.0.3 - 2026-05-03

- **Slideshow Enhancements**: Improved settings dialog and added full localization.
- **Image Editing**: [Feature] Introduced the `CropOverlay` for area selection.
- **Key Bindings**: [Feature] Implemented configuration UI for custom key bindings.

## v1.0.2 - 2026-05-02

- **New View Modes**: Added Quad-split (4-up) view mode.
- **Metadata Overlay**: Implemented an image information panel (EXIF data).
- **Advanced Slideshow**: Added crossfade transitions and folder inclusion options.

## v1.0.1 - 2026-05-02

- **Stability**: Added thread safety to rendering operations.

## v1.0.0 - 2026-05-02

- **Initial Release**: Basic image viewing features with Grid and Manga Mode support.
- **Format Support**: Support for AVIF, WebP, JPEG XL and more.


