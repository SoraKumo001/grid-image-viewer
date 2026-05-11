# Changelog

All notable changes to this project will be documented in this file.

## v1.5.6 - 2026-05-11

- **Cleanup & Optimization**:
  - [Maintenance] Removed unnecessary debug log outputs and unused exception variables to improve code cleanliness.
  - [Refactor] Simplified search state management by leveraging `ObservableProperty` in `ViewerStateService`.
- **Bug Fixes**:
  - [Fix] Fixed an issue where the aspect ratio of video files was ignored in the 4-split layout's "Auto (Aspect Ratio)" mode. Now correctly fetches actual video dimensions and respects rotation metadata.

## v1.5.5 - 2026-05-11

- **Store Compliance & Permissions**:
  - [Refactor] Migrated from `Windows.Storage` APIs to standard `.NET System.IO` across all core services (`ViewerImageLoader`, `MetadataService`, `PrintService`, `ImageProcessor`) to eliminate the need for broad file system access permissions.
  - [Security] Removed the `broadFileSystemAccess` capability to align with Microsoft Store security policies while maintaining full functionality via `runFullTrust`.
- **Deployment & Distribution**:
  - [Improvement] Transitioned to a **Self-contained** deployment model, including the **.NET Desktop Runtime** directly in the package to ensure a seamless "out-of-the-box" experience for all users.

## v1.5.4 - 2026-05-10

- **Settings & Localization**:
  - [Feature] Implemented a modular settings overlay with localization support and various customization options (5869f0d).
- **Performance & Caching**:
  - [Feature] Introduced `ViewerCacheManager` for background image byte and `SoftwareBitmap` preloading, significantly improving navigation responsiveness (eb40bea).
  - [Feature] Established a comprehensive image caching infrastructure and updated playlist navigation to leverage preloaded folder data (20446fa).
- **State & Playlist Management**:
  - [Feature] Implemented `PlaylistManager` and `ViewerStateService` to centralize management of navigation, folder switching, and application state tracking (fc624e3, dcb431e).

## v1.5.3 - 2026-05-10

- **Localization & App Identity**:
  - [Feature] Added localized resource files for English (en-US) and Japanese (ja-JP) to support multi-language application environments (7d37f68).
  - [Feature] Integrated `Package.appxmanifest` to formally define application identity, capabilities, and file type associations (b6a7593).
- **Core Architecture**:
  - [Feature] Introduced `ViewerManager` to centralize UI display logic and streamline buffer management, improving coordination between different view states (88503f5).

## v1.5.2 - 2026-05-10

- **Core Architecture & Refactoring**:
  - [Feature] Initialized WinUI 3 project configuration and integrated core dependencies for improved stability.
  - [Feature] Established core interfaces, managers, and the main window structure to support a modular architecture.
  - [Refactor] Overhauled `ViewerManager` and `SlideshowManager` to decouple display logic from slideshow state management.
  - [Internal] Implemented a new core architecture for `ViewerManager` to handle page rendering, layout management, and buffer synchronization more efficiently.
  - [Improvement] Refined the synchronization between display logic and layout transitions within the viewer.

## v1.5.1 - 2026-05-09

- **New Features & UI Improvements**:
  - [Feature] Added a **"Reset Pan & Zoom"** button to the top operation panel, allowing for quick restoration of image scale and position.
  - [Improvement] Enhanced **Zoom Reset (Ctrl + 0)** to support both the number row and Numpad 0.
  - [Improvement] Improved keyboard compatibility for Zoom In/Out shortcuts, supporting various key combinations across different keyboard layouts.
  - [Improvement] Integrated the **Top Operation Panel** into the main viewer for easier access to common navigation and viewing actions.
- **Architecture & Refactoring**:
  - [Refactor] Migrated to a **Modular Service Architecture**, extracting window management, view logic, and media loading into dedicated services (`AppWindowManager`, `ViewerImageLoader`, etc.).
  - [Refactor] Centralized input processing into a new `InputHandler`, unifying pointer, mouse wheel, and keyboard event handling.
- **Technical Fixes & Stability**:
  - [Fix] Optimized Zoom Reset behavior to ensure both zoom factor and pan position are reset immediately without visual delay.
  - [Fix] Resolved build errors related to interface type mismatches in `IMainView`.
  - [Internal] Improved multi-language support and updated localized resources for new UI components.

## v1.5.0 - 2026-05-09

