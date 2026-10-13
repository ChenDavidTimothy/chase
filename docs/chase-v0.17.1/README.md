# CHASE v0.17.1: Clean arena floor

Removed 22 nonphysical `Floor marker X/Z` guide lines from the **public Arena 01** scene. The real `Ground 12x12` collider, existing modular obstacles, role-based starting plates, camera, movement, Practice and technical Gym are unchanged. Added a permanent release contract that rejects reintroduced production floor-grid markers.

## Verification

- 33/33 EditMode tests passed.
- 103/103 PlayMode tests passed.
- Cloudflare-compatible production WebGL build succeeded with **zero errors**, using Unity's `.unityweb` browser decompression fallback (no `_headers` required).
- Physical Chrome browser navigation reached Arena 01 preview and launched a new chase, with zero browser-console errors or JS exceptions.
- All 17 WebGL files verified in `Builds/ChaseWebGL` (largest compressed asset less than 25 MiB).
- The single authoritative `Chibi/Chibi_Rigged.blend` checksum remains unchanged.

[Actual Arena 01 preview](evidence/webgl/arena-preview-no-grid.png) · [Match countdown](evidence/webgl/match-countdown-no-grid.png)

## To deploy

In existing Cloudflare Pages project `chase`, create a new **Production** deployment and upload the **contents of `Builds/ChaseWebGL`** (the folder Cloudflare groups as `ChaseWebGL`) with 17 files. Do not upload `Assets/`, `Library/`, or source project folders. The existing `https://chase-3bl.pages.dev/` will continue to show the previous release until this upload completes. Hard-refresh after deployment.
