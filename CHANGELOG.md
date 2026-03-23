## Change Log

### 8.X.XX (2026-01-XX)
- UI: On-brand background image and loader icon.

## [8.6.15] - 2026-03-23
### Added
- Exact Time Generator feature in Timer Mode to distribute a total time across achievements.
- Validation to ensure total time is sufficient for the number of achievements.
- Pseudo-random, sorted distribution for more natural unlock patterns.
- MaxLength validation for timer inputs.
### Fixed
- Fixed a race condition causing crashes when switching between games rapidly.
- Corrected various code style and semantic issues to align with project conventions.
- Resolved compilation errors related to missing `async` modifiers and incorrect type names during refactoring.
- Fixed an issue where the `ExactTimeButton` icon was not displaying due to an incorrect resource definition.

## [8.6.14] - 2026-03-23
- UI: Added Play Time and Last Played under the title of the game in the details view.
- Core: Implemented a `keep alive` timer to update Steam every 5 minutes, ensuring the total playtime is tracked correctly.

### 8.6.13 (2026-01-09)
- UX: Added Warning Module ("SLAM Alert") to prevent accidental actions.
  - Added warnings when attempting to open DLC content without the base game.
  - Added warnings when attempting to unlock achievements for uninstalled games.
  - Added warnings when attempting to instantly unlock multiple achievements (>2) to encourage safe usage.

### 8.6.12 (2026-01-07)
- UI: Merged status icons (Installed, Favorite) into a unified indicator area.
- UI: Added visual "Installed" indicator to game cards and the filter area.
- UX: Added "Not Favorite" icon state for better discoverability.

### 8.6.11 (2026-01-07)
- Core: Added "Smart Random" button for achievements randomization in timer mode.
  - Dynamically calculates jitter based on the input range.
  - Ensures values are naturally distributed across the achievement list (top -> min, bottom -> max).

### 8.6.10 (2026-01-05)
- Core: Favorites System.
  - Added interactive Star icon on game cards (hover to see, click to toggle).
  - Added "Favorites Only" filter to quickly access pinned games.
  - Added "Add to Favorites" option in the game context menu.
  - `config.json` for storing application settings and favorites.

### 8.5.10 (2026-01-04)
- Core: Now parsing `appinfo.vdf` directly to detect installed games, allowing games without achievements to be listed.
- UI: Replaced dropdown filters with segmented controls for better accessibility.
- UI: Added exclusive filters for games with and without achievements.

### 8.4.10 (2025-12-31)
- UI: Added background image on the achievement list.
- Core: Improved image caching logic (thread-safe, DRY, UI-thread safe).

### 8.4.9 (2025-12-28)
- UX: Added new sorting options to sort by Unlock Time (Newest/Oldest), prioritizing unlocked achievements while intelligently sorting remaining locked achievements by rarity.

### 8.4.8 (2025-12-28)
- Core: Single Instance Enforcement: Launching the app while it's already running now brings the existing window to the foreground.
- Core: Consolidated API libraries and Steam utilities into a unified `SAM.API` for better maintainability (Internal).

### 8.4.7 (2025-12-23)
- Core: Broken Achievement detection. Achievements with missing or zero-byte images now display a specific "Broken" icon with a tooltip explaining potential issues.
- Fix: Resolved application crash when switching between games, specifically when transitioning from games with no hidden achievements.
- Fix: Corrected "Reveal Hidden" button visibility logic and spacing uniformity.
- Core: Implemented robust crash logging. Critical and unhandled errors are now saved to `crash.log` for troubleshooting.

### 8.4.6 (2025-12-22)
- Core: Hidden achievement handling:
  - Hidden locked achievements now display as "Hidden Achievement" with a placeholder description and a hidden icon until unlocked or revealed.
  - Reveal Locked Hidden Achievements. Added a "Reveal Hidden" toggle button to reveal details of hidden achievements.
- UX: Filter by Locked/Unlocked/All. Replaced dropdown with segmented control buttons using new icons.
- Core: Enhanced image caching for hidden icons and faster filter switching.

### 8.3.6 (2025-12-22)
- Core: Refactored image caching: Standard download -> Steam API JSON Fallback -> Local Placeholder.
- Core: Improved game image download reliability with robust fallback for missing hashes.

### 8.3.5 (2025-12-21)
- Core: Implemented local caching for achievement images.
- Core: Organized cache directory structure (separate folders for games and achievements).

### 8.3.4 (2025-12-21)
- Core: Added support for Protected Achievements:
  - Shield icon indicator for protected achievements.
  - Modification tools (Save, Timer, Bulk Actions) are hidden when protection is active.
  - Checkboxes are hidden to prevent accidental unlocking.
  - Status bar warning for protected games.

### 8.3.3 (2025-12-21)
- UI: Polished Timer Mode: index of the achievement queue, ETA display based on current time, drag handle position, responsiveness.
- UI: Added comprehensive external links support (Steam Store, Guides, Stats, SteamDB, Completionist.me).

### 8.2.3 (2025-12-21)
- UI: Added "Details" button with external links support (Completionist.me).
- UI: Polish and layout adjustments of the game list.

### 8.2.2 (2025-12-20)
- Core: Implemented image caching and async loading for faster library loading.
- Core: Optimized shutdown process.

### 8.1.2 (2025-12-20)
- UX: Streamlined error messages to display only on the status bar.

### 8.1.1 (2025-12-20)
- UX: Added sorting and filtering functionality to the achievements list.

### 8.1.0 (2025-12-20)
- Core: Implemented timer functionality.

### 8.0.0 (2025-12-19)
- Core: Initial release of Super Lazy Achievement Manager - 2025