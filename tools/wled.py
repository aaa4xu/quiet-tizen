#!/usr/bin/env python3
"""Control a WLED device over its HTTP JSON API and realtime UDP protocols.

Deliberately small and dependency-free: it is the reference implementation for
the ambilight sender, so every packet it builds is inspectable.

Realtime transports:
  ddp   Distributed Display Protocol v1 on UDP 4048 (sequence + push flag)
  drgb  WLED's classic "DRGB" realtime packet on the configured UDP port

Examples:
  wled.py --host 10.0.0.5 info
  wled.py --host 10.0.0.5 on --bri 128
  wled.py --host 10.0.0.5 fill 255 0 0 --seconds 5
  wled.py --host 10.0.0.5 corners
  wled.py --host 10.0.0.5 segments
  wled.py --host 10.0.0.5 chase --seconds 10
"""
from __future__ import annotations

import argparse
import base64
import json
import os
import socket
import struct
import sys
import time
import urllib.error
import urllib.request

DDP_PORT = 4048


def _request(host: str, path: str, payload: dict | None = None, timeout: float = 8.0):
    url = f"http://{host}{path}"
    data = None
    headers = {}
    method = "GET"
    if payload is not None:
        data = json.dumps(payload).encode()
        headers["Content-Type"] = "application/json"
        method = "POST"
    req = urllib.request.Request(url, data=data, headers=headers, method=method)
    with urllib.request.urlopen(req, timeout=timeout) as resp:
        body = resp.read().decode()
    return json.loads(body) if body.strip() else {}


def info(host: str) -> dict:
    return _request(host, "/json/info")


def state(host: str) -> dict:
    return _request(host, "/json/state")


def ddp_packet(seq: int, pixels: bytes, offset: int = 0, push: bool = True) -> bytes:
    """Build a DDP v1 write packet.

    Header layout from the DDP specification: flags, sequence, data type,
    destination ID, a 32-bit data offset (bytes 4-7) and a 16-bit data length
    (bytes 8-9). Version 1 is 0x40 and the push flag is 0x01.
    """
    length = len(pixels)
    if length % 3:
        raise ValueError("DDP RGB payload must be a multiple of 3 bytes")
    flags = 0x40 | (0x01 if push else 0x00)
    header = bytes([
        flags,
        seq & 0x0F,
        0x01,  # data type: RGB
        0x00,  # destination id
        (offset >> 24) & 0xFF, (offset >> 16) & 0xFF, (offset >> 8) & 0xFF, offset & 0xFF,
        (length >> 8) & 0xFF, length & 0xFF,
    ])
    return header + pixels


def drgb_packet(pixels: bytes, timeout_s: int = 5) -> bytes:
    return bytes([2, timeout_s & 0xFF]) + pixels


class WebSocket:
    """Minimal RFC 6455 client: just enough for WLED's /ws live view."""

    def __init__(self, host: str, port: int = 80, timeout: float = 8.0):
        self.sock = socket.create_connection((host, port), timeout=timeout)
        self.buf = b""
        key = base64.b64encode(os.urandom(16)).decode()
        request = (
            f"GET /ws HTTP/1.1\r\n"
            f"Host: {host}\r\n"
            f"Upgrade: websocket\r\n"
            f"Connection: Upgrade\r\n"
            f"Sec-WebSocket-Key: {key}\r\n"
            f"Sec-WebSocket-Version: 13\r\n"
            f"\r\n"
        )
        self.sock.sendall(request.encode())
        head = self._read_until(b"\r\n\r\n")
        status = head.split(b"\r\n", 1)[0]
        if b"101" not in status:
            raise RuntimeError(f"WebSocket upgrade failed: {status.decode(errors='replace')}")

    def _read_until(self, marker: bytes) -> bytes:
        while marker not in self.buf:
            chunk = self.sock.recv(4096)
            if not chunk:
                raise RuntimeError("WebSocket closed during handshake")
            self.buf += chunk
        head, _, self.buf = self.buf.partition(marker)
        return head

    def _read_exact(self, n: int) -> bytes:
        while len(self.buf) < n:
            chunk = self.sock.recv(65536)
            if not chunk:
                raise RuntimeError("WebSocket closed")
            self.buf += chunk
        out, self.buf = self.buf[:n], self.buf[n:]
        return out

    def send_text(self, text: str) -> None:
        payload = text.encode()
        mask = os.urandom(4)
        header = bytearray([0x81])
        if len(payload) < 126:
            header.append(0x80 | len(payload))
        elif len(payload) < 65536:
            header.append(0x80 | 126)
            header += struct.pack(">H", len(payload))
        else:
            header.append(0x80 | 127)
            header += struct.pack(">Q", len(payload))
        masked = bytes(b ^ mask[i % 4] for i, b in enumerate(payload))
        self.sock.sendall(bytes(header) + mask + masked)

    def read_frame(self) -> tuple[int, bytes]:
        b1, b2 = self._read_exact(2)
        opcode = b1 & 0x0F
        masked = bool(b2 & 0x80)
        length = b2 & 0x7F
        if length == 126:
            length = struct.unpack(">H", self._read_exact(2))[0]
        elif length == 127:
            length = struct.unpack(">Q", self._read_exact(8))[0]
        mask = self._read_exact(4) if masked else None
        payload = self._read_exact(length)
        if mask:
            payload = bytes(b ^ mask[i % 4] for i, b in enumerate(payload))
        return opcode, payload

    def close(self) -> None:
        try:
            self.sock.close()
        except OSError:
            pass


