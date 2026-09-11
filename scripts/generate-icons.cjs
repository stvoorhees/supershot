// npm install --no-save sharp; node scripts/generate-icons.cjs
// ICO embeds separately rasterized PNGs so each Windows scale has a crisp source.
const fs = require('node:fs');
const path = require('node:path');
const sharp = require('sharp');
const root = path.join(__dirname, '..');
async function ico(source, sizes, destination) {
  const images = await Promise.all(sizes.map(n => sharp(source).resize(n,n).png().toBuffer()));
  const head = Buffer.alloc(6 + sizes.length * 16); head.writeUInt16LE(1,2); head.writeUInt16LE(sizes.length,4);
  let offset = head.length;
  sizes.forEach((n,i) => { const at = 6+i*16; head[at]=head[at+1]=n===256?0:n;head.writeUInt16LE(1,at+4);head.writeUInt16LE(32,at+6);head.writeUInt32LE(images[i].length,at+8);head.writeUInt32LE(offset,at+12);offset+=images[i].length; });
  fs.writeFileSync(destination,Buffer.concat([head,...images]));
}
(async()=>{
  const app = path.join(root,'assets/icon.svg'), tray = path.join(root,'assets/tray.svg');
  for(const n of [16,32,48,64,128,256,512,1024])await sharp(app).resize(n,n).png().toFile(path.join(root,`assets/icon-${n}.png`));
  await ico(app,[16,20,24,32,40,48,64,128,256],path.join(root,'assets/Supershot.ico'));
  await ico(tray,[16,20,24,32,40,48,64],path.join(root,'assets/Tray.ico'));
  await ico(path.join(root,'assets/tray-dark.svg'),[16,20,24,32,40,48,64],path.join(root,'assets/TrayDark.ico'));
  await sharp(app).resize(256,256).png().toFile(path.join(root,'editor/logo.png'));
})();
