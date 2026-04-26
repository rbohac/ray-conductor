import { useEffect, useRef, useState } from 'react';

export type SseEventHandler = (event: string, data: unknown) => void;

interface UseSseOptions {
  /**
   * Optional set of named events to subscribe to. If omitted, the handler
   * receives every named event the server emits.
   */
  events?: readonly string[];
  /**
   * Called for every named event from the server with the parsed JSON
   * payload. The reference is read on each call, so passing a freshly
   * constructed function each render is fine.
   */
  onEvent: SseEventHandler;
  /**
   * Called once when the connection opens (after `: connected` from server).
   */
  onOpen?: () => void;
  /**
   * Called when the connection drops permanently.
   */
  onError?: (err: unknown) => void;
}

/**
 * Subscribes to a Maestro SSE endpoint. Reconnects are handled by the
 * browser's EventSource implementation. Cleans up on unmount.
 */
export function useSse(url: string | null, opts: UseSseOptions) {
  const [connected, setConnected] = useState(false);
  const handlerRef = useRef(opts.onEvent);
  handlerRef.current = opts.onEvent;
  const onOpenRef = useRef(opts.onOpen);
  onOpenRef.current = opts.onOpen;
  const onErrorRef = useRef(opts.onError);
  onErrorRef.current = opts.onError;

  useEffect(() => {
    if (!url) return;
    const es = new EventSource(url);
    let closed = false;

    const events = opts.events ?? ['agent-output', 'agent-status', 'worktree-changed', 'diff-changed'];

    function dispatch(evt: MessageEvent, name: string) {
      try {
        const parsed = JSON.parse(evt.data);
        handlerRef.current(name, parsed);
      } catch {
        handlerRef.current(name, evt.data);
      }
    }

    for (const name of events) {
      es.addEventListener(name, (e) => dispatch(e as MessageEvent, name));
    }

    es.onopen = () => {
      if (closed) return;
      setConnected(true);
      onOpenRef.current?.();
    };
    es.onerror = (err) => {
      if (closed) return;
      setConnected(false);
      onErrorRef.current?.(err);
    };

    return () => {
      closed = true;
      es.close();
      setConnected(false);
    };
    // We intentionally only re-subscribe on URL change. The handler is read
    // via ref so its identity may change freely.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [url]);

  return { connected };
}
