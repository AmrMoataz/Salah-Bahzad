import { Injectable, inject } from '@angular/core';
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { StudentAuthStore } from '@sb/student-portal/data-access';

/**
 * The **`QuizHub`** SignalR client (contract §A.1, `FR-STU-QZ-004`) — its **sole** job is to arm
 * **forfeit-on-disconnect** for the active sitting. The hub is **not** a data channel: it pushes
 * **nothing** (no server→client ticks, no client→server methods); the countdown is the runner's
 * **local** timer, authoritatively backstopped by the server-side Hangfire job (§C).
 *
 * **Lifecycle = the forfeit:**
 * - {@link open} the connection **immediately after** the REST `start(quizId)` (#2) resolves — the hub
 *   binds this connection to the active attempt on `OnConnectedAsync`, so the attempt must already exist.
 * - {@link close} on a clean submit / leave-confirm / `ngOnDestroy`.
 * - **The disconnect IS the forfeit.** There is **NO** `withAutomaticReconnect()` — by the time a
 *   reconnect fired the server would have already forfeited the attempt (score 0, consumed). A silent
 *   re-bind would lie to the student (§A.1). Single-shot: connect on start, tear down on exit; a
 *   mid-sitting socket drop **is** the forfeit.
 *
 * Root-provided (one active sitting at a time, by the engine's invariant) and behind a DI seam so the
 * Jest runner specs assert "opened on start / closed on submit/leave" without a real WebSocket.
 */
@Injectable({ providedIn: 'root' })
export class QuizHubClient {
  readonly #authStore = inject(StudentAuthStore);
  #connection: HubConnection | null = null;

  /** Open the forfeit-arming connection (idempotent). The JWT rides the SignalR `access_token` query. */
  open(): void {
    if (this.#connection) return;
    const connection = new HubConnectionBuilder()
      .withUrl(`${this.#apiUrl()}/hubs/quiz`, {
        // The platform JWT rides the SignalR `access_token` query, scoped to the hub path (§A.1).
        accessTokenFactory: () => this.#authStore.getAccessToken() ?? '',
      })
      .configureLogging(LogLevel.None)
      // NO .withAutomaticReconnect() — a drop IS the forfeit (§A.1); never imply the attempt survives.
      .build();
    this.#connection = connection;
    // A failed connect simply leaves the forfeit unarmed — the server Hangfire timer still backstops
    // the sitting; the runner keeps working. Never block the runner on the socket.
    connection.start().catch(() => {
      /* unarmed — server timer backstops; the live runner is unaffected */
    });
  }

  /** Tear down the connection (idempotent) — a clean exit, NOT a forfeit. */
  close(): void {
    const connection = this.#connection;
    this.#connection = null;
    if (connection) {
      void connection.stop().catch(() => {
        /* already closing/closed */
      });
    }
  }

  #apiUrl(): string {
    // A relative `/hubs/quiz` lets the dev proxy (ws:true) handle it; the shim sets the prod origin.
    return (window as unknown as { __SB_API_URL__?: string }).__SB_API_URL__ ?? '';
  }
}
