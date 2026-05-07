# Changelog

All notable changes to this project will be documented in this file.

## v1.4.3 (Current) - 2026-05-07

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
