// The backend serializes RankingRowDto.ResolutionTime as a raw .NET TimeSpan — "[d.]hh:mm:ss[.fffffff]",
// e.g. "1.05:05:07.5499560" — which is unreadable rendered verbatim in the standings. This turns it into
// a compact duration.
//
// Display only: RB-08 ordering and Position are decided by the backend, so dropping the fractional
// seconds here cannot change the ranking. Two rows separated only by ticks still arrive pre-ordered and
// correctly numbered; they just read as the same duration.
//
// An unrecognized value degrades to the raw string rather than throwing or rendering nothing — an
// unexpected server format should still show the operator something. Negative spans are not handled
// because the domain rejects them (ResolutionTime.Comparable), so they degrade like any other mismatch.
const DOTNET_TIMESPAN = /^(?:(\d+)\.)?(\d{1,2}):([0-5]\d):([0-5]\d)(?:\.(\d{1,7}))?$/

function pad(value: number): string {
  return String(value).padStart(2, '0')
}

export function formatResolutionTime(raw: string): string {
  const match = DOTNET_TIMESPAN.exec(raw.trim())
  if (match === null) return raw

  const days = Number(match[1] ?? 0)
  const hours = Number(match[2])
  const minutes = Number(match[3])
  const seconds = Number(match[4])

  // Largest two-to-three units only: a resolution time is read at a glance to compare teams, so
  // trailing precision the operator cannot act on is noise.
  if (days > 0) return `${days}d ${pad(hours)}h ${pad(minutes)}m`
  if (hours > 0) return `${hours}h ${pad(minutes)}m ${pad(seconds)}s`
  if (minutes > 0) return `${minutes}m ${pad(seconds)}s`
  return `${seconds}s`
}
