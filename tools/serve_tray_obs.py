"""Serve an owned tray-release OBS example; never changes OBS or live integrations.

Start an explicitly selected packaged host or pinned SDK/assembly. The printed
URL is for a separate owned OBS Browser Source. Commands exercise authenticated
restart/restore while OBS remains open. A tone plays only on an explicit command.
Process checks are preparation evidence, never an OBS visual/audio pass.
"""

import argparse
import concurrent.futures
import contextlib
import io
import json
import math
import shutil
import sqlite3
import struct
import subprocess
import tempfile
import time
import urllib.request
import uuid
import wave
from pathlib import Path

from qualify_desktop_control import BrowserClient, OwnedWine, checked_command, free_port, request, start_host


OVERLAY_ID = "owned-tray-recovery"


def tone_bytes():
    output = io.BytesIO()
    with wave.open(output, "wb") as audio:
        audio.setnchannels(1)
        audio.setsampwidth(2)
        audio.setframerate(48000)
        # One second at low amplitude, with fades to avoid abrupt clicks.
        samples = []
        for index in range(48000):
            fade = min(1, index / 480, (47999 - index) / 480)
            samples.append(struct.pack("<h", int(32767 * .08 * fade * math.sin(2 * math.pi * 660 * index / 48000))))
        audio.writeframes(b"".join(samples))
    return output.getvalue()


def upload(client, content, mime, filename):
    message = urllib.request.Request(client.origin + "/api/assets", data=content, method="POST",
        headers={"Origin": client.origin, "X-TDSBLive-CSRF": client.csrf,
                 "Content-Type": mime, "X-Asset-Filename": filename})
    with client.opener.open(message, timeout=30) as response:
        return json.load(response)["id"]


class OwnedExample:
    def __init__(self, command, profile, port, wine=None):
        self.command = command
        self.profile = profile
        self.port = port
        self.wine = wine
        self.process = None
        self.bootstrap = None
        self.client = None
        self.backup = None
        self.state_widget = str(uuid.uuid4())
        self.progress_widget = str(uuid.uuid4())
        self.tone_widget = str(uuid.uuid4())
        self.tone_asset = None

    def start(self):
        self.process, self.bootstrap, _ = start_host(self.command, self.profile, self.wine)
        self.client = BrowserClient(self.port)
        configuration = self.client.send("/api/configuration")
        for integration in ("streamerBot", "speakerBot", "rumble"):
            if configuration[integration]["enabled"]:
                raise RuntimeError("An integration was enabled in the owned example")
        if configuration["server"]["enableLan"]:
            raise RuntimeError("The owned example unexpectedly enabled LAN access")

    def seed(self):
        image = upload(self.client, bytes.fromhex("47494638396101000100800000ff00000000ff2c00000000010001000002024401003b"),
                       "image/gif", "owned-tray-image.gif")
        self.tone_asset = upload(self.client, tone_bytes(), "audio/wav", "owned-tray-tone.wav")
        document = {"id": OVERLAY_ID, "name": "Owned tray and OBS recovery example", "width": 1000,
            "height": 600, "canvasEnabled": True, "widgets": [
                {"id": str(uuid.uuid4()), "name": "Owned title", "kind": "text", "x": 24, "y": 24,
                 "width": 950, "height": 80, "fontSize": 36, "text": "TDSBLive tray recovery test"},
                {"id": self.progress_widget, "name": "Owned progress", "kind": "progress-bar", "x": 24, "y": 130,
                 "width": 950, "height": 100, "progress": {"value": 25, "target": 100, "label": "Owned progress"}},
                {"id": str(uuid.uuid4()), "name": "Owned image", "kind": "image", "x": 24, "y": 270,
                 "width": 160, "height": 120, "assetId": image},
                {"id": self.state_widget, "name": "Owned persistent state", "kind": "custom", "x": 230, "y": 270,
                 "width": 720, "height": 120, "custom": {"permissions": ["storage"], "subscriptions": [],
                     "html": '<p id="state">Loading owned state</p>',
                     "css": "body { color: white; background: transparent; font: 36px sans-serif; }",
                     "javaScript": 'function refresh() { SBX.store.get().then(s => document.getElementById("state").textContent = "Owned state "+s.count); } refresh(); SBX.on("sbx:session", s => { if (s.connected) refresh(); });'}}
            ]}
        self.client.send("/api/overlays", document, "POST")
        self.client.send(self.store_route, {"count": 1}, "PUT")
        self.backup = self.client.send("/api/recovery/backup", method="POST")
        self.assert_state(1, 25)

    @property
    def store_route(self):
        return f"/api/overlays/{OVERLAY_ID}/widgets/{self.state_widget}/store"

    def assert_state(self, count, progress):
        current = self.client.send(f"/api/overlays/{OVERLAY_ID}")
        widget = next(item for item in current["widgets"] if item["id"] == self.progress_widget)
        if self.client.send(self.store_route)["count"] != count or widget["progress"]["value"] != progress:
            raise RuntimeError("The owned overlay or persistent state did not survive recovery")

    def mutate(self):
        current = self.client.send(f"/api/overlays/{OVERLAY_ID}")
        for widget in current["widgets"]:
            if widget["id"] == self.progress_widget:
                widget["progress"]["value"] = 75
        self.client.send(self.store_route, {"count": 2}, "PUT")
        self.client.send(f"/api/overlays/{OVERLAY_ID}", current, "PUT")
        self.assert_state(2, 75)

    def lifecycle(self, operation):
        previous = self.client.send("/api/application/status")["generation"]
        if operation == "restore":
            checked = self.client.send("/api/recovery/validate", self.backup, "POST", raw=True)
        with concurrent.futures.ThreadPoolExecutor(max_workers=1) as executor:
            outcome = executor.submit(request, self.bootstrap, "wait", 30)
            if operation == "restore":
                self.client.send("/api/recovery/restore", {"id": checked["id"], "confirm": True}, "POST")
            else:
                self.client.send("/api/application/restart", method="POST")
            if outcome.result(timeout=30)["State"] != "restart-ready":
                raise RuntimeError("The owned host did not explicitly authorize successful relaunch")
        if self.process.wait(timeout=15) != 0:
            raise RuntimeError("The owned host did not shut down cleanly")
        self.start()
        if self.client.send("/api/application/status")["generation"] == previous:
            raise RuntimeError("The owned host did not start a new generation")

    def tone(self):
        current = self.client.send(f"/api/overlays/{OVERLAY_ID}")
        current["widgets"].append({"id": self.tone_widget, "name": "Explicit owned tone", "kind": "audio",
                                   "assetId": self.tone_asset, "volume": 1, "loop": False, "muted": False})
        try:
            self.client.send(f"/api/overlays/{OVERLAY_ID}", current, "PUT")
            time.sleep(2)
        finally:
            # Remove it before any restart/restore. Ordinary fixture startup is silent.
            current = self.client.send(f"/api/overlays/{OVERLAY_ID}")
            current["widgets"] = [widget for widget in current["widgets"] if widget["id"] != self.tone_widget]
            self.client.send(f"/api/overlays/{OVERLAY_ID}", current, "PUT")

    def close(self):
        if self.process is not None and self.process.poll() is None:
            try:
                request(self.bootstrap, "quit")
                self.process.wait(timeout=15)
            finally:
                if self.process.poll() is None:
                    self.process.terminate()
                    self.process.wait(timeout=15)
        database = self.profile / "tdsblive.db"
        if not database.is_file(): return
        subprocess.run(["sonar", "analyze", "secrets", str(database)], check=True, stdout=subprocess.DEVNULL)
        with contextlib.closing(sqlite3.connect(database)) as connection:
            if connection.execute("PRAGMA integrity_check").fetchone()[0] != "ok":
                raise RuntimeError("The owned database did not pass integrity checking")