- **UI & UX Enhancements**:
  - [Improvement] Refined Bookmark Panel behavior: the panel now automatically closes after selecting a bookmark to provide a cleaner transition to the new folder.
  - [Improvement] Enhanced Bookmark Panel auto-show logic: automatic display is now suppressed when the mouse is hovering over video transport controls, preventing accidental UI overlapping.
- **Video Playback Improvements**:
  - [Fix] Improved Video Transport Controls reliability: controls now appear immediately upon video load and respond more consistently to mouse movement and clicks.
  - [Fix] Fixed a rendering issue where video controls could fail to receive input events due to missing background hit-testing.
- **Technical Refinement**:
  - [Internal] Improved message-based state synchronization for the Bookmark Panel, allowing for explicit visibility control.

## v1.4.9 - 2026-05-09

- **Architecture & Refactoring**:
  - [Refactor] Extracted video playback logic and FFmpeg integration from `ViewerPageControl` into a dedicated `VideoPlayerControl` for better maintainability. 
  - [Refactor] Separated file management logic (Delete, Rename, Move) from `MainWindow` into a new `FileOperationService`.
  - [Refactor] Refactored `ViewerManager` by splitting complex layout and buffer management methods into smaller, manageable asynchronous operations.
- **Stability & Bug Fixes**:
  - [Fix] Fixed an issue where images failed to load during fast backward navigation (Shift + Back) in split view due to `SoftwareBitmap` cache ownership conflicts.
  - [Fix] Fixed an application crash (`RPC_E_WRONG_THREAD`) caused by background tasks attempting to read UI thread-bound properties.
  - [Fix] Resolved a bug where the viewer would occasionally display a black screen after stopping a slideshow.

## v1.4.8 - 2026-05-09

- **Slideshow Enhancements**:
  - [Feature] Added "Stretch Mode" selection to the slideshow setup dialog, allowing users to choose a specific display size (Keep Current, Contain, Cover, or Original Size) for slideshow playback.
  - [Stability] Fixed a `XamlParseException` related to resource ID mapping in the slideshow dialog.
  - [Stability] Fixed a `NullReferenceException` caused by unsafe type unboxing in the slideshow stretch mode selection.

## v1.4.7 - 2026-05-09

- **Grid Mode Enhancements**:
  - [Feature] Implemented a dedicated context menu (right-click) for Grid Mode. 
  - [Improvement] Tailored the grid context menu to exclude irrelevant image editing items (e.g., Crop) and include grid-specific actions like "Refresh Thumbnails" and "Sort By".
  - [Improvement] Optimized keyboard focus management specifically for Grid Mode.
- **File Management Features**:
  - [Feature] Added "Rename" functionality for files (available via context menu and keyboard shortcuts).
  - [Feature] Added "Move to Folder" functionality to relocate files to a different directory.
  - [Improvement] Enhanced the Delete confirmation dialog to display the target filename, preventing accidental deletions.
  - [Security] Implemented guards to disable file modification operations (Delete, Rename, Move) for files within archives.
- **Security & Stability**:
  - [Security] Updated `SharpCompress` library to v0.48.0 to resolve known vulnerabilities.
  - [Fix] Corrected XAML configuration issues that caused build failures.       
  - [Improvement] Refactored bookmark menu synchronization to ensure consistent state across both Viewer and Grid mode menus.

## v1.4.6 - 2026-05-09

- **Stability & Crash Fixes**:
  - [Fix] Fixed an application crash (COMException 0x80000019) that occurred when attempting to open the Autoplay (slideshow) dialog via shortcut while it was already open.
  - [Stability] Implemented a `MediaPlayerElement` re-creation strategy. Re-initializing the media player control for each new video ensures a clean state and reliable resource disposal, improving long-term stability.
  - [Fix] Added global guards in `DialogService` to prevent multiple overlapping overlays from being displayed simultaneously.
- **Performance Optimization**:
  - [Optimization] Disabled redundant video thumbnail extraction in `ViewerCacheManager` during normal navigation and slideshow modes. This significantly reduces CPU and disk I/O when thumbnails are not required for display.
  - [Optimization] Streamlined the video loading sequence in `ViewerImageLoader` by removing intermediate thumbnail rendering, resulting in faster and cleaner playback transitions.
  - [Refactor] Refined asynchronous background image caching and decoding logic to improve memory efficiency and responsiveness.
