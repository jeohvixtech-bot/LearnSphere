'use strict';

// Terms and Conditions: fetch/accept/publish against the API, and parse the plain-text
// format into render-ready blocks. The text is NEVER inserted as HTML — each block is
// rendered through normal Angular bindings, so an admin cannot inject markup or script.
//
// Format: "## Heading" = heading, "- item" = bullet, blank line = paragraph break,
// **bold** = bold, email addresses become mailto links.
angular.module('learnSphereApp')
.service('TermsService', ['$http', 'API_URL', 'AuthService', function ($http, API_URL, AuthService) {
  var self = this;
  var h = function () { return { headers: AuthService.authHeader() }; };

  self.getCurrent = function () {
    return $http.get(API_URL + '/terms/current').then(function (res) { return res.data; });
  };

  self.accept = function (termsVersionId) {
    return $http.post(API_URL + '/terms/accept', { termsVersionId: termsVersionId }, h());
  };

  self.getHistory = function () {
    return $http.get(API_URL + '/terms/history', h()).then(function (res) { return res.data; });
  };

  self.publish = function (version, content) {
    return $http.post(API_URL + '/terms/publish', { version: version, content: content }, h());
  };

  // Splits a line into plain / bold / mailto segments.
  var INLINE = /(\*\*[^*]+\*\*|[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,})/;
  function segments(text) {
    return text.split(INLINE).filter(function (p) { return p !== ''; }).map(function (p) {
      if (/^\*\*[^*]+\*\*$/.test(p)) return { t: p.slice(2, -2), bold: true };
      if (/^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$/.test(p)) return { t: p, mail: true };
      return { t: p };
    });
  }

  // content (string) -> [{ type: 'h' | 'p' | 'li', segs: [...] }]
  self.parse = function (content) {
    var blocks = [];
    var para = null; // open paragraph/bullet still accepting continuation lines

    function flush() {
      if (para) { blocks.push({ type: para.type, segs: segments(para.text) }); para = null; }
    }

    String(content || '').split(/\r?\n/).forEach(function (raw) {
      var line = raw.trim();
      if (line === '') { flush(); return; }
      if (line.indexOf('## ') === 0) { flush(); blocks.push({ type: 'h', segs: segments(line.slice(3)) }); return; }
      if (line.indexOf('- ') === 0) { flush(); para = { type: 'li', text: line.slice(2) }; return; }
      if (para) para.text += ' ' + line;
      else para = { type: 'p', text: line };
    });
    flush();
    return blocks;
  };
}]);
