'use strict';

// Pre-built once at module load time (not regenerated per digest) so it is safe
// to bind directly in ng-options/ng-repeat without triggering AngularJS's
// infinite-digest guard, which fires when a watched collection is a new array
// of new objects on every check.
angular.module('learnSphereApp')
.constant('SubjectCatalog', (function () {
  function flatten(examDefs) {
    var out = [];
    examDefs.forEach(function (def) {
      var subjects = def.compulsory.concat(def.elective);
      subjects.forEach(function (subject) {
        def.levels.forEach(function (level) {
          out.push({
            examType: def.examType,
            subject: subject,
            level: level,
            label: subject + '(' + level + ')'
          });
        });
      });
    });
    return out;
  }

  return {
    Singapore: flatten([
      {
        examType: 'PSLE',
        levels: ['Primary 1', 'Primary 2', 'Primary 3', 'Primary 4', 'Primary 5', 'Primary 6'],
        compulsory: ['English Language', 'Mathematics', 'Mother Tongue (Chinese)', 'Mother Tongue (Malay)', 'Mother Tongue (Tamil)'],
        elective: ['Science']
      },
      {
        examType: 'N/O-Level',
        levels: ['Secondary 1', 'Secondary 2', 'Secondary 3', 'Secondary 4', 'Secondary 5'],
        compulsory: ['English Language', 'Mathematics', 'Mother Tongue (Chinese)', 'Mother Tongue (Malay)', 'Mother Tongue (Tamil)'],
        elective: ['Additional Mathematics', 'Combined Science', 'Physics', 'Chemistry', 'Biology', 'History', 'Geography', 'Social Studies', 'Literature in English', 'Principles of Accounts', 'Computing', 'Art', 'Music', 'Design & Technology', 'Food & Nutrition']
      },
      {
        examType: 'A-Level',
        levels: ['JC1', 'JC2'],
        compulsory: ['General Paper'],
        elective: ['H2 Mathematics', 'H2 Further Mathematics', 'H2 Physics', 'H2 Chemistry', 'H2 Biology', 'H2 Economics', 'H2 History', 'H2 Geography', 'H2 Literature in English', 'H1 Project Work']
      }
    ]),
    Malaysia: flatten([
      {
        examType: 'UPSR',
        levels: ['Primary 1', 'Primary 2', 'Primary 3', 'Primary 4', 'Primary 5', 'Primary 6'],
        compulsory: ['Bahasa Malaysia', 'English', 'Mathematics'],
        elective: ['Science', 'History']
      },
      {
        examType: 'PT3',
        levels: ['Form 1', 'Form 2', 'Form 3'],
        compulsory: ['Bahasa Malaysia', 'English', 'Mathematics', 'Science'],
        elective: ['History', 'Geography', 'Islamic Education', 'Moral Education', 'Design & Technology']
      },
      {
        examType: 'SPM',
        levels: ['Form 4', 'Form 5'],
        compulsory: ['Bahasa Malaysia', 'English', 'Mathematics'],
        elective: ['Additional Mathematics', 'Physics', 'Chemistry', 'Biology', 'History', 'Geography', 'Economics', 'Accounting', 'Commerce', 'Business Studies', 'Information Technology', 'Moral Education', 'Islamic Studies', 'Art & Design']
      },
      {
        examType: 'STPM',
        levels: ['Lower Six', 'Upper Six'],
        compulsory: ['General Studies'],
        elective: ['Mathematics (T)', 'Further Mathematics (T)', 'Physics', 'Chemistry', 'Biology', 'Economics', 'History', 'Geography', 'Literature in English', 'Accounting', 'Business Studies']
      }
    ])
  };
})());
