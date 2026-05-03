# Changelog

All notable changes to this project will be documented in this file.

## v1.1.0 (Current) - 2026-05-03
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