def peek(host: str, seconds: float = 3.0) -> tuple[int, bytes]:
    """Enable WLED's live view and return the newest LED frame it broadcasts.

    WLED's web UI "Peek" button does the same thing: it asks the WebSocket for
    live LED data. The returned payload is the raw strip buffer, so its channel
    order follows the configured LED type, not necessarily RGB.
    """
    ws = WebSocket(host)
    try:
        ws.send_text('{"lv":true}')
        deadline = time.monotonic() + seconds
        ws.sock.settimeout(1.0)
        last: tuple[int, bytes] = (0, b"")
        while time.monotonic() < deadline:
            try:
                opcode, payload = ws.read_frame()
            except (socket.timeout, TimeoutError):
                continue
            if opcode == 2 and payload:
                last = (opcode, payload)
            elif opcode == 8:
                break
        return last
    finally:
        ws.close()


def decode_led_frame(payload: bytes, count: int) -> bytes | None:
    """Strip WLED's live-view header bytes, if present.

    WLED 16 sends two bytes before the raw strip buffer (observed 0x4C 0x01).
    """
    for prefix in (2, 1, 0):
        if len(payload) == count * 3 + prefix:
            return payload[prefix:]
    return None


def send_pixels(host: str, pixels: bytes, transport: str = "ddp",
                udp_port: int = DDP_PORT, seconds: float = 2.0, rate_hz: float = 20.0,
                offset: int = 0) -> int:
    """Stream one frame repeatedly; WLED displays a realtime frame on receipt."""
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    sock.setsockopt(socket.SOL_SOCKET, socket.SO_SNDBUF, 1 << 16)
    period = 1.0 / rate_hz if rate_hz > 0 else 0.05
    deadline = time.monotonic() + seconds
    seq = 0
    sent = 0
    while time.monotonic() < deadline:
        if transport == "ddp":
            pkt = ddp_packet(seq, pixels, offset=offset)
            seq = (seq + 1) & 0x0F
        elif transport == "drgb":
            pkt = drgb_packet(pixels)
        else:
            raise ValueError(f"unknown transport {transport!r}")
        sock.sendto(pkt, (host, udp_port))
        sent += 1
        time.sleep(period)
    return sent


def led_count(host: str) -> int:
    data = info(host)
    count = int(data.get("leds", {}).get("count", 0))
    if count <= 0:
        raise RuntimeError("WLED did not report an LED count")
    return count


def segments(host: str) -> list[dict]:
    data = state(host)
    return data.get("seg", [])


def solid(count: int, r: int, g: int, b: int) -> bytes:
    return bytes((r, g, b)) * count


def corners_payload(count: int, segs: list[dict]) -> tuple[bytes, list[tuple[int, tuple[int, int, int]]]]:
    """Light the first LED of every segment so the physical layout can be read."""
    palette = [
        (255, 0, 0), (0, 255, 0), (0, 0, 255), (255, 255, 255),
        (255, 255, 0), (0, 255, 255), (255, 0, 255), (128, 128, 0),
    ]
    frame = bytearray(count * 3)
    marks = []
    idx = 0
    seen = set()
    for seg in segs:
        start = int(seg.get("start", 0))
        if start in seen or start >= count:
            continue
        seen.add(start)
        color = palette[idx % len(palette)]
        frame[start * 3:start * 3 + 3] = bytes(color)
        marks.append((start, color))
        idx += 1
    return bytes(frame), marks


