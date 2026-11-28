mergeInto(LibraryManager.library, {
 CatchARScanConfigure:function(enabled){
  if(!Module.WebXR||!Module.WebXR.Settings)return;
  var s=Module.WebXR.Settings;
  s.AROptionalFeatures=(s.AROptionalFeatures||[]).filter(function(f){return f!=='plane-detection'&&f!=='depth-sensing';});
  s.CatchARDepthSensing=!!enabled;if(enabled)s.AROptionalFeatures.push('depth-sensing');
 },
 CatchARScanStart:function(receiverPtr){
  if(Module.catchARScanner)Module.catchARScanner.stop();
  var receiver=UTF8ToString(receiverPtr),session=Module.WebXR&&Module.WebXR.xrSession;if(!session)return;
  var state={active:true,sources:[],raf:null,last:-Infinity,kind:'pending',error:false};Module.catchARScanner=state;
  state.stop=function(){state.active=false;state.sources.forEach(function(s){try{s.source.cancel();}catch(e){}});state.sources=[];try{session.cancelAnimationFrame(state.raf);}catch(e){}session.removeEventListener('end',state.stop);};
  session.addEventListener('end',state.stop);
  session.requestReferenceSpace('viewer').then(async function(viewer){
   if(!state.active)return;if(!session.requestHitTestSource){state.error=true;return;}
   var first;
   first=await session.requestHitTestSource({space:viewer,entityTypes:['plane']});state.kind='plane';
   if(!state.active){first.cancel();return;}state.sources.push({source:first,ray:0});
   if(typeof XRRay==='undefined')return;
   var i=1;
   for(var y=-1;y<=1;y++)for(var x=-1;x<=1;x++){
    if(x===0&&y===0)continue;
    (function(ray,dx,dy){session.requestHitTestSource({space:viewer,entityTypes:[state.kind],offsetRay:new XRRay({x:0,y:0,z:0,w:1},{x:dx,y:dy,z:-1,w:0})}).then(function(source){if(state.active)state.sources.push({source:source,ray:ray});else{try{source.cancel();}catch(e){}}}).catch(function(){});})(i++,x*.0125,y*.0125);
   }
  }).catch(function(){state.error=true;});
  function transform(m,p){return{x:m[0]*p.x+m[4]*p.y+m[8]*p.z+m[12],y:m[1]*p.x+m[5]*p.y+m[9]*p.z+m[13],z:m[2]*p.x+m[6]*p.y+m[10]*p.z+m[14],w:m[3]*p.x+m[7]*p.y+m[11]*p.z+m[15]};}
  function depthMatch(point,depth,view){
   var geometry=depth.transform||view.transform,projection=depth.projectionMatrix||view.projectionMatrix;
   var camera=transform(geometry.inverse.matrix,point),clip=transform(projection,camera);
   if(clip.w<=0||camera.z>=0)return 0;
   var u=(clip.x/clip.w+1)*.5,v=(1-clip.y/clip.w)*.5;
   if(u<0||u>1||v<0||v>1)return 0;
   var measured=depth.getDepthInMeters(u,v),expected=-camera.z;
   if(!Number.isFinite(measured)||measured<=0)return 0;
   return Math.abs(measured-expected)<=Math.max(.02,expected*.015)?1:-1;
  }
  function cellDepth(m,depth,view){
   var p={x:m[12],y:m[13],z:m[14]},result=depthMatch(p,depth,view);if(result!==1)return result;
   var minX=Math.floor(p.x/.05)*.05,minZ=Math.floor(-p.z/.05)*.05;
   var plane=m[4]*p.x+m[5]*p.y+m[6]*p.z;
   for(var i=0;i<4;i++){
    var x=minX+(i===1||i===2?.05:0),z=-(minZ+(i>=2?.05:0));
    var y=(plane-m[4]*x-m[6]*z)/m[5];
    var hit=depthMatch({x:x,y:y,z:z},depth,view);if(hit!==1)return hit;
   }return 1;
  }
  function tick(time,frame){
   if(!state.active)return;state.raf=session.requestAnimationFrame(tick);if(time-state.last<100||!session.refSpace)return;state.last=time;
   var packet={backend:'coarse',tracking:false,unavailable:state.error,hits:[]};
   try{
    var pose=frame.getViewerPose(session.refSpace);packet.tracking=!!pose;
    var view=pose&&pose.views&&pose.views[0],depth=null;
    try{if(view&&frame.getDepthInformation)depth=frame.getDepthInformation(view);}catch(e){}
    if(depth&&typeof depth.getDepthInMeters!=='function')depth=null;
    packet.backend=depth?'depth':state.kind==='pending'?'waiting':'hit-test';
    if(pose)state.sources.forEach(function(s){
     var results=frame.getHitTestResults(s.source);if(!results.length)return;
     var hit=results[0].getPose(session.refSpace);if(!hit)return;var m=hit.transform.matrix;if(Math.abs(m[5])<.98480775)return;
     var proof=depth?cellDepth(m,depth,view):1;
     packet.hits.push({ray:s.ray,position:{x:m[12],y:m[13],z:-m[14]},normal:{x:m[4],y:m[5],z:-m[6]},verified:proof===1,fullCell:proof===1,rejected:proof===-1});
    });
   }catch(e){packet.tracking=false;packet.hits=[];}
   if(state.active)Module.SendMessage(receiver,'ReceiveScanFrame',JSON.stringify(packet));
  }state.raf=session.requestAnimationFrame(tick);
 },
 CatchARScanStop:function(){if(Module.catchARScanner){Module.catchARScanner.stop();Module.catchARScanner=null;}}
});