- **Media & UI Improvements**:
  - [Feature] Enhanced `ViewerPageControl` with more robust media playback state handling and custom transport control synchronization.
  - [Improvement] Improved the reliability of the core loading architecture to handle rapid page switching and mixed-media playlists more effectively.

## v1.4.5 - 2026-05-08

- **Core Architecture & Refactoring**:
  - [Refactor] Significant overhaul of the application core, initializing a robust architecture with centralized `MainWindow`, `ViewModels`, and specialized managers.
  - [Refactor] Re-implemented playlist, viewer, and cache management systems to improve navigation speed and directory loading performance.
- **Slideshow System**:
  - [Feature] Introduced `SlideshowManager` to handle the entire slideshow lifecycle, including configuration persistence and state synchronization with the main viewer.
  - [Fix] Resolved video playback issues during slideshow transitions.
- **Media Rendering & Performance**:
  - [Feature] Implemented `ViewerPageControl` for enhanced media playback and high-performance rendering support.
  - [Feature] Introduced `GridManager` for the thumbnail panel, featuring smooth layout animations and optimized scroll tracking.
  - [Feature] Re-engineered the image and video loading architecture with native FFmpeg integration and advanced cache management.
- **Video Stability**:
  - [Fix] Fixed video playback bugs in both normal and slideshow modes to ensure consistent behavior across all view states.
- **CI/CD**:
  - [Feature] Added a new GitHub Actions workflow for automated Windows builds and release management.

## v1.4.4 (Current) - 2026-05-07

- **Video Stability & GPU Hardening**:
  - [Stability] Introduced asynchronous reset processing (`ResetPlaybackAsync`) and `SemaphoreSlim`-based mutual exclusion to prevent resource contention during rapid page switching.
  - [Stability] Implemented a **Global Initialization Lock** to serialize video decoder startups across the application, significantly reducing GPU driver hangs and crashes.
  - [Stability] Added a short "cooldown" delay during resource disposal, allowing the OS and drivers sufficient time to safely release hardware handles.        
  - [Fix] Explicitly implemented `Dispose()` for WinRT resources such as `SoftwareBitmapSource` to prevent memory leaks and GPU memory exhaustion.
- **UI & UX Improvements**:
  - [Fix] Resolved an issue where the playback transport panel would incorrectly appear on non-video files (e.g., WebP, GIF, or edited images) during mouse movement.
  - [Improvement] Eliminated the visual "jumping" effect of video position and size by forcing synchronous layout updates immediately after a video is opened.  
  - [Improvement] Enhanced playback transitions by controlling opacity during the loading phase, preventing incomplete or uninitialized frames from being visible to the user.
- **Visual & Rendering Fixes**:
  - [Visual] Unified the background color of page containers and buffer grids to **Black**, eliminating the "gray borders" previously visible in the margins or during content loading.
  - [Fix] Fixed a race condition where thumbnails could remain visible behind an active video, preventing ghosting or "afterimage" effects.
- **Performance Optimization**:
  - [Optimization] Reduced the layout recalculation debounce timer from 150ms to **30ms**, dramatically improving responsiveness during window resizing operations.
  - [Optimization] Streamlined the video source assignment process to reduce main thread overhead during simultaneous multi-video loading.

## v1.4.3 - 2026-05-07

- **Video Playback & Settings**:
  - [Feature] Added a **Video Master Volume** slider in the General Settings (Display tab), allowing global control over video playback volume.
  - [Improvement] Video volume settings are now applied in real-time to currently playing videos.
  - [Stability] Optimized GPU resource management for video playback by streamlining `SetSurfaceSize` calls and improving resource cleanup during content switching.
  - [Fix] Implemented temporary video pausing during Fullscreen transitions to prevent GPU hangs on certain hardware configurations.
- **UI & Navigation**:
  - [Fix] Resolved an issue where the "Searching for images..." overlay was not correctly displayed during folder navigation or background discovery.
  - [Improvement] Enhanced the layout and visibility of the folder searching overlay for better user feedback.
  - [Fix] Synchronized searching state across the ViewModel and core services to ensure consistent UI behavior.
- **Bug Fixes**:
  - [Fix] Corrected a compilation error in `ViewerPageControl` related to missing dependency injection namespaces.

## v1.4.2 - 2026-05-06

