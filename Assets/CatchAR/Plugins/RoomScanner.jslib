mergeInto(LibraryManager.library, {
  CatchARScanConfigure: function (enabled) {
    if (!Module.WebXR || !Module.WebXR.Settings) return;
    var settings = Module.WebXR.Settings;
    var features = (settings.AROptionalFeatures || []).filter(function (f) { return f !== 'plane-detection'; });
    if (enabled) features.push('plane-detection');
    settings.AROptionalFeatures = features;
  },
  CatchARScanStart: function (receiverPtr) {
    if (Module.catchARScanner) Module.catchARScanner.stop();
    var receiver = UTF8ToString(receiverPtr), session = Module.WebXR && Module.WebXR.xrSession;
    if (!session) return;
    var state = { active: true, source: null, raf: null, last: -Infinity, backend: 'hits', ids: new WeakMap(), next: 1, error: false };
    Module.catchARScanner = state;
    state.stop = function () {
      state.active = false;
      if (state.source) { try { state.source.cancel(); } catch (ended) {} state.source = null; }
      if (state.raf !== null) { try { session.cancelAnimationFrame(state.raf); } catch (ended) {} state.raf = null; }
      session.removeEventListener('end', state.stop);
    };
    session.addEventListener('end', state.stop);
    var ready = session.requestReferenceSpace('viewer');
    ready.then(function (viewer) {
      if (!state.active) return;
      if (!session.requestHitTestSource) { state.error = true; return; }
      return session.requestHitTestSource({space: viewer, entityTypes: ['plane']}).then(function (source) {
        if (state.active) state.source = source; else { try { source.cancel(); } catch (ended) {} }
      });
    }).catch(function () { state.error = true; });
    function world(m, p) {
      return { x: m[0]*p.x+m[4]*p.y+m[8]*p.z+m[12], y: m[1]*p.x+m[5]*p.y+m[9]*p.z+m[13], z: -(m[2]*p.x+m[6]*p.y+m[10]*p.z+m[14]) };
    }
    function tick(time, frame) {
      if (!state.active) return;
      state.raf = session.requestAnimationFrame(tick);
      if (time - state.last < (state.backend === 'planes' ? 250 : 100) || !session.refSpace) return;
      state.last = time;
      var packet = {backend:'hits', tracking:false, unavailable:false, planes:[], hits:[]};
      try {
        packet.tracking = !!frame.getViewerPose(session.refSpace);
        var planes; try { planes = frame.detectedPlanes; } catch (unsupported) {}
        if (planes !== undefined && planes !== null) packet.backend = 'planes';
        if (packet.tracking && packet.backend === 'planes') {
          planes.forEach(function (plane) {
            if (packet.planes.length >= 64 || plane.polygon.length < 3 || plane.polygon.length > 128) return;
            var pose = frame.getPose(plane.planeSpace, session.refSpace);
            if (!pose) return;
            var id = state.ids.get(plane);
            if (!id) { id = state.next++; state.ids.set(plane,id); }
            var m = pose.transform.matrix;
            if (Math.abs(m[5]) < .98480775) return; // within 10 degrees of horizontal
            packet.planes.push({id:id, normal:{x:m[4],y:m[5],z:-m[6]}, points:Array.from(plane.polygon, function(p) {return world(m,p);})});
          });
        } else if (packet.tracking && state.source) {
          var hits = frame.getHitTestResults(state.source);
          if (hits.length) {
            var hit = hits[0].getPose(session.refSpace);
            if (hit && Math.abs(hit.transform.matrix[5]) >= .98480775) {
              var matrix = hit.transform.matrix;
              packet.hits.push({position:{x:matrix[12],y:matrix[13],z:-matrix[14]},normal:{x:matrix[4],y:matrix[5],z:-matrix[6]}});
            }
          }
        }
        packet.unavailable = packet.backend === 'hits' && state.error;
      } catch (error) { packet.tracking=false; }
      state.backend = packet.backend;
      if (state.active) Module.SendMessage(receiver, 'ReceiveScanFrame', JSON.stringify(packet));
    }
    state.raf = session.requestAnimationFrame(tick);
  },
  CatchARScanStop: function () {
    if (Module.catchARScanner) { Module.catchARScanner.stop(); Module.catchARScanner = null; }
  }
});