def segments_payload(count: int, segs: list[dict]) -> tuple[bytes, list[tuple[str, int, int, tuple[int, int, int]]]]:
    """Paint every segment in its own colour.

    Catch-all segments spanning the whole strip are skipped when real segments
    exist, otherwise they would paint over the per-segment map.
    """
    palette = [
        (255, 0, 0), (0, 255, 0), (0, 0, 255), (255, 255, 255),
        (255, 255, 0), (0, 255, 255), (255, 0, 255), (128, 128, 0),
    ]
    frame = bytearray(count * 3)
    marks = []
    idx = 0
    specific = [s for s in segs if not (int(s.get("start", 0)) == 0 and int(s.get("stop", 0)) >= count)]
    chosen = specific if specific else segs
    for seg in chosen:
        name = seg.get("n", "")
        start = int(seg.get("start", 0))
        stop = int(seg.get("stop", 0))
        if stop <= start or start >= count:
            continue
        color = palette[idx % len(palette)]
        for i in range(start, min(stop, count)):
            frame[i * 3:i * 3 + 3] = bytes(color)
        marks.append((name, start, stop, color))
        idx += 1
    return bytes(frame), marks


def chase_payload(count: int, head: int, width: int = 3) -> bytes:
    frame = bytearray(count * 3)
    for k in range(width):
        i = (head + k) % count
        level = 255 if k == 0 else max(0, 200 - 80 * k)
        frame[i * 3:i * 3 + 3] = bytes((level, level, level))
    return bytes(frame)


def rainbow_payload(count: int) -> bytes:
    frame = bytearray(count * 3)
    for i in range(count):
        hue = i / max(1, count)
        r, g, b = _hsv_to_rgb(hue, 1.0, 1.0)
        frame[i * 3:i * 3 + 3] = bytes((r, g, b))
    return bytes(frame)


def _hsv_to_rgb(h: float, s: float, v: float) -> tuple[int, int, int]:
    i = int(h * 6) % 6
    f = h * 6 - int(h * 6)
    p = int(255 * v * (1 - s))
    q = int(255 * v * (1 - f * s))
    t = int(255 * v * (1 - (1 - f) * s))
    vv = int(255 * v)
    return [(vv, t, p), (q, vv, p), (p, vv, t), (p, q, vv), (t, p, vv), (vv, p, q)][i]


def print_info(data: dict) -> None:
    leds = data.get("leds", {})
    print(f"version: {data.get('ver')}  name: {data.get('name')}  ip: {data.get('ip')}")
    print(f"leds: {leds.get('count')}  rgbw: {leds.get('rgbw')}  maxpwr: {leds.get('maxpwr')}  maxseg: {leds.get('maxseg')}")
    print(f"udpport: {data.get('udpport')}  live: {data.get('live')}  mode: {data.get('lm')!r}  from: {data.get('lip')!r}")


def print_state(data: dict) -> None:
    print(f"on: {data.get('on')}  bri: {data.get('bri')}  mainseg: {data.get('mainseg')}")
    for seg in data.get("seg", []):
        print(f"  seg {seg.get('id')} {seg.get('n')!r}: {seg.get('start')}..{seg.get('stop')} on={seg.get('on')} col={seg.get('col')}")


