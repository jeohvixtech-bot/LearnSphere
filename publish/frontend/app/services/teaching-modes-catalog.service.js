'use strict';

// Pre-built once at module load time (not regenerated per digest) — same reasoning as
// SubjectCatalog: a fresh array of fresh objects on every digest trips AngularJS's
// infinite-digest guard when bound directly in ng-repeat.
angular.module('learnSphereApp')
.constant('TeachingModesCatalog', [
  { mode: 'Online', description: 'Remote via video call', icon: 'ti-video' },
  { mode: 'Tutor Place', description: "At the tutor's own place", icon: 'ti-map-pin' },
  { mode: 'Tuition Center', description: 'At a learning center', icon: 'ti-building' }
]);