- **Bug Fixes & Stability**:
  - [Fix] Resolved a critical issue where both ZIP and Installer versions failed to launch due to an incorrect file layout in the distribution package.
  - [Fix] Fixed a compilation error in the Inno Setup script caused by an invalid flag, which prevented successful GitHub Actions runs.
  - [Improvement] Restored standard build output paths to ensure reliable WinUI 3 unpackaged application bootstrapping.

## v1.4.1 - 2026-05-06

- **Deployment & Distribution**:
  - [Feature] Added support for **Inno Setup** to generate a non-packaged installer (`.exe`) for easier installation.
  - [Improvement] The installer is configured to run with **user-level privileges**, allowing installation without administrator rights.
  - [Feature] Integrated installer generation into the GitHub Actions CI/CD workflow; the installer is now automatically attached to new releases.
  - [Improvement] Transitioned to a `dotnet publish` based build process for more reliable self-contained distribution.
- **Project Maintenance**:
  - [Refactor] Standardized the build output directory structure for consistent packaging across ZIP and Installer formats.

## v1.4.0 - 2026-05-06

- **Core Architecture**:
  - [Refactor] Completed the transition to a fully decoupled MVVM architecture using `CommunityToolkit.Mvvm`, improving testability and code separation.        
  - [Refactor] Componentized the UI by splitting large views into reusable controls (`ViewerPanel`, `GridImagePanel`, etc.).
  - [Improvement] Centralized cross-component communication via `WeakReferenceMessenger`.
- **Navigation & Media Support**:
  - [Fix] Resolved an issue where folders containing only video files were skipped during folder navigation.
  - [Improvement] Integrated extension filters into folder navigation logic; folders containing only disabled extensions are now correctly skipped.
  - [Fix] Fixed navigation logic to correctly handle mixed-media playlists (images, videos, and archives) in multi-page view modes.
- **Settings & UI**:
  - [Feature] Added "ALL" and "NONE" toggle buttons for each extension group (Images, Videos, Archives) in the settings overlay for easier configuration.       
  - [Improvement] Optimized extension filtering to apply changes immediately to the active playlist and background preloading.
  - [Fix] Enhanced focus management to ensure keyboard navigation remains active after closing overlays or interacting with video controls.
- **Stability & Performance**:
  - [Fix] Resolved critical WinRT exceptions related to `ResourceLoader` and `XamlRoot` access by ensuring proper visual tree attachment and thread-safe resource retrieval.
  - [Fix] Fixed potential crashes in `SlideshowService` and `ViewerManager` related to bounds checking and empty playlists.
- **Project Maintenance**:
  - Updated target framework to **.NET 10.0**.
  - Updated README documentation to reflect the new architecture and features.  

## v1.3.1 - 2026-05-06

- **Slideshow Optimization**:
  - [Fix] Resolved an issue where a redundant crossfade effect occurred during slideshow playback when the playlist was updated by background file discovery.   
  - [Improvement] Optimized the display update logic to prevent unnecessary re-rendering when the currently displayed images remain unchanged.
- **Layout & Rendering**:
  - [Feature] Implemented end-of-folder alignment for multi-view modes (Double/Quad), ensuring a full grid is displayed when reaching the end of a playlist.    
  - [Improvement] Enhanced display synchronization between buffers to ensure seamless transitions and consistent state across different view modes.

## v1.3.0 - 2026-05-05

- **Architecture Overhaul**:
  - [Refactor] Restructured the entire codebase into a modular directory hierarchy (`Interfaces`, `Services`, `Managers`, `Models`, `ViewModels`, `Helpers`, `Views`), significantly improving project maintainability and alignment with MVVM patterns.
  - [Branding] Migrated the internal namespace and project naming from `grid_image_viewer` to `quick_image_viewer` to fully reflect the application's identity. 
  - [Cleanup] Streamlined the project by consolidating UI components and removing redundant legacy control files.
- **Platform & Build**:
  - Updated project configuration for .NET 10 and Windows App SDK 2.0 stability.
  - Optimized build process and assembly naming consistency.

## v1.2.2 - 2026-05-04

- **WACK Compliance & Optimization**:
  - [Fix] Corrected splash screen (scale-200) and lock screen logo dimensions to meet Windows App Certification Kit (WACK) requirements.
  - [Feature] Implemented background preloading for the next and previous folder's image playlists, eliminating wait times during navigation.
  - [Optimization] Removed redundant directory re-scanning during navigation by utilizing preloaded playlist data for instant transitions.
