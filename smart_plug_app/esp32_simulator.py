#!/usr/bin/env python3
"""
ESP32 + PZEM-004T simulator — lets you test the whole app WITHOUT hardware.

It behaves exactly like the real firmware:
  * logs in as your user to discover your devices
  * reads each device's DeviceKey from the API's SQLite file (the key is not exposed by the API)
  * every few seconds POSTs a realistic reading to  /api/devices/{id}/readings  (X-Device-Key)
  * polls  /api/devices/{id}/command  and applies relay commands (the app's power button)
  * when a device is OFF it reports ~0 W, when ON it reports its typical load + noise

Python 3.8+, standard library only.

Usage:
  python esp32_simulator.py --email you@mail.com --password 123456 \
      --db "C:/path/to/SmartEnergyApi/smart_energy.db"

Options:
  --api URL        default http://localhost:4000/api
  --interval SEC   default 5  (same as the firmware)
  --only 1,2       simulate only these device ids
  --spike          every ~60 s push one very high reading (triggers a threshold alert in the app)
"""
import argparse, json, math, random, sqlite3, sys, time, urllib.request, urllib.error

# typical load per device name, watts (fallback = 250 W)
TYPICAL = {"Smart Plug 01": 374.0, "Smart Plug 02": 198.0, "Smart Plug 03": 1200.0}


def call(method, url, body=None, headers=None):
    data = json.dumps(body).encode() if body is not None else None
    h = {"Content-Type": "application/json"}
    h.update(headers or {})
    req = urllib.request.Request(url, data=data, headers=h, method=method)
    try:
        with urllib.request.urlopen(req, timeout=8) as r:
            txt = r.read().decode()
            return r.status, (json.loads(txt) if txt else None)
    except urllib.error.HTTPError as e:
        txt = e.read().decode()
        try:
            return e.code, json.loads(txt)
        except Exception:
            return e.code, txt
    except Exception as e:
        return -1, str(e)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--api", default="http://localhost:4000/api")
    ap.add_argument("--email", required=True)
    ap.add_argument("--password", required=True)
    ap.add_argument("--db", required=True, help="path to smart_energy.db (to read DeviceKey)")
    ap.add_argument("--interval", type=float, default=5)
    ap.add_argument("--only", default="")
    ap.add_argument("--spike", action="store_true")
    a = ap.parse_args()
    api = a.api.rstrip("/")

    code, d = call("POST", f"{api}/auth/login", {"email": a.email, "password": a.password})
    if code != 200:
        sys.exit(f"Login failed ({code}): {d}\nCreate the account in the app first, then run again.")
    tok = {"Authorization": "Bearer " + d["token"]}

    code, d = call("GET", f"{api}/devices", headers=tok)
    if code != 200:
        sys.exit(f"Could not list devices ({code}): {d}")
    devices = d["devices"]
    only = {int(x) for x in a.only.split(",") if x.strip()}
    if only:
        devices = [x for x in devices if x["id"] in only]
    if not devices:
        sys.exit("No devices to simulate. Add a device in the app first.")

    try:
        con = sqlite3.connect(a.db)
        sim = {}
        for dev in devices:
            row = con.execute('SELECT "DeviceKey","CumulativeEnergyKwh" FROM "Devices" WHERE "Id"=?', (dev["id"],)).fetchone()
            if row is None:
                print(f"! device {dev['id']} not found in DB, skipping")
                continue
            try:
                rated = con.execute('SELECT "RatedWatts" FROM "Devices" WHERE "Id"=?', (dev["id"],)).fetchone()[0]
            except sqlite3.Error:
                rated = None  # column not created yet (start the API once first)
            base = rated * 0.8 if rated else TYPICAL.get(dev["name"], 250.0)
            sim[dev["id"]] = {"name": dev["name"], "key": row[0], "energy": float(row[1] or 0.0),
                              "base": base, "phase": random.random() * 6.28}
        con.close()
    except sqlite3.Error as e:
        sys.exit(f"Cannot read the database file: {e}\nCheck --db points to the API's smart_energy.db")

    print(f"Simulating {len(sim)} device(s): " + ", ".join(f"#{i} {s['name']}" for i, s in sim.items()))
    print("Press Ctrl+C to stop.\n")
    last_spike = time.time()
    is_on = {x["id"]: x["isOn"] for x in devices}

    try:
        while True:
            t0 = time.time()
            # keep app-side ON/OFF state in sync (the toggle updates it immediately in the API)
            c, dl = call("GET", f"{api}/devices", headers=tok)
            if c == 200:
                is_on = {x["id"]: x["isOn"] for x in dl["devices"]}
            for did, s in sim.items():
                hk = {"X-Device-Key": s["key"]}
                # 1) poll relay command (one-shot)
                c, r = call("GET", f"{api}/devices/{did}/command", headers=hk)
                if c == 200 and r and r.get("command") is not None:
                    is_on[did] = bool(r["command"])
                    print(f"[#{did}] relay command received -> {'ON' if is_on[did] else 'OFF'}")
                # 2) build a PZEM-like measurement
                on = is_on.get(did, False)
                volt = 220 + 4 * math.sin(t0 / 40 + s["phase"]) + random.uniform(-0.8, 0.8)
                if on:
                    drift = 1 + 0.12 * math.sin(t0 / 25 + s["phase"])
                    power = max(5.0, s["base"] * drift + random.uniform(-6, 6))
                    pf = min(0.99, max(0.35, 0.62 + 0.1 * math.sin(t0 / 55 + s["phase"])))
                else:
                    power, pf = 0.0, 0.0
                if a.spike and on and time.time() - last_spike > 60:
                    power = 2300.0 + random.uniform(0, 80)
                    pf = 0.95
                    print(f"[#{did}] SPIKE {power:.0f} W (should raise an alert if a threshold is set)")
                    last_spike = time.time()
                current = power / (volt * pf) if on and pf else 0.0
                s["energy"] += power / 1000.0 * (a.interval / 3600.0)
                body = {"voltage": round(volt, 1), "current": round(current, 3), "power": round(power, 1),
                        "energy": round(s["energy"], 5), "frequency": round(50 + random.uniform(-0.05, 0.05), 2),
                        "powerFactor": round(pf, 2)}
                c, r = call("POST", f"{api}/devices/{did}/readings", body, hk)
                st = "OK " if c in (200, 201) else f"ERR {c}"
                print(f"[#{did} {s['name']}] {st} {'ON ' if on else 'OFF'} {body['voltage']}V {body['current']}A {body['power']}W {body['energy']}kWh"
                      + ("" if c in (200, 201) else f"  {r}"))
            time.sleep(max(0.2, a.interval - (time.time() - t0)))
    except KeyboardInterrupt:
        print("\nStopped.")


if __name__ == "__main__":
    main()
