#!/usr/bin/env python3
"""Minimal client for the blender_mcp addon socket server (127.0.0.1:9876).

Usage:
  python bmcp.py '{"type":"ping"}'
  python bmcp.py --code 'import bpy; print(bpy.app.version_string)'
"""
import json
import socket
import sys

HOST, PORT = "127.0.0.1", 9876


def send(cmd: dict, timeout: float = 300.0) -> dict:
    with socket.create_connection((HOST, PORT), timeout=10) as s:
        s.settimeout(timeout)
        s.sendall(json.dumps(cmd).encode("utf-8"))
        buf = b""
        while True:
            chunk = s.recv(65536)
            if not chunk:
                break
            buf += chunk
            try:
                return json.loads(buf.decode("utf-8"))
            except json.JSONDecodeError:
                continue
    raise RuntimeError("connection closed before a complete JSON response")


def main() -> None:
    if len(sys.argv) >= 3 and sys.argv[1] == "--code":
        code = sys.argv[2]
        cmd = {"type": "execute_code", "params": {"code": code}}
    elif len(sys.argv) >= 3 and sys.argv[1] == "--file":
        with open(sys.argv[2], "r", encoding="utf-8") as f:
            code = f.read()
        cmd = {"type": "execute_code", "params": {"code": code}}
    else:
        cmd = json.loads(sys.argv[1])
    resp = send(cmd)
    print(json.dumps(resp, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