- **Branding & Visuals**:
  - [Improvement] Overhauled the application icon transparency using a flood-fill algorithm, preserving facial details while ensuring a clean background.       
  - [Visual] Optimized margins across all assets to maximize the visual size and prominence of the application icon.

## v1.2.1 - 2026-05-04

- **Navigation Improvements**:
  - Enhanced folder navigation to verify image content within archives before switching, preventing dead-end navigation to empty or non-image ZIP files.        
  - Resolved a race condition where the "Reached first/last folder" notification was occasionally suppressed.
- **Enhanced Persistence**:
  - Implemented `LastDirectoryPath` persistence to ensure the current folder is remembered even if the session ends without an active image (e.g., empty playlist).
  - Improved application startup to restore the previous folder as a fallback if the last viewed image file is no longer available.

## v1.2.0 - 2026-05-03

- **Archive Support**: Added direct viewing support for **ZIP, CBZ, RAR, CBR, and 7z** archives using `SharpCompress`.
- **Window Persistence**: Implemented window size and position preservation across sessions (Restored/Normal state only).
- **High-Quality Scaling**: Added a "High Quality Scaling" option (Lanczos-like interpolation) for improved image quality when zoomed.
- **Background Customization**: Added support for System (Mica), Black, and White background modes.
- **Settings Portability**: Implemented JSON-based Export and Import functionality for application settings.
- **UI/UX Improvements**:
  - Added on-screen overlay notifications for mode changes (View Mode, Stretch Mode, Slideshow).
  - Added a dedicated keybinding (`S`) to cycle through Stretch Modes (Contain, Cover, Original).
  - Improved Right-click menu accessibility by moving Settings to the root level.
- **Technical Improvements**:
  - Replaced deprecated SkiaSharp `FilterQuality` with `SKSamplingOptions` to align with SkiaSharp 3.x standards.
  - Implemented pointer-aware keybindings: rotation, deletion, and path copying now target the image directly under the mouse cursor in multi-view modes.       
- **Bug Fixes**:
  - Prevented window state corruption when exiting in maximized or fullscreen modes.
  - Fixed a bug where double-clicking on dialogs would trigger fullscreen mode. 
  - Improved file lock management by navigating to the next image before deleting a file.

## v1.1.0 - 2026-05-03

- **App Renaming**: Renamed the application from "Grid Image Viewer" to "**Quick Image Viewer**".
- **Printing Support**: Implemented image printing functionality with automatic size adjustment to fit paper margins.
- **Support Menu**: Added a "Support" item to the context menu that opens the GitHub repository.
- **Bug Fixes**:
  - Fixed an issue where the print preview appeared blank.
  - Resolved a hang issue during print preview generation.
- **Documentation**: Updated README.md to reflect new features and the new application name.

## v1.0.3 - 2026-05-03

- **Slideshow Enhancements**: Improved the slideshow settings dialog and added full localization support for UI labels.
- **Image Editing Improvements**: Introduced the `CropOverlay` control for a better area selection experience and refactored `ImageEditService` session management.
- **Key Bindings UI**: Implemented a configuration UI to allow users to customize key bindings.
- **Architecture Refactoring**: Extracted animation and crossfade logic into a dedicated `AnimationService` class for better maintainability.
- **Localization**: Synchronized Japanese and English resource files across all UI components.

## v1.0.2 - 2026-05-02

- **New View Modes**: Added Quad-split (4-up) view mode with multiple layout options (Auto, 1x4, 2x2).
- **Metadata Overlay**: Implemented an image information panel to display EXIF data (Camera, Lens, Settings, Date).
- **Advanced Slideshow**: Added support for crossfade transitions and options to include subfolders or sibling directories.
- **Performance Optimization**: Improved image caching and offloaded encoding/decoding tasks to background threads to prevent UI hangs.
- **Modern UI**: Applied Windows 11 Mica backdrop and custom title bar design.  

## v1.0.1 - 2026-05-02

- **Stability**: Added thread safety to image rendering operations to prevent crashes during rapid navigation.
- **Assets**: Included official application icons and splash screen assets.     

## v1.0.0 - 2026-05-02

- **Initial Release**: Basic image viewing features with Grid Mode and Manga Mode support.
- **Format Support**: Wide range of image formats including AVIF, WebP, and JPEG XL.
- **CI/CD**: Established GitHub Actions workflow for automated builds and releases.
- **Store Readiness**: Configured project for Microsoft Store deployment.
