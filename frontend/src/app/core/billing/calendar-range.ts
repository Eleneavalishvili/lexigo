export function calendarRange(from: string, to: string): { from?: string; to?: string } {
  if (from && to && from > to) throw new Error('The end date must be on or after the start date.');
  const start = from ? new Date(`${from}T00:00:00`) : null;
  const end = to ? new Date(`${to}T00:00:00`) : null;
  if ((start && !Number.isFinite(start.getTime())) || (end && !Number.isFinite(end.getTime())))
    throw new Error('Choose valid start and end dates.');
  if (end) end.setDate(end.getDate() + 1);
  return { from: start?.toISOString(), to: end?.toISOString() };
}
