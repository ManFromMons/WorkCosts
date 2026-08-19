# Shell

`MainWindow`: title **Will I DIY?**, 48px-class title bar with back (when stack can go back), pane toggle, app icon, **Light/Dark switch** on the right.

Navigation:

- Home (work job cards) — tag `home`  
- Work — tag `work` (placeholder page today)  
- Stuff (group, not selectable): Products, Jobs, Categories  
- Settings (footer / dedicated item)

Selecting a leaf **clears the back stack** (section switch, not a deep link stack). Home after launch.

Behind the frame: `GarageBackground` image, `UniformToFill`, plus ~95% opaque page brush.

GNOME/iPad: same destinations. Compact iPad: **tab bar** with Home, Products, Jobs, Categories, Settings (Work can fold into Home until the Work page exists).
