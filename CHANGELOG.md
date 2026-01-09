## Change Log

### 8.6.13 (2026-01-09)
- Added Warning Module ("SLAM Alert") to prevent accidental actions.
- Added warnings when attempting to open DLC content without the base game.
- Added warnings when attempting to unlock achievements for uninstalled games.
- Added warnings when attempting to instantly unlock multiple achievements (>2) to encourage safe usage.

### 8.6.12 (2026-01-07)
- Merged status icons (Installed, Favorite) into a unified indicator area.
- Added visual "Installed" indicator to game cards and the filter area.
- Added "Not Favorite" icon state for better discoverability.

### 8.6.11 (2026-01-07)
- Added "Smart Random" button for achievements randomization in timer mode.
  - Dynamically calculates jitter based on the input range.
  - Ensures values are naturally distributed across the achievement list (top -> min, bottom -> max).

### 8.6.10 (2026-01-05)
- Favorites System.
  - Added interactive Star icon on game cards (hover to see, click to toggle).
  - Added "Favorites Only" filter to quickly access pinned games.
  - Added "Add to Favorites" option in the game context menu.
  - `config.json` for storing application settings and favorites.

### 8.5.10 (2026-01-04)
- Core: Now parsing `appinfo.vdf` directly to detect installed games, allowing games without achievements to be listed.
- UI: Replaced dropdown filters with segmented controls for better accessibility.
- UI: Added exclusive filters for games with and without achievements.

### 8.4.10 (2025-12-31)
- Added background image on the achievement list.
- Improved image caching logic (thread-safe, DRY, UI-thread safe).

### 8.4.9 (2025-12-28)
- Feature: Added new sorting options to sort by Unlock Time (Newest/Oldest), prioritizing unlocked achievements while intelligently sorting remaining locked achievements by rarity.

### 8.4.8 (2025-12-28)
- System: Single Instance Enforcement: Launching the app while it's already running now brings the existing window to the foreground.
- Architecture: Consolidated API libraries and Steam utilities into a unified `SAM.API` for better maintainability (Internal).

### 8.4.7 (2025-12-23)
- Feature: Broken Achievement detection. Achievements with missing or zero-byte images now display a specific "Broken" icon with a tooltip explaining potential issues.
- Fix: Resolved application crash when switching between games, specifically when transitioning from games with no hidden achievements.
- Fix: Corrected "Reveal Hidden" button visibility logic and spacing uniformity.
- System: Implemented robust crash logging. Critical and unhandled errors are now saved to `crash.log` for troubleshooting.

### 8.4.6 (2025-12-22)
- Hidden achievement handling:
  - Hidden locked achievements now display as "Hidden Achievement" with a placeholder description and a hidden icon until unlocked or revealed.
  - Reveal Locked Hidden Achievements. Added a "Reveal Hidden" toggle button to reveal details of hidden achievements.
- Filter by Locked/Unlocked/All. Replaced dropdown with segmented control buttons using new icons.
- Optimization: Enhanced image caching for hidden icons and faster filter switching.

### 8.3.6 (2025-12-22)
- Refactored image caching: Standard download -> Steam API JSON Fallback -> Local Placeholder.
- Improved game image download reliability with robust fallback for missing hashes.

### 8.3.5 (2025-12-21)
- Implemented local caching for achievement images.
- Organized cache directory structure (separate folders for games and achievements).

### 8.3.4 (2025-12-21)
- Added support for Protected Achievements:
  - Shield icon indicator for protected achievements.
  - Modification tools (Save, Timer, Bulk Actions) are hidden when protection is active.
  - Checkboxes are hidden to prevent accidental unlocking.
  - Status bar warning for protected games.

### 8.3.3 (2025-12-21)
- Polished Timer Mode UI: index of the achievement queue, ETA display based on current time, drag handle position, responsiveness.
- Added comprehensive external links support (Steam Store, Guides, Stats, SteamDB, Completionist.me).

### 8.2.3 (2025-12-21)
- Added "Details" button with external links support (Completionist.me).
- UI polish and layout adjustments.

### 8.2.2 (2025-12-20)
- Implemented image caching and async loading for faster library loading.
- Optimized shutdown process.

### 8.1.2 (2025-12-20)
- Streamlined error messages to display only on the status bar.

### 8.1.1 (2025-12-20)
- Added sorting and filtering functionality to the achievements list.

### 8.1.0 (2025-12-20)
- Implemented timer functionality.

### 8.0.0 (2025-12-19)
- Initial release of Super Lazy Achievement Manager - 2025