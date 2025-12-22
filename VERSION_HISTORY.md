## Version History

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
- Initial release of SAM Reborn - 2026