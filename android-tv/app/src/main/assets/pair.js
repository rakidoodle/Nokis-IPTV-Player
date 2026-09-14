'use strict';
const fragment=new URLSearchParams(location.hash.slice(1));
const token=fragment.get('t'), publicKey=fragment.get('k');
history.replaceState(null,'',location.pathname);
const form=document.getElementById('form'), status=document.getElementById('status'), submit=document.getElementById('submit');
const type=document.getElementById('type');
type.addEventListener('change',()=>{ document.getElementById('login').hidden=type.value==='M3U';document.getElementById('seriesOption').hidden=type.value==='M3U';document.getElementById('stalker').hidden=type.value!=='STALKER'; });
if(!token||!publicKey){status.textContent='Scan the QR code shown on your TV to start pairing.';submit.disabled=true;}
form.addEventListener('submit',async event=>{
 event.preventDefault();submit.disabled=true;status.textContent='Sending securely…';
 try {
  const source=Object.fromEntries(new FormData(form));source.includeVod=source.includeVod==='true';source.includeSeries=source.includeSeries==='true';source.endpoint=source.endpoint.trim();source.epg=source.epg.trim();
  if(!/^https?:\/\//i.test(source.endpoint))throw new Error('Enter an HTTP or HTTPS URL.');
  if(source.type==='XTREAM'&&(!source.username||!source.password))throw new Error('Enter your provider username and password.');
  if(source.type==='STALKER'&&!/^[0-9a-f]{2}(:[0-9a-f]{2}){5}$/i.test(source.mac))throw new Error('Enter the registered MAC address.');
  const key=forge.random.getBytesSync(32),iv=forge.random.getBytesSync(12);
  const cipher=forge.cipher.createCipher('AES-GCM',key);cipher.start({iv:iv,tagLength:128});cipher.update(forge.util.createBuffer(forge.util.encodeUtf8(JSON.stringify(source))));if(!cipher.finish())throw new Error('Encryption failed.');
  const der=forge.util.decode64(publicKey.replace(/-/g,'+').replace(/_/g,'/'));
  const rsa=forge.pki.publicKeyFromAsn1(forge.asn1.fromDer(der));
  const envelope={key:forge.util.encode64(rsa.encrypt(key,'RSA-OAEP',{md:forge.md.sha256.create(),mgf1:{md:forge.md.sha256.create()}})),iv:forge.util.encode64(iv),data:forge.util.encode64(cipher.output.getBytes()+cipher.mode.tag.getBytes())};
  const response=await fetch('/source',{method:'POST',headers:{'Content-Type':'application/json','Authorization':'Bearer '+token},body:JSON.stringify(envelope)});
  const text=await response.text();if(!response.ok)throw new Error(text);status.textContent=text;form.reset();form.hidden=true;
 }catch(error){status.textContent=error.message||'Could not reach the TV. Scan a new QR code and try again.';submit.disabled=false;}
});
