# Browser session

`IBrowserPageSession` abstracts “load this URL in a real engine, return HTML + images”.

Windows: `ChromiumPageLoader` — WebView2 in an off-screen Popup (not inside ContentDialog). Captures image bytes from the network (Autodoc blocks HttpClient image downloads too).

GNOME: WebKitGTK (or webkit2gtk) in a hidden/offscreen web view.  
iPad: WKWebView; allow network; do not skip live scrape.

Rules:

- Create the view on the UI thread; wait for load with a timeout.  
- Prefer cache (`WebCacheStore.CanServeFromCacheAsync`) before spinning a browser.  
- After success, write HTML + images to the cache and index rows.  
- Surface status text in the add-product sheet (“Opening Autodoc in Chromium…”).  
- Cloudflare/CAPTCHA: fail clearly and offer **paste HTML**.
