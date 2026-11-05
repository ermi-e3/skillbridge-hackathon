export interface CountdownDetails {
  days: number;
  hours: number;
  minutes: number;
  seconds: number;
  formattedDays: string;
  formattedHours: string;
  formattedMinutes: string;
  formattedSeconds: string;
  totalMilliseconds: number;
  isUrgent: boolean; // 24 hours or less
  isExpired: boolean;
  daysRemainingText: string;
}

export function calculateCountdown(deadlineStr?: string | null, isOpen: boolean = true): CountdownDetails {
  if (!deadlineStr || !isOpen) {
    const isClosed = !isOpen;
    return {
      days: 0,
      hours: 0,
      minutes: 0,
      seconds: 0,
      formattedDays: '00',
      formattedHours: '00',
      formattedMinutes: '00',
      formattedSeconds: '00',
      totalMilliseconds: 0,
      isUrgent: false,
      isExpired: isClosed,
      daysRemainingText: isClosed ? 'Closed' : 'No deadline',
    };
  }

  const deadlineDate = new Date(deadlineStr).getTime();
  const now = Date.now();
  const diff = deadlineDate - now;

  if (diff <= 0) {
    return {
      days: 0,
      hours: 0,
      minutes: 0,
      seconds: 0,
      formattedDays: '00',
      formattedHours: '00',
      formattedMinutes: '00',
      formattedSeconds: '00',
      totalMilliseconds: 0,
      isUrgent: false,
      isExpired: true,
      daysRemainingText: 'Expired',
    };
  }

  const oneDayMs = 24 * 60 * 60 * 1000;
  const oneHourMs = 60 * 60 * 1000;
  const oneMinuteMs = 60 * 1000;

  const days = Math.floor(diff / oneDayMs);
  const hours = Math.floor((diff % oneDayMs) / oneHourMs);
  const minutes = Math.floor((diff % oneHourMs) / oneMinuteMs);
  const seconds = Math.floor((diff % oneMinuteMs) / 1000);

  const isUrgent = diff <= oneDayMs; // 24 hours or less

  let daysRemainingText: string;
  if (days >= 1) {
    daysRemainingText = `${days} ${days === 1 ? 'day' : 'days'} left`;
  } else {
    daysRemainingText = hours >= 1 ? `${hours}h left` : `${minutes}m left`;
  }

  const pad = (n: number) => n.toString().padStart(2, '0');

  return {
    days,
    hours,
    minutes,
    seconds,
    formattedDays: pad(days),
    formattedHours: pad(hours),
    formattedMinutes: pad(minutes),
    formattedSeconds: pad(seconds),
    totalMilliseconds: diff,
    isUrgent,
    isExpired: false,
    daysRemainingText,
  };
}
