import { calculateCountdown } from './countdown';

describe('calculateCountdown', () => {
  it('should return No deadline when deadline is not provided and isOpen is true', () => {
    const result = calculateCountdown(null, true);
    expect(result.daysRemainingText).toBe('No deadline');
    expect(result.isExpired).toBe(false);
    expect(result.isUrgent).toBe(false);
  });

  it('should return Closed when isOpen is false', () => {
    const futureDate = new Date(Date.now() + 5 * 24 * 60 * 60 * 1000).toISOString();
    const result = calculateCountdown(futureDate, false);
    expect(result.daysRemainingText).toBe('Closed');
    expect(result.isExpired).toBe(true);
    expect(result.isUrgent).toBe(false);
  });

  it('should return Expired when deadline is in the past', () => {
    const pastDate = new Date(Date.now() - 3600 * 1000).toISOString();
    const result = calculateCountdown(pastDate, true);
    expect(result.daysRemainingText).toBe('Expired');
    expect(result.isExpired).toBe(true);
    expect(result.isUrgent).toBe(false);
  });

  it('should calculate days remaining when more than 24 hours left', () => {
    const fourDaysLater = new Date(Date.now() + 4 * 24 * 60 * 60 * 1000 + 2000).toISOString();
    const result = calculateCountdown(fourDaysLater, true);
    expect(result.days).toBe(4);
    expect(result.daysRemainingText).toBe('4 days left');
    expect(result.isUrgent).toBe(false);
    expect(result.isExpired).toBe(false);
    expect(result.formattedDays).toBe('04');
  });

  it('should flag urgent and show hours left when 24 hours or less left', () => {
    const eighteenHoursLater = new Date(Date.now() + 18 * 60 * 60 * 1000 + 5000).toISOString();
    const result = calculateCountdown(eighteenHoursLater, true);
    expect(result.isUrgent).toBe(true);
    expect(result.daysRemainingText).toBe('18h left');
    expect(result.isExpired).toBe(false);
  });

  it('should format padded digits for days, hours, minutes, seconds', () => {
    const target = new Date(Date.now() + (2 * 86400 + 3 * 3600 + 4 * 60 + 5) * 1000).toISOString();
    const result = calculateCountdown(target, true);
    expect(result.formattedDays).toBe('02');
    expect(result.formattedHours).toBe('03');
    expect(result.formattedMinutes).toBe('04');
  });
});
