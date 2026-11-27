mergeInto(LibraryManager.library, {
  CatchARQuit: function () {
    setTimeout(function () {
      if (window.catchARExit) window.catchARExit();
    }, 0);
  }
});
