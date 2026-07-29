#!/usr/bin/env python3
"""On-demand Volleyball match-server spawner.

Runs on the game box behind the existing web server. POST /spawn launches one
dedicated match server (the Linux build with -vbhost), waits for it to create
its Relay session, and returns the join code — the requester shares that code
and everyone joins through the normal in-game Online flow. Servers exit on
their own when idle (built into the game), so this service only ever starts
processes and reaps the finished ones.

Endpoints:
    POST /spawn   -> {"code": "ABC123"} | {"error": "..."}
    GET  /status  -> {"active": 2, "max": 4}

Suggested nginx location block (TLS already handled by the site):

    location /volleyball/ {
        proxy_pass http://127.0.0.1:8765/;
        proxy_read_timeout 60s;
    }

Run under systemd via volleyball-spawn.service (same directory).
Stdlib only — no pip installs needed.
"""

import json
import os
import re
import subprocess
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

BIND = ("127.0.0.1", 8765)          # keep loopback-only; nginx is the front door
GAME_DIR = os.path.expanduser("~/volleyball/server")
BINARY = os.path.join(GAME_DIR, "Volleyball.x86_64")
LOG_DIR = os.path.expanduser("~/volleyball/logs")
MAX_SERVERS = 4                      # concurrent matches allowed on the box
CODE_TIMEOUT_S = 45                  # UGS session creation is a few seconds; allow slack

CODE_RE = re.compile(r"JOIN CODE:\s+(\w+)")

_procs = []  # list of (Popen, logpath)


def _reap():
    global _procs
    _procs = [(p, lg) for (p, lg) in _procs if p.poll() is None]


def _spawn():
    _reap()
    if len(_procs) >= MAX_SERVERS:
        return None, f"server limit reached ({MAX_SERVERS} matches running)"
    if not os.path.isfile(BINARY):
        return None, f"game binary not found at {BINARY}"

    os.makedirs(LOG_DIR, exist_ok=True)
    logpath = os.path.join(LOG_DIR, f"match-{int(time.time())}-{len(_procs)}.log")
    proc = subprocess.Popen(
        [BINARY, "-vbhost", "-batchmode", "-nographics", "-logFile", logpath],
        cwd=GAME_DIR,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
    )
    _procs.append((proc, logpath))

    deadline = time.time() + CODE_TIMEOUT_S
    while time.time() < deadline:
        if proc.poll() is not None:
            return None, "server exited before creating a session (see its log)"
        try:
            with open(logpath, "r", errors="replace") as f:
                m = CODE_RE.search(f.read())
                if m:
                    return m.group(1), None
        except FileNotFoundError:
            pass
        time.sleep(0.5)

    proc.terminate()
    return None, "timed out waiting for a join code"


class Handler(BaseHTTPRequestHandler):
    def _send(self, status, payload):
        body = json.dumps(payload).encode()
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def do_POST(self):
        if self.path.rstrip("/") in ("", "/spawn"):
            code, err = _spawn()
            if code:
                self._send(200, {"code": code})
            else:
                self._send(503, {"error": err})
        else:
            self._send(404, {"error": "unknown endpoint"})

    def do_GET(self):
        if self.path.rstrip("/") in ("", "/status"):
            _reap()
            self._send(200, {"active": len(_procs), "max": MAX_SERVERS})
        else:
            self._send(404, {"error": "unknown endpoint"})

    def log_message(self, fmt, *args):  # quiet: systemd journal gets enough
        pass


if __name__ == "__main__":
    print(f"vb-spawn listening on {BIND[0]}:{BIND[1]}, binary: {BINARY}")
    ThreadingHTTPServer(BIND, Handler).serve_forever()
