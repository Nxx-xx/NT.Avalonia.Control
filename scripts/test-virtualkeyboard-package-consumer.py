#!/usr/bin/env python3
"""Pack and verify isolated VirtualKeyboard NuGet consumers; Python 3 + .NET SDK.

No PowerShell or publishing. Diagnostic artifacts remain in artifacts/consumer.
"""
import os
from pathlib import Path
import shutil
import subprocess
import uuid
import xml.etree.ElementTree as ET
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parent.parent
FIXTURES = ROOT / "scripts/Consumers/VirtualKeyboard"


def run(*args, cwd=ROOT, env=None):
    subprocess.run(args, cwd=cwd, env=env, check=True)


def main():
    version = ET.parse(ROOT / "Directory.Build.props").findtext(".//Version")
    if not version:
        raise RuntimeError("Product Version is missing")
    session = ROOT / "artifacts/consumer" / ("keyboard-" + uuid.uuid4().hex)
    feed = session / "feed"
    feed.mkdir(parents=True)
    for package in ("NT.AvaloniaControl.VirtualKeyboard", "NT.AvaloniaControl.VirtualKeyboard.Themes"):
        run("dotnet", "pack", str(ROOT / "src" / package), "-c", "Release", "-m:1", "-o", str(feed))
    for mode in ("FunctionOnly", "DefaultTheme"):
        consumer = session / mode
        consumer.mkdir()
        for name in ("Directory.Build.props", "Directory.Packages.props"):
            (consumer / name).write_text("<Project />", encoding="utf-8")
        package = "NT.AvaloniaControl.VirtualKeyboard" + (".Themes" if mode == "DefaultTheme" else "")
        project = (FIXTURES / "Consumer.csproj.template").read_text(encoding="utf-8")
        (consumer / "Consumer.csproj").write_text(project.replace("__PACKAGE_ID__", package).replace("__PRODUCT_VERSION__", version), encoding="utf-8")
        for name in ("Program.cs", "Window.axaml", "ScopeChecks.cs"):
            shutil.copyfile(FIXTURES / (name + ".template"), consumer / name)
        program = (consumer / "Program.cs").read_text(encoding="utf-8")
        if program.count('        window.Close();') != 1:
            raise RuntimeError("Consumer fixture changed: expected one window.Close insertion point")
        program = program.replace('        window.Close();', '        ScopeChecks.Run(window, editor, keyboard, args.Single() == "DefaultTheme");\n        window.Close();', 1)
        (consumer / "Program.cs").write_text(program, encoding="utf-8")
        styles = ((FIXTURES / "CustomStyles.axaml.template").read_text(encoding="utf-8") if mode == "FunctionOnly"
                  else '<StyleInclude Source="avares://NT.AvaloniaControl.VirtualKeyboard.Themes/Themes/Generic.axaml" />')
        app = (FIXTURES / "App.axaml.template").read_text(encoding="utf-8")
        (consumer / "App.axaml").write_text(app.replace("__KEYBOARD_STYLES__", styles), encoding="utf-8")
        config = consumer / "NuGet.Config"
        config.write_text('<configuration><packageSources><clear /><add key="local-preview" value="' + escape(str(feed), {'"': '&quot;'}) + '" /><add key="nuget.org" value="https://api.nuget.org/v3/index.json" /></packageSources></configuration>', encoding="utf-8")
        # Both caches are fresh: same-version preview packages cannot be reused.
        env = os.environ.copy()
        env["NUGET_PACKAGES"] = str(consumer / ".packages")
        env["NUGET_HTTP_CACHE_PATH"] = str(consumer / ".http-cache")
        run("dotnet", "restore", "Consumer.csproj", "--configfile", str(config), "--disable-parallel", cwd=consumer, env=env)
        run("dotnet", "build", "Consumer.csproj", "-c", "Release", "--no-restore", "-m:1", cwd=consumer, env=env)
        run("dotnet", "run", "--project", "Consumer.csproj", "-c", "Release", "--no-build", "--no-restore", "--", mode, cwd=consumer, env=env)
    print(f"PASS: both fresh package consumers; artifacts: {session}")


if __name__ == "__main__":
    main()
