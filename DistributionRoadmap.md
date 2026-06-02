# RagNext Release & Distribution Architecture Strategy

This document establishes the official cross-platform distribution, licensing, and payment model for RagNext. This architecture completely decouples core game-engine logic from web infrastructure, eliminating the need for a custom database or self-hosted web server.

## 🧭 1. CORE ARCHITECTURE PILLARS

| Component | Technology | Responsibility | Cost (Sandbox) | Cost (Production) |
| :--- | :--- | :--- | :--- | :--- |
| **Monetization & Taxes** | **Paddle** | Merchant of Record (MoR). Handles web checkouts, pop-up overlays, recurring subscription billing, and 100% of global/international sales tax filing compliance. | $0 / mo | 5% + $0.50 per transaction |
| **Licensing & DRM** | **Keygen.sh** | Cloud licensing brain. Handles secure user authentication, node-locking (machine fingerprints), standalone web login checks, and Steam validation. | $0 / mo (Up to 25 keys) | $19 / mo (1,000+ keys) |
| **Primary Platform** | **Steam** | Primary discovery engine and storefront launcher. Provides automated delta background updates and native desktop platform DRM checks. | $100 fee per app listing | 30% flat platform split |
| **Marketing Web Layer** | **GitHub Pages** | Serves static marketing storefront landing page (`ragnext.com`). Zero backend scripts required. | $0 / mo | $0 / mo |
| **Binary File Delivery** | **GitHub Releases** | Hosts compiled production installation zip files (`.exe` / `.app`) securely via global download links. | $0 / mo | $0 / mo |

---

## 🔒 2. SECURITY & REPOSITORY MANAGEMENT (THE TWO-REPO SPLIT)
To completely insulate the source code from public access while maintaining free web and file distribution channels, the GitHub configuration is split into two separate containment fields:

1. **`RagNext-Core` (Strictly Private):** Contains all C# source files, engine dependencies, `.sln` solution manifests, and Avalonia view components. Visible only to the core development team.
2. **`RagNext-Public` (100% Public):** Contains exclusively flat static HTML landing page files (for GitHub Pages deployment) and compiled, production-ready binary zip assets attached strictly via GitHub Release tags.

---

## 🔄 3. THE HYBRID VERIFICATION LOGIC (HOW THE APP RUNS)
When the Avalonia application initializes its launcher lifecycle on a user's desktop, it executes a single, unified verification loop pinging Keygen's secure API:

### Path A: The Steam Launch Environment
* The launcher detects it was booted directly from the Steam client.
* It grabs the user's cryptographically signed Steam App Ticket using the native Steamworks .NET SDK wrapper.
* It passes this token to Keygen. Keygen instantly verifies ownership with Steam's servers and grants access, bypassing any need for an external user account login screen.

### Path B: The Direct Web Purchase Environment
* The user buys a permanent Version 1 Major Release license or a monthly tier subscription via the Paddle overlay on `ragnext.com`.
* Paddle fires an automated webhook directly to Keygen, instantly activating a new profile under the customer's email.
* The user opens the standalone app, enters their credentials into the flat dark Avalonia login box, and Keygen verifies the license state, pairing it securely to that specific machine's unique hardware GUID.