# Confirm sample pages (planning)

Use this whenever the user starts a **new supplier host** with a URL (or without a complete story). The **user’s confirmed values** are the contract. Your scrape is only a proposal.

Stay on **`Planning`**. Do not create `feature/source-…`. Do not implement parsers, fixtures, or tests in this pass.

## Bar

**Status** `ready-for-agent` only when the story lists **at least three** product pages, each with:

- Canonical **product URL** (same host family)
- **Expected Name** (as the user sees it on the page)
- **Expected UnitPrice** (GBP, as the user sees it)

Until then **Status** is `draft`. Do not fill those fields from unconfirmed extraction.

## Interaction (one page at a time)

1. Take the first URL. Open or fetch the product page so you can **propose** Name, UnitPrice, and any optional fields that are obvious (manufacturer, seller, EAN). Prefer a real browser view when HttpClient is a challenge wall. If you cannot see the page, say so and ask the user what **they** see.
2. Ask them to **confirm or correct**. Phrase it so they can answer from the page in front of them, for example:
   - Proposed Name: …
   - Proposed unit price: £…
   - Optional (only if visible): …
   - “Is that what you see? If not, paste the name and GBP price from the page.”
3. Wait. Do not write Expected Name / UnitPrice into the story until they confirm or supply replacements. If they correct a value, **their** text is what goes in the file.
4. Ask for **more product URLs on the same host** until you have **three confirmed pages**. Prefer variety when they can (another category, a sale price, a variant) — do not invent URLs.
5. Repeat steps 1–3 for each new URL. Do not batch-guess three pages and ask once.
6. Login, CAPTCHA they cannot pass, or a non-product URL: skip that URL, ask for a replacement. Do not scrape behind a password. Do not commit cookies.

Optional fields: ask only when they are clearly on the page. Name and GBP price are required for every sample. Null is fine for the rest.

## After three confirmed pages

Write or rewrite `docs/features/source-<host>.md` from [template.md](template.md). Put every sample under **Parameters**. Assign **Seq**. Set **Status** `ready-for-agent`.

Then **stop**. Tell them the story id. Ask whether to land Planning (`merge-planning`) and whether to invoke `/start-add-source source-<host>` to **implement**. Do not start coding unless they explicitly say to implement now.

## CLI

This protocol needs a conversation. Do not run it as a single `agent -p` shot that invents Name and price. Interactive `agent` or editor chat only. Headless `/start-add-source source-<host>` is for a story that is already `ready-for-agent`.
