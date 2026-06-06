import { Tutor, Student, Booking, ChatMessage, NotificationItem, Payout, Invoice } from './types';

export const INITIAL_TUTORS: Tutor[] = [
  {
    id: 'tutor-1',
    name: 'Mr. Lim Wei Sheng',
    imageUrl: 'https://images.unsplash.com/photo-1544717305-2782549b5136?auto=format&fit=crop&q=80&w=250',
    rating: 4.9,
    reviewCount: 128,
    subjects: ['Mathematics', 'Additional Mathematics', 'Physics'],
    levels: ['Secondary 1-4', 'JC 1-2'],
    modes: ['Online', 'Home Visit'],
    pricePerSession: 50,
    experienceYears: 8,
    bio: 'Passionate in helping students build strong foundations in Math. My teaching style is patient, structured and results-oriented.',
    qualifications: ['B.Sc. Mathematics (NUS)', 'PGDE (NIE)', '8 years exp - MOE Teacher'],
    reviews: [
      { author: 'Parent of Jessica T.', text: 'Great tutor! My daughter\'s grades improved significantly from C to A! Highly recommend.', rating: 5 },
      { author: 'Student Ethan L.', text: 'Mr. Lim explains complex equations very clearly. The practice sheets are super useful.', rating: 5 }
    ],
    timetable: [
      { id: 'lim-1', day: 'Monday', time: '04:00 PM - 06:00 PM', status: 'Available' },
      { id: 'lim-2', day: 'Wednesday', time: '05:00 PM - 07:00 PM', status: 'Available' },
      { id: 'lim-3', day: 'Friday', time: '04:00 PM - 06:00 PM', status: 'Available' },
      { id: 'lim-4', day: 'Saturday', time: '10:00 AM - 12:00 PM', status: 'Available' }
    ]
  },
  {
    id: 'tutor-2',
    name: 'Ms. Rachel Wong',
    imageUrl: 'https://images.unsplash.com/photo-1573496359142-b8d87734a5a2?auto=format&fit=crop&q=80&w=250',
    rating: 4.8,
    reviewCount: 94,
    subjects: ['Chemistry', 'Physics', 'Science'],
    levels: ['Secondary 2-4'],
    modes: ['Online', 'Tutor Place'],
    pricePerSession: 60,
    experienceYears: 6,
    bio: 'Interactive science lessons focusing on conceptual clarity and real-life examples. Specialize in preparation for national exams.',
    qualifications: ['M.Sc. Chemistry (NTU)', '6 years active tutoring experience'],
    reviews: [
      { author: 'Parent of Marcus K.', text: 'Very engaging lessons. Marcus hated chemistry but now finds it interesting. Thank you Rachel!', rating: 5 }
    ],
    timetable: [
      { id: 'wong-1', day: 'Tuesday', time: '06:00 PM - 08:00 PM', status: 'Available' },
      { id: 'wong-2', day: 'Thursday', time: '04:00 PM - 06:00 PM', status: 'Available' },
      { id: 'wong-3', day: 'Saturday', time: '02:00 PM - 04:00 PM', status: 'Available' }
    ]
  },
  {
    id: 'tutor-3',
    name: 'Mr. Daniel Tan',
    imageUrl: 'https://images.unsplash.com/photo-1500648767791-00dcc994a43e?auto=format&fit=crop&q=80&w=250',
    rating: 4.7,
    reviewCount: 76,
    subjects: ['Physics', 'Science'],
    levels: ['Secondary 4 - JC'],
    modes: ['Home Visit', 'Online'],
    pricePerSession: 55,
    experienceYears: 10,
    bio: 'Former MOE Physics teacher specializing in fast-track improvements for O-Level and A-Level students.',
    qualifications: ['B.Sc. Physics (NUS)', '10 years exp - ex-MOE Subject Head'],
    reviews: [
      { author: 'Parent of Keith M.', text: 'Expert physics tutor. Clear-cut methodology for answering difficult calculation questions.', rating: 4 }
    ],
    timetable: [
      { id: 'tan-1', day: 'Monday', time: '05:00 PM - 07:00 PM', status: 'Available' },
      { id: 'tan-2', day: 'Thursday', time: '05:00 PM - 07:00 PM', status: 'Available' },
      { id: 'tan-3', day: 'Sunday', time: '09:00 AM - 11:00 AM', status: 'Available' }
    ]
  }
];

export const INITIAL_STUDENTS: Student[] = [
  {
    id: 'student-1',
    name: 'Jessica Tan',
    birthDate: '2012-05-14',
    school: 'CHIJ Secondary School',
    educationLevel: 'Secondary 3',
    subjectSelect: 'Mathematics',
    learningGoal: 'Improve algebra speed and prepare for end-of-year examinations.',
    photoUrl: 'https://images.unsplash.com/photo-1438761681033-6461ffad8d80?auto=format&fit=crop&q=80&w=150'
  }
];

export const INITIAL_BOOKINGS: Booking[] = [];

export const INITIAL_MESSAGES: ChatMessage[] = [];

export const INITIAL_NOTIFICATIONS: NotificationItem[] = [
  {
    id: 'notif-1',
    title: 'Booking Confirmed',
    message: 'Mr. Lim contains confirmed your tutorial request for Jessica on Friday June 5th.',
    timestamp: '2026-06-04 09:12 AM',
    type: 'booking',
    read: false
  },
  {
    id: 'notif-2',
    title: 'Lesson Report Submitted',
    message: 'Mr. Lim has submitted the detailed lesson report for tutorial on 2026-05-28.',
    timestamp: '2026-05-28 06:15 PM',
    type: 'system',
    read: true
  },
  {
    id: 'notif-3',
    title: 'Payment Successful',
    message: 'Your monthly subscription of $200 has been successfully debited.',
    timestamp: '2026-05-15 08:00 AM',
    type: 'payment',
    read: true
  }
];

export const INITIAL_PAYOUTS: Payout[] = [];

export const INITIAL_INVOICES: Invoice[] = [];
