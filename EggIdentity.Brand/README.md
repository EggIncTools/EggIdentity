# EggIdentity.Brand

Canonical brand marks for the EggIncTools suite, shipped as Razor class library static web assets.

Consumers take a `PackageReference` and resolve bytes from their own origin:

```
_content/EggIdentity.Brand/brand/{slug}/icon-{size}.png
_content/EggIdentity.Brand/brand/{slug}/lockup.png
_content/EggIdentity.Brand/brand/{slug}/wordmark.png
_content/EggIdentity.Brand/brand/tools/tools.ico
```

Metadata (slug, display name, accent, families, trim inset) lives in `EggIdentity.Contract`, class `Brands`. Path helpers are there too; do not hand-build these strings.

## Assets are committed, not generated

Nothing resamples at build or runtime. The downscales below were produced once and committed so the bytes are reviewable and the package is deterministic. Regenerate only with the same settings.

| Slug | Master | Resampler |
|---|---|---|
| ledger | `EggLedger.Web/wwwroot/images/icon-512.png` | Lanczos |
| incognito | `EggIncognito/wwwroot/brand/icon.png` | Nearest |
| abacus | `eggabacus_pix_w_eggs.png` | Lanczos |
| identity | `eggidentity_pix_lock.png` | Lanczos |
| tools | `toolbox-egg-wrench-512.png` | Lanczos |

Incognito's icon earns nearest-neighbour on colour evidence, not grid alignment: 10 unique colours and 2 alpha levels, so an integer nearest downscale cannot introduce a colour that was not authored. The other four carry sub-grid detail (soft shading, antialiased outlines) and nearest drops whole pixel rows on them, which looks worse than Lanczos softening.

An earlier claim that these masters sit on a native 4px or 8px pixel grid was retracted. Round-trip tests fail for all five at every size. Run-length analysis reports a grid where there is none, because large flat regions produce long runs regardless of authoring. Do not re-derive it that way.

512 divides exactly by 256, 128, 64, 32 and 16. 180 and 192 do not divide it, so they are the 128 downscale centred on a transparent canvas rather than a resample.

## Trim inset

`BrandInfo.UniformTrimInset` is a uniform inset from every edge, **not** the alpha bounding box. The two differ and the bounding box is the wrong one.

For incognito the alpha bbox is `(112, 40, 400, 472)`, giving a non-square 288x432. The uniform 40px inset gives a square 432x432 that is byte-identical to the `icon-trimmed.png` that app ships today. Consumers wanting the trimmed form crop the untrimmed asset; cropped padding is not recoverable, so only the untrimmed bytes are stored.

## Pixelation

`image-rendering: pixelated` applies to the icon family only. Lockups and wordmarks are authored antialiased (2026 and 32 unique colours, 99 and 16 alpha levels), and pixelating them on scale makes them worse. Use `brand-icon` and `brand-art` from `EggIdentity.Styles`.

## Licensing

Assets are all rights reserved, not MIT. See `LICENSE-BRAND.txt`. Packages that depend on this one do not relicense the art.