def main(argv: list[str] | None = None) -> int:
    # The same options are accepted before and after the subcommand. Subparser
    # copies use SUPPRESS so an omitted flag never clobbers the earlier value.
    shared = argparse.ArgumentParser(add_help=False)
    shared.add_argument("--host", default=argparse.SUPPRESS, help="WLED address, preferably a literal IP")
    shared.add_argument("--transport", choices=("ddp", "drgb"), default=argparse.SUPPRESS)
    shared.add_argument("--udp-port", type=int, default=argparse.SUPPRESS,
                        help=f"UDP port (default {DDP_PORT} for ddp, device udpport for drgb)")
    shared.add_argument("--seconds", type=float, default=argparse.SUPPRESS,
                        help="how long to stream a pattern")
    shared.add_argument("--rate", type=float, default=argparse.SUPPRESS,
                        help="packets per second")

    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--host", default=None, help="WLED address, preferably a literal IP")
    parser.add_argument("--transport", choices=("ddp", "drgb"), default="ddp")
    parser.add_argument("--udp-port", type=int, default=None,
                        help=f"UDP port (default {DDP_PORT} for ddp, device udpport for drgb)")
    parser.add_argument("--seconds", type=float, default=2.0, help="how long to stream a pattern")
    parser.add_argument("--rate", type=float, default=20.0, help="packets per second")
    sub = parser.add_subparsers(dest="action", required=True)

    sub.add_parser("info", parents=[shared])
    sub.add_parser("state", parents=[shared])
    p_peek = sub.add_parser("peek", parents=[shared])
    p_peek.add_argument("--peek-seconds", type=float, default=3.0,
                        help="how long to listen for live LED frames")
    p_on = sub.add_parser("on", parents=[shared])
    p_on.add_argument("--bri", type=int, default=None)
    sub.add_parser("off", parents=[shared])
    p_fill = sub.add_parser("fill", parents=[shared])
    p_fill.add_argument("r", type=int)
    p_fill.add_argument("g", type=int)
    p_fill.add_argument("b", type=int)
    sub.add_parser("corners", parents=[shared])
    sub.add_parser("segments", parents=[shared])
    sub.add_parser("rainbow", parents=[shared])
    p_chase = sub.add_parser("chase", parents=[shared])
    p_chase.add_argument("--width", type=int, default=3)

    args = parser.parse_args(argv)
    if not args.host:
        parser.error("--host is required")

    if args.action == "info":
        print_info(info(args.host))
        return 0
    if args.action == "state":
        print_state(state(args.host))
        return 0
    if args.action == "peek":
        count = led_count(args.host)
        _, payload = peek(args.host, args.peek_seconds)
        if not payload:
            print("no live LED frame received (is the strip idle?)")
            return 1
        data = decode_led_frame(payload, count)
        if data is None:
            print(f"unexpected live frame: {len(payload)} bytes for {count} LEDs; head={payload[:16].hex()}")
            return 1
        triples = [tuple(data[i * 3:i * 3 + 3]) for i in range(count)]
        print(f"frame: {len(payload)} bytes for {count} LEDs")
        print("first:", " ".join(f"{i}:{triples[i]}" for i in range(min(8, count))))
        print("last:", " ".join(f"{i}:{triples[i]}" for i in range(max(0, count - 4), count)))
        mean = tuple(round(sum(t[k] for t in triples) / count, 1) for k in range(3))
        print("mean:", mean, " distinct:", len(set(triples)))
        chunks = 16
        step = count // chunks
        print("map:", " ".join(
            str(tuple(round(sum(triples[i * step + j][k] for j in range(step)) / step) for k in range(3)))
            for i in range(chunks)))
        return 0
    if args.action == "on":
        payload = {"on": True}
        if args.bri is not None:
            payload["bri"] = args.bri
        print(_request(args.host, "/json/state", payload))
        return 0
    if args.action == "off":
        print(_request(args.host, "/json/state", {"on": False}))
        return 0

    count = led_count(args.host)
    segs = segments(args.host)
    port = args.udp_port
    if port is None:
        port = DDP_PORT if args.transport == "ddp" else int(info(args.host).get("udpport", 21324))

    if args.action == "fill":
        pixels = solid(count, args.r, args.g, args.b)
        sent = send_pixels(args.host, pixels, args.transport, port, args.seconds, args.rate)
        print(f"sent {sent} {args.transport} packets ({count} LEDs) to {args.host}:{port}")
        return 0
    if args.action == "corners":
        pixels, marks = corners_payload(count, segs)
        sent = send_pixels(args.host, pixels, args.transport, port, args.seconds, args.rate)
        print(f"sent {sent} packets; segment starts:")
        for start, color in marks:
            print(f"  LED {start}: rgb{color}")
        return 0
    if args.action == "segments":
        pixels, marks = segments_payload(count, segs)
        sent = send_pixels(args.host, pixels, args.transport, port, args.seconds, args.rate)
        print(f"sent {sent} packets; segments:")
        for name, start, stop, color in marks:
            print(f"  {name!r} {start}..{stop}: rgb{color}")
        return 0
    if args.action == "rainbow":
        pixels = rainbow_payload(count)
        sent = send_pixels(args.host, pixels, args.transport, port, args.seconds, args.rate)
        print(f"sent {sent} packets (rainbow, {count} LEDs)")
        return 0
    if args.action == "chase":
        sock_deadline = time.monotonic() + args.seconds
        period = 1.0 / args.rate
        head = 0
        seq = 0
        sent = 0
        sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        while time.monotonic() < sock_deadline:
            pixels = chase_payload(count, head, args.width)
            if args.transport == "ddp":
                pkt = ddp_packet(seq, pixels)
                seq = (seq + 1) & 0x0F
            else:
                pkt = drgb_packet(pixels)
            sock.sendto(pkt, (args.host, port))
            sent += 1
            head = (head + 1) % count
            time.sleep(period)
        print(f"sent {sent} packets (chase, {count} LEDs)")
        return 0
    return 1


if __name__ == "__main__":
    sys.exit(main())
