import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
const builtRoot=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../Builds/WebGL');
const root=fs.existsSync(path.join(builtRoot,'index.html'))?builtRoot:path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../docs');
const port=Number(process.env.PORT||8080);
const types={'.html':'text/html; charset=utf-8','.js':'application/javascript','.wasm':'application/wasm','.json':'application/json','.data':'application/octet-stream','.css':'text/css','.png':'image/png'};
const server=http.createServer((req,res)=>{
  let name;
  try{name=decodeURIComponent(new URL(req.url,'http://localhost').pathname)}catch{res.writeHead(400);res.end();return}
  const target=path.resolve(root,'.'+(name==='/'?'/index.html':name));
  if(target!==root&&!target.startsWith(root+path.sep)){res.writeHead(403);res.end();return}
  fs.stat(target,(error,stat)=>{
    if(error||!stat.isFile()){res.writeHead(404);res.end('Not found');return}
    res.writeHead(200,{'Content-Type':types[path.extname(target)]||'application/octet-stream','Content-Length':stat.size,'Cache-Control':'no-cache'});
    fs.createReadStream(target).pipe(res);
  });
});
server.listen(port,'127.0.0.1',()=>console.log(`Catch AR: http://localhost:${port}\nCtrl+C stops the server.`));
server.on('error',error=>{console.error(error.message);process.exitCode=1});
