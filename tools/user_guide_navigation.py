"""Reading order shared by the public wiki and packaged offline guide."""

GROUPS = (
    ("Start here", (("Home", "Welcome and reading order"), ("Before-You-Begin", "Before you begin"))),
    ("Install or update", (("Install-and-Update", "Choose, update or remove"), ("Install-on-Windows", "Windows installation"), ("Install-on-Linux-Wine", "Linux with Wine / Bottles"), ("Install-on-Linux-Proton", "Linux with Proton / UMU"))),
    ("Your first working setup", (("First-Setup", "Connect your services"), ("Chat-and-OBS", "See chat in OBS"), ("Overlays-and-Alerts", "Create overlays and alerts"), ("Adaptive-Editor-and-Guided-Alerts", "Adaptive editor and guided alerts"))),
    ("Everyday use", (("Everyday-Use", "Stream checklist"), ("Tray-and-Desktop-Controls", "Find the app and quit"), ("Supporter-Totals", "Understand supporter totals"), ("Automation", "Speech and sound rules"))),
    ("Protect your setup and get help", (("Backup-and-Recovery", "Back up and recover"), ("LAN-Access", "Optional network access"), ("Troubleshooting", "Troubleshooting"), ("Glossary", "Plain-language glossary"))),
    ("Optional advanced features", (("Advanced-Editor-and-Widgets", "Advanced layout and widgets"), ("Custom-Widgets-and-Portable-Packages", "Custom widgets and sharing"), ("Local-StreamElements-Compatibility", "Local compatibility"))),
)


def ordered_pages(guide):
    names = [name for _, entries in GROUPS for name, _ in entries]
    actual = {page.stem for page in guide.glob("*.md")}
    if len(names) != len(set(names)) or set(names) != actual:
        raise ValueError("Every guide chapter must appear exactly once in navigation")
    return [guide / (name + ".md") for name in names]
