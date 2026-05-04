# Changelog

All notable changes to this project will be documented in this file.

## v1.2.2 (Current) - 2026-05-04

- **WACK Compliance & Optimization**:
  - [修正] Windows App Certification Kit (WACK) のリソース判定に基づき、スプラッシュ画面 (scale-200) とロック画面ロゴの解像度を規定サイズに修正
  - [高速化] 次・前のフォルダの画像リスト（プレイリスト）をバックグラウンドで先読みする機能を実装。移動時の待ち時間を解消
  - [修正] フォルダ移動時の再スキャンを廃止し、先読みデータを活用して瞬時のフォルダ遷移を実現
- **Branding & Visuals**:
  - [改善] アイコンの背景透過アルゴリズムを Flood Fill 方式に刷新。顔のディテールを維持したまま背景を完全透過
  - [更新] アセット全般の余白を最適化し、アイコンの表示サイズを最大化
  - Optimized icon margins for a larger, more prominent visual presence across all application assets (SplashScreen, Logo, etc.).

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
