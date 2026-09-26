import { Brand } from "./Brand";
import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Alert, Button, TextField, MenuItem } from "@mui/material";
import { Mail, Send, CheckCircle2, ArrowLeft, RefreshCw } from "lucide-react";
import { api, date } from "./api";
import { ErrorBox, Loading } from "./shared";

export function GuestPage({ confirm = false }: { confirm?: boolean }) {
  const [name, setName] = useState(""),
    [email, setEmail] = useState(""),
    [subject, setSubject] = useState(""),
    [body, setBody] = useState(""),
    [category, setCategory] = useState("General"),
    [busy, setBusy] = useState(false),
    [error, setError] = useState<unknown>(),
    [success, setSuccess] = useState(""),
    [token] = useState(
      () => new URLSearchParams(location.hash.slice(1)).get("token") || "",
    );
  const config = useQuery({
    queryKey: ["public-config"],
    queryFn: async () =>
      (
        await api<{
          available: boolean;
          testMode: boolean;
          categories: string[];
        }>("/public/config")
      ).data,
  });
  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(undefined);
    try {
      if (confirm) {
        const result = await api<{ number: string }>("/public/confirm", {
          method: "POST",
          body: JSON.stringify({ token }),
        });
        setSuccess(
          "Tu ticket " +
            result.data.number +
            " está registrado. Recibirás la confirmación por correo y podrás responder directamente desde tu buzón.",
        );
        history.replaceState(null, "", location.pathname);
      } else {
        const result = await api<{ message: string }>("/public/tickets", {
          method: "POST",
          body: JSON.stringify({ name, email, subject, body, category }),
        });
        setSuccess(result.data.message);
      }
    } catch (e) {
      setError(e);
    } finally {
      setBusy(false);
    }
  }
  return (
    <div className="guest-page">
      <a href="/" className="guest-brand">
        <Brand />
      </a>
      <section className="guest-card">
        {config.data?.testMode && (
          <Alert severity="info" sx={{ mb: 3 }}>
            Modo de demostración: no se envían correos reales.
          </Alert>
        )}
        <span className="eyebrow">ATENCIÓN SIN COMPLICACIONES</span>
        <h1>
          {confirm ? "Confirma tu solicitud." : "Cuéntanos qué necesitas."}
        </h1>
        <p className="form-note">
          No necesitas crear una cuenta. Recibe tu número de ticket y conversa
          con nuestro equipo por correo.
        </p>
        {success ? (
          <div className="guest-success">
            <CheckCircle2 size={40} />
            <h2>{confirm ? "Solicitud registrada" : "Revisa tu correo"}</h2>
            <p>{success}</p>
            <Button href="/">Ir al inicio</Button>
          </div>
        ) : (
          <form className="form-fields" onSubmit={submit}>
            {confirm ? (
              <>
                <Alert severity="info">
                  Pulsa Confirmar para verificar tu dirección de correo y
                  registrar el ticket. Este enlace solo sirve para esta
                  solicitud.
                </Alert>
                <Button
                  variant="contained"
                  type="submit"
                  disabled={busy || !token}
                >
                  Confirmar solicitud
                </Button>
              </>
            ) : (
              <>
                {config.data && !config.data.available && (
                  <Alert severity="warning">
                    Este canal aún no está habilitado. Contacta al equipo de
                    Sidecil.
                  </Alert>
                )}
                <TextField
                  label="Nombre completo"
                  required
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                  inputProps={{ minLength: 2, maxLength: 120 }}
                />
                <TextField
                  label="Correo electrónico"
                  type="email"
                  required
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  inputProps={{ maxLength: 200 }}
                />
                <TextField
                  label="Asunto"
                  required
                  value={subject}
                  onChange={(e) => setSubject(e.target.value)}
                  inputProps={{ minLength: 5, maxLength: 180 }}
                />
                <TextField
                  select
                  label="Categoría"
                  value={category}
                  onChange={(e) => setCategory(e.target.value)}
                >
                  {(config.data?.categories || ["General"]).map((x) => (
                    <MenuItem key={x} value={x}>
                      {x}
                    </MenuItem>
                  ))}
                </TextField>
                <TextField
                  required
                  multiline
                  minRows={5}
                  label="Describe tu solicitud"
                  value={body}
                  onChange={(e) => setBody(e.target.value)}
                  inputProps={{ minLength: 10, maxLength: 12000 }}
                />
                <p className="small-note">
                  Te enviaremos un enlace de confirmación, válido por 24 horas.
                  Tu solicitud se registra cuando confirmas tu correo.
                </p>
                <Button
                  variant="contained"
                  type="submit"
                  disabled={busy || !config.data?.available}
                  endIcon={<Send size={16} />}
                >
                  {busy ? "Enviando…" : "Enviar solicitud"}
                </Button>
              </>
            )}
            <ErrorBox error={error || config.error} />
          </form>
        )}
        <Button href="/" startIcon={<ArrowLeft size={15} />} sx={{ mt: 3 }}>
          Ya tengo cuenta
        </Button>
      </section>
    </div>
  );
}

