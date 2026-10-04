'use strict';

// <span ls-clock></span> — live "Sat, 4 Oct 2026 · 3:42 PM" text, ticking once
// per minute on the minute. Deliberately writes textContent from a plain
// setTimeout instead of $interval + {{ binding }}: $interval would run a full
// $rootScope digest every tick, re-evaluating every watcher on heavy pages like
// the tutor overview, while this touches only its own text node. Minute
// granularity (no seconds) keeps it to ~1 DOM write per minute.
angular.module('learnSphereApp')
.directive('lsClock', function () {
  // Built by hand rather than Intl.DateTimeFormat, whose punctuation/casing
  // differs between browsers ("Sun, 4 Oct" vs "Sun 4 Oct", "pm" vs "PM").
  var DAYS = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
  var MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
  function format(d) {
    var h = d.getHours(), m = d.getMinutes();
    return DAYS[d.getDay()] + ', ' + d.getDate() + ' ' + MONTHS[d.getMonth()] + ' ' + d.getFullYear() +
      ' · ' + (h % 12 || 12) + ':' + (m < 10 ? '0' : '') + m + ' ' + (h < 12 ? 'AM' : 'PM');
  }
  return {
    restrict: 'A',
    link: function (scope, element) {
      var timer = null;
      function render() {
        var now = new Date();
        element[0].textContent = format(now);
        // Re-arm for the start of the next minute, so the display never lags.
        timer = setTimeout(render, 60000 - (now.getSeconds() * 1000 + now.getMilliseconds()) + 50);
      }
      render();
      scope.$on('$destroy', function () { clearTimeout(timer); });
    }
  };
});
