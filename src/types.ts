export interface TimeSlot {
  id: string;
  day: 'Monday' | 'Tuesday' | 'Wednesday' | 'Thursday' | 'Friday' | 'Saturday' | 'Sunday';
  time: string; // e.g. "04:00 PM - 06:00 PM"
  status: 'Available' | 'Booked';
  bookingId?: string;
}

export interface Tutor {
  id: string;
  name: string;
  imageUrl: string;
  rating: number;
  reviewCount: number;
  subjects: string[];
  levels: string[];
  modes: string[]; // e.g. "Online", "Home Visit"
  pricePerSession: number;
  experienceYears: number;
  bio: string;
  qualifications: string[];
  reviews: Array<{
    author: string;
    text: string;
    rating: number;
  }>;
  timetable?: TimeSlot[];
}

export interface Student {
  id: string;
  name: string;
  birthDate: string;
  school: string;
  educationLevel: string;
  subjectSelect: string;
  learningGoal?: string;
  photoUrl?: string;
}

export interface CounterProposalDetails {
  date: string;
  time: string;
  message: string;
}

export interface LessonReport {
  id: string; // matches parent booking
  covered: string;
  performance: string;
  homework: string;
  submitDate: string;
  editHistory?: Array<{
    date: string;
    changes: string;
  }>;
}

export interface Booking {
  id: string;
  tutorId: string;
  studentId: string;
  subject: string;
  mode: string;
  date: string;
  time: string;
  durationHours: number;
  message?: string;
  totalPrice: number;
  status: 'pending' | 'countered' | 'confirmed' | 'completed' | 'cancelled';
  counterProposal?: CounterProposalDetails;
  lessonReport?: LessonReport;
  issueReported?: {
    issueType: string;
    details: string;
    timestamp: string;
  };
  slotId?: string;
}

export interface ChatMessage {
  id: string;
  tutorId: string;
  sender: 'parent' | 'tutor' | 'system';
  text: string;
  timestamp: string;
}

export interface NotificationItem {
  id: string;
  title: string;
  message: string;
  timestamp: string;
  type: 'booking' | 'message' | 'payment' | 'system';
  read: boolean;
}

export interface Payout {
  id: string;
  date: string;
  amount: number;
  status: 'Paid' | 'Processing';
}

export interface Invoice {
  id: string;
  date: string;
  amount: number;
  status: 'Paid' | 'Unpaid';
  bookingId?: string;
  subject?: string;
}