type Settings = {
  template: {
    subject: string;
    body: string;
    signature: string;
    version: string;
  };
  mode: string;
  outbound: {
    id: string;
    recipient: string;
    subject: string;
    state: string;
    kind: string;
    attempts: number;
    createdAt: string;
    lastError: string | null;
  }[];
  inbound: {
    id: string;
    sender: string;
    subject: string;
    state: string;
    reason: string;
    receivedAt: string;
  }[];
};
const states: Record<string, string> = {
  Pending: "Pendiente",
  Sending: "Procesando",
  Sent: "Enviado",
  Failed: "Fallido",
  Expired: "Vencido",
  Review: "Revisión manual",
  Accepted: "Incorporado",
  Ignored: "Ignorado",
};
export function MailSettings() {
  const q = useQuery({
    queryKey: ["mail-settings"],
    queryFn: async () => (await api<Settings>("/admin/email")).data,
    refetchInterval: 15000,
  });
  const [draft, setDraft] = useState<Settings["template"] | null>(null),
    [error, setError] = useState<unknown>(),
    [saved, setSaved] = useState(false),
    [busy, setBusy] = useState(false);
  if (q.isPending) return <Loading />;
  if (!q.data) return <ErrorBox error={q.error} />;
  const t = draft || q.data.template;
  const update = (key: "subject" | "body" | "signature", value: string) => {
    setDraft({ ...t, [key]: value });
    setSaved(false);
  };
  const render = (value: string) =>
    value.replace(
      /\{(nombre|numero|asunto|firma)\}/g,
      (_, v: string) =>
        ({
          nombre: "Mariana",
          numero: "SC-00125",
          asunto: "Consulta sobre el servicio",
          firma: t.signature,
        })[v] || "",
    );
  async function save(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(undefined);
    try {
      await api("/admin/email/template", {
        method: "PUT",
        body: JSON.stringify(t),
      });
      await q.refetch();
      setDraft(null);
      setSaved(true);
    } catch (e) {
      setError(e);
    } finally {
      setBusy(false);
    }
  }
  return (
    <>
      <div className="page-heading">
        <div>
          <span className="eyebrow">CONVERSACIONES QUE CONTINÚAN</span>
          <h1>Correo de atención.</h1>
          <p>
            Personaliza la confirmación y sigue los mensajes que entran y salen.
          </p>
        </div>
        <Button startIcon={<RefreshCw size={16} />} onClick={() => q.refetch()}>
          Actualizar
        </Button>
      </div>
      {q.data.mode === "Pickup" ? (
        <Alert severity="info">
          Modo de prueba local: los mensajes se guardan como archivos de correo.
          No se envían a destinatarios reales.
        </Alert>
      ) : q.data.mode === "Disabled" ? (
        <Alert severity="warning">
          El envío está deshabilitado. Los correos quedan pendientes hasta
          configurar el buzón.
        </Alert>
      ) : (
        <Alert severity="success">
          Modo de correo empresarial. Consulta los estados de entrega y los
          mensajes que requieran revisión.
        </Alert>
      )}
      <div className="mail-layout">
        <section className="report-card">
          <h2>
            <Mail size={18} /> Mensaje de confirmación
          </h2>
          <p>
            Se envía al crear un ticket. Sin cuenta, se envía después de
            confirmar el correo. Los cambios aplican a futuros mensajes.
          </p>
          <form className="form-fields" onSubmit={save}>
            <TextField
              required
              label="Asunto del correo"
              value={t.subject}
              onChange={(e) => update("subject", e.target.value)}
              inputProps={{ maxLength: 200 }}
            />
            <TextField
              required
              label="Mensaje personalizado"
              multiline
              minRows={8}
              value={t.body}
              onChange={(e) => update("body", e.target.value)}
              inputProps={{ maxLength: 8000 }}
            />
            <TextField
              required
              label="Firma"
              multiline
              minRows={2}
              value={t.signature}
              onChange={(e) => update("signature", e.target.value)}
              inputProps={{ maxLength: 500 }}
            />
            <span className="small-note">
              Variables disponibles: {"{nombre}, {numero}, {asunto}, {firma}"}.
              El número del ticket siempre se añade al asunto.
            </span>
            <ErrorBox error={error} />
            {saved && <Alert severity="success">Plantilla guardada.</Alert>}
            <div className="heading-actions">
              <Button
                variant="contained"
                type="submit"
                disabled={busy || !draft}
              >
                Guardar plantilla
              </Button>
              {draft && (
                <Button
                  onClick={() => {
                    setDraft(null);
                    setError(undefined);
                  }}
                >
                  Descartar cambios
                </Button>
              )}
            </div>
          </form>
        </section>
        <section className="mail-preview">
          <span className="eyebrow">VISTA PREVIA</span>
          <h3>[SC-00125] {render(t.subject)}</h3>
          <p>{render(t.body)}</p>
          <small>
            Las respuestas públicas del agente también llegan por correo. Las
            notas internas permanecen en el sistema.
          </small>
        </section>
      </div>
      <section className="inbox-panel mail-log">
        <div className="panel-title">
          <h2>
            Correos salientes <span className="count">Últimos 50</span>
          </h2>
        </div>
        <div className="table-scroll">
          <table className="tickets-table">
            <thead>
              <tr>
                <th>Correo / destinatario</th>
                <th>Estado</th>
                <th>Intentos</th>
                <th>Acción</th>
              </tr>
            </thead>
            <tbody>
              {q.data.outbound.map((m) => (
                <tr key={m.id}>
                  <td>
                    <strong>{m.subject}</strong>
                    <small className="email-recipient">
                      {m.recipient} · {date(m.createdAt)}
                    </small>
                  </td>
                  <td>
                    <span className="badge">
                      {q.data.mode === "Pickup" && m.state === "Sent"
                        ? "Guardado local"
                        : states[m.state] || m.state}
                    </span>
                    {m.lastError && (
                      <small className="email-recipient">{m.lastError}</small>
                    )}
                  </td>
                  <td>{m.attempts}</td>
                  <td>
                    {m.state === "Failed" && (
                      <Button
                        disabled={busy}
                        onClick={async () => {
                          setBusy(true);
                          try {
                            await api("/admin/email/" + m.id + "/retry", {
                              method: "POST",
                            });
                            await q.refetch();
                          } catch (e) {
                            setError(e);
                          } finally {
                            setBusy(false);
                          }
                        }}
                      >
                        Reintentar
                      </Button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {!q.data.outbound.length && (
            <p className="empty">Todavía no hay correos salientes.</p>
          )}
        </div>
      </section>
      <section className="inbox-panel mail-log">
        <div className="panel-title">
          <h2>
            Correos recibidos <span className="count">Últimos 50</span>
          </h2>
        </div>
        <div className="table-scroll">
          <table className="tickets-table">
            <thead>
              <tr>
                <th>Correo / remitente</th>
                <th>Estado</th>
                <th>Resultado</th>
              </tr>
            </thead>
            <tbody>
              {q.data.inbound.map((m) => (
                <tr key={m.id}>
                  <td>
                    <strong>{m.subject || "Sin asunto"}</strong>
                    <small className="email-recipient">
                      {m.sender} · {date(m.receivedAt)}
                    </small>
                  </td>
                  <td>
                    <span className="badge">{states[m.state] || m.state}</span>
                  </td>
                  <td className="email-reason">{m.reason}</td>
                </tr>
              ))}
            </tbody>
          </table>
          {!q.data.inbound.length && (
            <p className="empty">Todavía no hay correos recibidos.</p>
          )}
        </div>
      </section>
      <p className="info-strip">
        Los mensajes en revisión se conservan en el buzón original. Verifica
        allí remitente y adjuntos antes de añadir información al ticket.
      </p>
    </>
  );
}