def run(command, self_check, wine=None):
    command = checked_command(command)
    if wine is not None and len(command) != 1:
        raise ValueError("Wine requires the packaged Windows EXE")
    subprocess.run(["sonar", "analyze", "secrets", str(Path(command[-1]).parent)], check=True)
    directory = Path(tempfile.mkdtemp(prefix="tdsblive-tray-obs-"))
    wine_session = None
    try:
        if wine is not None:
            wine_session = OwnedWine(wine, directory)
        profile = directory / "owned profile with spaces"
        profile.mkdir()
        port = free_port()
        (profile / "configuration.json").write_text(json.dumps({"displayName": "Owned tray OBS qualification",
            "server": {"host": "127.0.0.1", "port": port}}), encoding="utf-8")
        example = OwnedExample(command, profile, port, wine_session)
        try:
            example.start()
            example.seed()
            print(f"Owned OBS URL: http://127.0.0.1:{port}/overlay/{OVERLAY_ID} (1000 x 600)", flush=True)
            print("Integrations/LAN disabled; audio silent until the explicit tone command. No OBS settings are changed.", flush=True)
            if self_check:
                example.mutate()
                example.lifecycle("restart")
                example.assert_state(2, 75)
                example.lifecycle("restore")
                example.assert_state(1, 25)
                print("Owned fixture preparation passed: overlay/assets, persistent state, authenticated restart/restore. This is not OBS evidence.", flush=True)
            else:
                print("Commands: mutate (progress 75/state 2), restart, restore (progress 25/state 1), tone, status, quit.", flush=True)
                while True:
                    action = input("Owned example> ").strip().lower()
                    if action == "quit": break
                    if action == "mutate": example.mutate()
                    elif action in ("restart", "restore"): example.lifecycle(action)
                    elif action == "tone": example.tone()
                    elif action == "status":
                        print("Owned state:", example.client.send(example.store_route)["count"], flush=True)
                    else: print("Choose one of the listed commands.", flush=True)
        finally:
            example.close()
    finally:
        if wine_session is not None:
            wine_session.wait()
        shutil.rmtree(directory)
    print("Owned host stopped; temporary profile removed.", flush=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--executable", help="Explicit packaged TDSBLive.exe")
    mode.add_argument("--dotnet", help="Explicit pinned dotnet executable")
    parser.add_argument("--assembly", help="ExtensionSuite.Host.dll, required with --dotnet")
    parser.add_argument("--wine", help="Linux only: installed wine/wine64; uses a fresh isolated test prefix")
    parser.add_argument("--self-check", action="store_true", help="Check fixture lifecycle without touching OBS or playing audio")
    arguments = parser.parse_args()
    if arguments.dotnet and not arguments.assembly or arguments.executable and arguments.assembly:
        parser.error("Choose packaged EXE or pinned SDK plus host assembly")
    if arguments.wine and not arguments.executable:
        parser.error("--wine requires --executable; existing prefixes are never used")
    run([arguments.executable] if arguments.executable else [arguments.dotnet, arguments.assembly],
        arguments.self_check, arguments.wine)
