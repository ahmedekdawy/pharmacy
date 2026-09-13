#!/usr/bin/env python3
import json
import os
from pathlib import Path

path = Path("deploy/appsettings.Production.json")
data = json.loads(path.read_text(encoding="utf-8")) if path.exists() else {}

db = os.environ.get("DB", "").strip()
if not db:
    print("DATABASE_CONNECTION_STRING not set; skipping overlay.")
    raise SystemExit(0)

data.setdefault("ConnectionStrings", {})["DefaultConnection"] = db
data["PathBase"] = "/api"

jwt = os.environ.get("JWT", "").strip()
if jwt:
    data.setdefault("Jwt", {})["Key"] = jwt

path.write_text(json.dumps(data, indent=2), encoding="utf-8")
print(f"Wrote {path}")
