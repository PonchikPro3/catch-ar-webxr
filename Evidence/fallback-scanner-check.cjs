const fs=require('fs'),vm=require('vm'),assert=require('assert');
(async()=>{
 const lib={},packets=[],requests=[];let tick,cancelled=0;
 const matrix=[1,0,0,0,0,1,0,0,0,0,1,0,.1,-.5,-2,1];
 const session={refSpace:{},requestReferenceSpace:async()=>({}),requestHitTestSource:async o=>{requests.push(o);assert.equal(o.entityTypes[0],'plane');return{cancel(){cancelled++;}};},requestAnimationFrame:cb=>{tick=cb;return 1;},cancelAnimationFrame(){},addEventListener(){},removeEventListener(){}};
 const Module={WebXR:{Settings:{AROptionalFeatures:['hit-test']},xrSession:session},SendMessage:(r,m,p)=>packets.push(JSON.parse(p))};
 vm.runInNewContext(fs.readFileSync('Assets/CatchAR/Plugins/RoomScanner.jslib','utf8'),{LibraryManager:{library:lib},mergeInto:Object.assign,Module,UTF8ToString:x=>x,XRRay:class{},Number,Math});
 lib.CatchARScanStart('Catch Game');for(let i=0;i<15;i++)await Promise.resolve();
 const frame={getViewerPose:()=>({views:[{}]}),getHitTestResults:()=>[{getPose:()=>({transform:{matrix}})}]};
 tick(100,frame);assert.equal(packets[0].backend,'hit-test');assert.equal(packets[0].hits.length,9);assert(packets[0].hits.every(h=>h.verified&&h.fullCell));
 matrix[5]=0;tick(200,frame);assert.equal(packets[1].hits.length,0);
 lib.CatchARScanStop();assert.equal(cancelled,9);
 console.log('PASS: plane sources used directly; no depth API required; nine usable fallback samples; walls rejected; sources cancelled. Mock XR frames, not a device test.');
})().catch(e=>{console.error(e);process.exitCode=1;});
