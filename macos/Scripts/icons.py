from PIL import Image
from pathlib import Path
root=Path(__file__).resolve().parent.parent
image=Image.open(root/'Assets/Brand.png').convert('RGBA')
folder=root/'Assets/AppIcon.iconset'; folder.mkdir(exist_ok=True)
for size in [16,32,128,256,512]:
    for scale in [1,2]:
        n=size*scale
        canvas=Image.new('RGBA',(n,n))
        copy=image.copy(); copy.thumbnail((round(n*.88),round(n*.88)),Image.Resampling.LANCZOS)
        canvas.alpha_composite(copy,((n-copy.width)//2,(n-copy.height)//2))
        canvas.save(folder/f'icon_{size}x{size}{"@2x" if scale==2 else ""}.png')
canvas=Image.open(folder/'icon_256x256.png')
canvas.save(root/'Assets/favicon.ico',sizes=[(16,16),(32,32),(48,48),(64,64),(128,128),(256,256)])
canvas.save(root/'Assets/favicon.png')
