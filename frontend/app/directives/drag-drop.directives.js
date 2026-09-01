'use strict';

// Minimal native HTML5 Drag-and-Drop wrappers — no external DnD library.
// dndDraggable: makes an element draggable and carries its bound item as JSON
// payload via the browser's native dataTransfer channel.
// dndDropzone: accepts a drop and hands the parsed item back to the given
// expression as `item`.
//
// Neither directive takes an isolate scope — the ordered pool (Component 2) needs
// both dnd-draggable and dnd-dropzone on the very same row (drag to reorder by
// dropping on another row), and two isolate-scope directives on one element is an
// Angular compile error ("Multiple directives asking for new/isolated scope"),
// which silently breaks that repeated row instead of throwing somewhere obvious.
angular.module('learnSphereApp')
.directive('dndDraggable', ['$parse', function ($parse) {
  return {
    restrict: 'A',
    link: function (scope, element, attrs) {
      var getItem = $parse(attrs.dndDraggable);
      element.attr('draggable', 'true');
      element.on('dragstart', function (e) {
        var ev = e.originalEvent || e;
        ev.dataTransfer.effectAllowed = 'move';
        ev.dataTransfer.setData('text/plain', JSON.stringify(getItem(scope)));
        element.addClass('dnd-dragging');
      });
      element.on('dragend', function () {
        element.removeClass('dnd-dragging');
      });
    }
  };
}])
.directive('dndDropzone', ['$parse', function ($parse) {
  return {
    restrict: 'A',
    link: function (scope, element, attrs) {
      var dropFn = $parse(attrs.dndDrop);
      element.on('dragover', function (e) {
        e.preventDefault();
        var ev = e.originalEvent || e;
        ev.dataTransfer.dropEffect = 'move';
        element.addClass('dnd-drag-over');
      });
      element.on('dragleave', function (e) {
        if (e.target === element[0]) element.removeClass('dnd-drag-over');
      });
      element.on('drop', function (e) {
        e.preventDefault();
        e.stopPropagation();
        element.removeClass('dnd-drag-over');
        var ev = e.originalEvent || e;
        var raw = ev.dataTransfer.getData('text/plain');
        if (!raw) return;
        var item = JSON.parse(raw);
        scope.$apply(function () {
          dropFn(scope, { item: item });
        });
      });
    }
  };
}]);
