import { useState, useEffect, useCallback } from 'react';
import { parseSystemRequirementsResult } from '../contracts/SystemRequirements';
import type { SystemRequirementsResult } from '../contracts/SystemRequirements';

// Security council amendment: allowlist pattern for path segment safety.
// encodeURIComponent already handles URL encoding, but we also reject strings
// that would produce misleading or multi-segment paths after encoding.
const SAFE_SEGMENT = /^[A-Za-z0-9._-]+$/;

function assertSafeSegment(value: string, name: string): void {
  if (!value || !SAFE_SEGMENT.test(value)) {
    throw new Error(
      `useSystemRequirements: ${name} must match ^[A-Za-z0-9._-]+$ (got: ${JSON.stringify(value)})`,
    );
  }
}

export interface UseSystemRequirementsResult {
  result: SystemRequirementsResult | null;
  loading: boolean;
  error: Error | null;
  refresh: () => void;
}

export interface UseSystemRequirementsOptions {
  /** Base URL for the system-requirements API endpoint. Defaults to '/api/system-requirements'. */
  baseUrl?: string;
}

export function useSystemRequirements(
  bundleId: string,
  platformKey: string,
  options: UseSystemRequirementsOptions = {},
): UseSystemRequirementsResult {
  const { baseUrl = '/api/system-requirements' } = options;

  const [result, setResult] = useState<SystemRequirementsResult | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<Error | null>(null);
  const [tick, setTick] = useState(0);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);

    let url: string;
    try {
      assertSafeSegment(bundleId, 'bundleId');
      assertSafeSegment(platformKey, 'platformKey');
      url = `${baseUrl}/${encodeURIComponent(bundleId)}?platform=${encodeURIComponent(platformKey)}`;
    } catch (err: unknown) {
      if (!cancelled) {
        setError(err instanceof Error ? err : new Error(String(err)));
        setLoading(false);
      }
      return () => { cancelled = true; };
    }

    fetch(url)
      .then((res) => {
        if (!res.ok) throw new Error(`HTTP ${res.status} from ${url}`);
        return res.json();
      })
      .then((json: unknown) => {
        if (!cancelled) setResult(parseSystemRequirementsResult(json));
      })
      .catch((err: unknown) => {
        if (!cancelled) setError(err instanceof Error ? err : new Error(String(err)));
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => { cancelled = true; };
  }, [bundleId, platformKey, baseUrl, tick]);

  const refresh = useCallback(() => setTick((t) => t + 1), []);

  return { result, loading, error, refresh };
}
