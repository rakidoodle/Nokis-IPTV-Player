#!/bin/sh
set -eu
cd "$(dirname "$0")/.."
./scripts/build.sh :app:assembleDebug
python3 - <<'PY'
from pathlib import Path
import hashlib, shutil
path=str(Path.cwd())
h=0
for c in path: h=(31*h+ord(c)) & 0xffffffff
root=Path.home()/'.cache/noki-iptv-build'/str(h)/'app'
source=root/'outputs/apk/debug/app-debug.apk'
dest=Path('dist/Noki-IPTV-TV-0.1.3.apk')
dest.parent.mkdir(exist_ok=True)
shutil.copy2(source,dest)
Path(str(dest)+'.sha256').write_text(hashlib.sha256(dest.read_bytes()).hexdigest()+'  '+dest.name+'\n')
print(dest.resolve())
PY
