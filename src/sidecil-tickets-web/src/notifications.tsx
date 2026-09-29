import { useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useQueryClient } from "@tanstack/react-query";
import { Alert, Button, MenuItem, Snackbar, TextField } from "@mui/material";
import { api, type NotificationSound, type User } from "./api";
import { ErrorBox } from "./shared";

const sounds: Record<NotificationSound, string> = {
  Off: "Sin sonido",
  Chime: "Campanilla",
  Bell: "Timbre",
  Soft: "Suave",
};

export async function playNotificationSound(sound: NotificationSound) {
  if (sound === "Off") return;
  try {
    const context = new AudioContext();
    await context.resume();
    const notes = sound === "Bell" ? [659, 880, 659] : sound === "Soft" ? [440, 554] : [523, 659, 784];
    const gain = context.createGain();
    gain.gain.value = sound === "Soft" ? 0.09 : 0.16;
    gain.connect(context.destination);
    const now = context.currentTime;
    notes.forEach((frequency, index) => {
      const oscillator = context.createOscillator();
      const envelope = context.createGain();
      oscillator.type = "sine";
      oscillator.frequency.value = frequency;
      oscillator.connect(envelope);
      envelope.connect(gain);
      const start = now + index * 0.14;
      envelope.gain.setValueAtTime(0.0001, start);
      envelope.gain.exponentialRampToValueAtTime(0.9, start + 0.025);
      envelope.gain.exponentialRampToValueAtTime(0.0001, start + 0.25);
      oscillator.start(start);
      oscillator.stop(start + 0.26);
    });
    window.setTimeout(() => void context.close(), 1200);
  } catch {
    // El navegador puede impedir audio hasta la primera interacción del usuario.
  }
}

type Notice = { eventId: number; ticketId: string; number: string; subject: string; kind: "ticket" | "reply" };
type NotificationResult = { cursor: number; items: Notice[] };

export function AgentNotifications({ user }: { user: User }) {
  const navigate = useNavigate();
  const client = useQueryClient();
  const [pending, setPending] = useState<Notice[]>([]);
  const played = useRef(0);
  const active = pending[0];

  useEffect(() => {
    let stopped = false;
    let cursor: number | null = null;
    let timer: number;
    async function poll() {
      try {
        const response = await api<NotificationResult>(
          "/notifications" + (cursor === null ? "" : "?after=" + cursor),
          { cache: "no-store" },
        );
        if (stopped) return;
        const previous = cursor;
        cursor = Math.max(cursor ?? 0, response.data.cursor);
        if (previous !== null && response.data.items.length) {
          setPending((current) => [...current, ...response.data.items.filter((item) => item.eventId > previous)]);
          void client.invalidateQueries({ queryKey: ["tickets"] });
          void client.invalidateQueries({ queryKey: ["ticket"] });
        }
      } catch {
        // Reintenta en el siguiente ciclo sin interrumpir el trabajo del agente.
      } finally {
        if (!stopped) timer = window.setTimeout(poll, 5000);
      }
    }
    void poll();
    return () => { stopped = true; window.clearTimeout(timer); };
  }, [user.id, client]);

  useEffect(() => {
    if (active && played.current !== active.eventId) {
      played.current = active.eventId;
      void playNotificationSound(user.notificationSound || "Off");
    }
  }, [active, user.notificationSound]);

  function dismiss() { setPending((current) => current.slice(1)); }
  return (
    <Snackbar key={active?.eventId ?? "empty"} open={!!active} autoHideDuration={9000}
      anchorOrigin={{ vertical: "top", horizontal: "right" }} onClose={dismiss}>
      <Alert severity="info" variant="filled"
        action={<><Button color="inherit" size="small" onClick={() => { if (active) navigate("/tickets/" + active.ticketId); dismiss(); }}>Abrir</Button><Button color="inherit" size="small" onClick={dismiss}>Cerrar</Button></>}
        sx={{ maxWidth: 410, alignItems: "center" }}>
        <strong>{active?.kind === "ticket" ? "Nuevo ticket" : "Nueva respuesta"} · {active?.number}</strong>
        <div>{active?.subject}</div>
      </Alert>
    </Snackbar>
  );
}

export function NotificationSettings({ user }: { user: User }) {
  const client = useQueryClient();
  const [sound, setSound] = useState<NotificationSound>(user.notificationSound || "Off");
  const [busy, setBusy] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<unknown>();
  useEffect(() => setSound(user.notificationSound || "Off"), [user.notificationSound]);
  return (
    <section className="account-card">
      <h2>Avisos de nuevos tickets y mensajes</h2>
      <p>Mientras tengas el sistema abierto, una burbuja te avisará de los tickets {user.role === "Admin" ? "del equipo" : "de tus módulos"} y de las respuestas de clientes. El sonido es opcional.</p>
      <div className="form-fields">
        <TextField select label="Sonido de las alertas" value={sound} disabled={busy}
          onChange={(event) => { setSound(event.target.value as NotificationSound); setSaved(false); }}>
          {Object.entries(sounds).map(([value, label]) => <MenuItem key={value} value={value}>{label}</MenuItem>)}
        </TextField>
        <small>Para comprobar el audio en este navegador, elige un sonido y pulsa «Escuchar prueba».</small>
        <div className="heading-actions">
          <Button variant="outlined" disabled={busy || sound === "Off"} onClick={() => void playNotificationSound(sound)}>Escuchar prueba</Button>
          <Button variant="contained" disabled={busy || sound === (user.notificationSound || "Off")}
            onClick={async () => {
              setBusy(true); setError(undefined); setSaved(false);
              try {
                await api("/auth/notification-sound", { method: "PUT", body: JSON.stringify({ sound }) });
                await client.invalidateQueries({ queryKey: ["me"] });
                setSaved(true);
              } catch (failure) { setError(failure); }
              finally { setBusy(false); }
            }}>Guardar sonido</Button>
        </div>
        {saved && <Alert severity="success">Preferencia guardada para tu cuenta.</Alert>}
        <ErrorBox error={error} />
      </div>
    </section>
  );
}
