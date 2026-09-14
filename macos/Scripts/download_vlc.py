#!/usr/bin/env python3
import concurrent.futures, urllib.request, pathlib, time, hashlib
root=pathlib.Path(__file__).resolve().parent.parent
url='https://download.videolan.org/cocoapods/prod/VLCKit-3.7.3-319ed2c0-79128878.tar.xz'
size=88186488
parts=root/'Vendor'/'parts'; parts.mkdir(exist_ok=True)
chunk=1024*1024
def get(i):
    start=i*chunk; end=min(size,start+chunk)-1; path=parts/str(i)
    if path.exists() and path.stat().st_size==end-start+1:return
    for attempt in range(5):
        try:
            req=urllib.request.Request(url,headers={'Range':f'bytes={start}-{end}'})
            with urllib.request.urlopen(req,timeout=180) as response:
                assert response.status==206
                data=response.read()
            assert len(data)==end-start+1
            path.write_bytes(data)
            print(f'Part {i+1}/{(size+chunk-1)//chunk}',flush=True);return
        except Exception:
            if attempt==4:raise
            time.sleep(2)
with concurrent.futures.ThreadPoolExecutor(max_workers=32) as pool:list(pool.map(get,range((size+chunk-1)//chunk)))
out=root/'Vendor'/'VLCKit-3.7.3-complete.tar.xz'
with out.open('wb') as f:
    for i in range((size+chunk-1)//chunk):f.write((parts/str(i)).read_bytes())
digest=hashlib.sha256(out.read_bytes()).hexdigest()
assert digest=='019afdae4e2e2d0f3ac325fac8f7ba0af25dca70b9d157df7d60db88e0be8e5d', 'Archive checksum mismatch'
print(digest,flush=True)
