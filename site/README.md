# Key Assign landing page

Static site for <https://keyassign.ilecto.app>, served by Cloudflare Workers static assets.

- `public/index.html`: Spanish (default)
- `public/en/index.html`: English
- `public/styles.css`, `public/favicon.svg`, `public/404.html`
- `public/favicon.svg` is the app icon: four keys in two rows, the second row shifted a quarter key to the right. `public/logo.svg` (hero, Spanish tagline) and `public/logo-en.svg` (English tagline) nest the same keys. The header's inline mark is a simplified copy without letters.

There is no build step: edit the files in `public/` directly. Pages use system fonts and load nothing from other sites, in keeping with the app's no-telemetry rule (R7.2). Keep both languages in step, and keep the progress section in sync with `docs/ROADMAP.md` when a milestone moves.

## Preview and deploy

```sh
cd site
npx wrangler dev       # local preview on http://localhost:8787
npx wrangler deploy    # publish to keyassign.ilecto.app
```

The custom domain is declared in `wrangler.jsonc`; it requires the `ilecto.app` zone in the same Cloudflare account.
