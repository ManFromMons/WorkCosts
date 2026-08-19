# Windows (WinUI 3)

Reference implementation. Unpackaged for daily F5 (`WindowsPackageType=None`). Assembly name `WillIDIY`.

- SDK: .NET 9, Windows App SDK 2.4, WinUI 3.  
- Entry: `App` → `DatabaseService.InitializeAsync` → `MainWindow.NavigateToHome`.  
- Fetch: WebView2 (`ChromiumPageLoader`).  
- Theme: Auto / Light / Dark (`AppThemeService`) plus title-bar toggle.  
- Packaging: `WorkCosts.Package` MSIX / `Pack-Msix.ps1`.  
- Planned: paste HTML on Add Product; zip export/import; product photos as files not BLOBs.

Do not retarget this project for Linux or iOS.
