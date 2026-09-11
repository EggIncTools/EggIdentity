# EggIdentity.Brand

Canonical brand marks for the EggIncTools suite, shipped as Razor class library static web assets. Consumers take a `PackageReference` and serve the bytes from their own origin under `_content/EggIdentity.Brand/brand/`.

Slug, display name, accent, families, trim inset and the path helpers live in `EggIdentity.Contract`, class `Brands`. That class is the only supported way to build an asset path.

Assets are committed, not generated. Nothing resamples at build or runtime, so the bytes are reviewable and the package is deterministic. Downscales were produced once with Lanczos, except incognito, which uses nearest-neighbour: its master has 10 unique colours and 2 alpha levels, so an integer nearest downscale cannot introduce an unauthored colour. The other masters carry sub-grid detail that nearest destroys.

The masters sit on no native pixel grid. Round-trip tests fail for all five at every size, and run-length analysis reports a grid that is an artifact of large flat regions.

512 divides exactly by 256, 128, 64, 32 and 16. The 180 and 192 sizes are the 128 downscale centred on a transparent canvas, not resamples.

`BrandInfo.UniformTrimInset` is a uniform inset from every edge, not the alpha bounding box; for incognito the two differ and the bounding box is non-square. Only untrimmed bytes are stored, because cropped padding is not recoverable.

`image-rendering: pixelated` suits the icon family only. Lockups and wordmarks are authored antialiased. `EggIdentity.Styles` exposes `brand-icon` and `brand-art` for the two cases.

Assets are all rights reserved, not MIT; see `LICENSE-BRAND.txt`. Packages depending on this one do not relicense the art.
