## Version History

### 8.4.9 (2026-12-28)
- Feature: Added new sorting options to sort by Unlock Time (Newest/Oldest), prioritizing unlocked achievements while intelligently sorting remaining locked achievements by rarity.

### 8.4.8 (2026-12-28)
- System: Single Instance Enforcement: Launching the app while it's already running now brings the existing window to the foreground.
- Architecture: Consolidated API libraries and Steam utilities into a unified `SAM.API` for better maintainability (Internal).

### 8.4.7 (2026-12-23)
- Feature: Broken Achievement detection. Achievements with missing or zero-byte images now display a specific "Broken" icon with a tooltip explaining potential issues.
- Fix: Resolved application crash when switching between games, specifically when transitioning from games with no hidden achievements.
- Fix: Corrected "Reveal Hidden" button visibility logic and spacing uniformity.
- System: Implemented robust crash logging. Critical and unhandled errors are now saved to `crash.log` for troubleshooting.

### 8.4.6 (2026-12-22)
- Hidden achievement handling:
  - Hidden locked achievements now display as "Hidden Achievement" with a placeholder description and a hidden icon until unlocked or revealed.
  - Reveal Locked Hidden Achievements. Added a "Reveal Hidden" toggle button to reveal details of hidden achievements.
- Filter by Locked/Unlocked/All. Replaced dropdown with segmented control buttons using new icons.
- Optimization: Enhanced image caching for hidden icons and faster filter switching.

### 8.3.6 (2026-12-22)
- Refactored image caching: Standard download -> Steam API JSON Fallback -> Local Placeholder.
- Improved game image download reliability with robust fallback for missing hashes.

### 8.3.5 (2026-12-21)
- Implemented local caching for achievement images.
- Organized cache directory structure (separate folders for games and achievements).

### 8.3.4 (2026-12-21)
- Added support for Protected Achievements:
  - Shield icon indicator for protected achievements.
  - Modification tools (Save, Timer, Bulk Actions) are hidden when protection is active.
  - Checkboxes are hidden to prevent accidental unlocking.
  - Status bar warning for protected games.

### 8.3.3 (2026-12-21)
- Polished Timer Mode UI: index of the achievement queue, ETA display based on current time, drag handle position, responsiveness.
- Added comprehensive external links support (Steam Store, Guides, Stats, SteamDB, Completionist.me).

### 8.2.3 (2026-12-21)
- Added "Details" button with external links support (Completionist.me).
- UI polish and layout adjustments.

### 8.2.2 (2026-12-20)
- Implemented image caching and async loading for faster library loading.
- Optimized shutdown process.

### 8.1.2 (2026-12-20)
- Streamlined error messages to display only on the status bar.

### 8.1.1 (2026-12-20)
- Added sorting and filtering functionality to the achievements list.

### 8.1.0 (2026-12-20)
- Implemented timer functionality.

### 8.0.0 (2026-12-19)
- Initial release of Super Lazy Achievement Manager - 2026