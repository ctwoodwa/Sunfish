import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { renderHook, act, waitFor } from '@testing-library/react';
import { useSystemRequirements } from './useSystemRequirements';
import { OverallVerdict } from '../contracts/SystemRequirements';

const VALID_RESULT = {
  overall: OverallVerdict.Pass,
  dimensions: [],
  evaluatedAt: '2026-05-12T00:00:00+00:00',
};

function makeFetch(response: { ok: boolean; status?: number; body?: unknown }) {
  return vi.fn().mockResolvedValue({
    ok: response.ok,
    status: response.status ?? (response.ok ? 200 : 500),
    json: () => Promise.resolve(response.body ?? VALID_RESULT),
  });
}

beforeEach(() => {
  vi.stubGlobal('fetch', makeFetch({ ok: true }));
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('useSystemRequirements', () => {
  it('resolves with result on success', async () => {
    const { result } = renderHook(() =>
      useSystemRequirements('com.example.app', 'macos'),
    );
    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(result.current.result?.overall).toBe(OverallVerdict.Pass);
    expect(result.current.error).toBeNull();
  });

  it('refresh() triggers a second fetch call', async () => {
    const fetchMock = makeFetch({ ok: true });
    vi.stubGlobal('fetch', fetchMock);

    const { result } = renderHook(() =>
      useSystemRequirements('com.example.app', 'macos'),
    );
    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(fetchMock).toHaveBeenCalledTimes(1);

    act(() => result.current.refresh());
    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(fetchMock).toHaveBeenCalledTimes(2);
  });

  it('sets error when response is non-ok (HTTP 500)', async () => {
    vi.stubGlobal('fetch', makeFetch({ ok: false, status: 500 }));

    const { result } = renderHook(() =>
      useSystemRequirements('com.example.app', 'macos'),
    );
    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(result.current.result).toBeNull();
    expect(result.current.error).toBeInstanceOf(Error);
    expect(result.current.error?.message).toMatch(/500/);
  });

  it('sets error immediately when bundleId fails allowlist validation', async () => {
    const { result } = renderHook(() =>
      useSystemRequirements('bad/bundle!id', 'macos'),
    );
    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(result.current.error).toBeInstanceOf(Error);
    expect(result.current.error?.message).toMatch(/bundleId/);
    expect(vi.mocked(fetch)).not.toHaveBeenCalled();
  });

  it('uses custom baseUrl when provided via options', async () => {
    const fetchMock = makeFetch({ ok: true });
    vi.stubGlobal('fetch', fetchMock);

    const { result } = renderHook(() =>
      useSystemRequirements('com.example.app', 'macos', {
        baseUrl: 'https://api.example.com/sysreq',
      }),
    );
    await waitFor(() => expect(result.current.loading).toBe(false));
    const calledUrl = fetchMock.mock.calls[0][0] as string;
    expect(calledUrl).toMatch(/^https:\/\/api\.example\.com\/sysreq\//);
  });
});
