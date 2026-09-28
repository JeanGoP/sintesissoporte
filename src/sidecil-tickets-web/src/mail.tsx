import { SupportFields } from "./support-fields";
import { Brand } from "./Brand";
import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Alert, Button, TextField, MenuItem } from "@mui/material";
import {
  Paperclip,
  Trash2,
  Mail,
  Send,
  CheckCircle2,
  ArrowLeft,
  RefreshCw,
} from "lucide-react";
import { api, date, statuses, type Status } from "./api";
import { ErrorBox, Loading } from "./shared";

export function GuestPage({ confirm = false }: { confirm?: boolean }) {
  const [name, setName] = useState(""),
    [email, setEmail] = useState(""),
    [subject, setSubject] = useState(""),
    [body, setBody] = useState(""),
    [category, setCategory] = useState("General"),
    [moduleId, setModuleId] = useState(""),
    [companyName, setCompanyName] = useState(""),
    [organizationId, setOrganizationId] = useState(""),
    [files, setFiles] = useState<File[]>([]),
    [accessToken] = useState(() => new URLSearchParams(location.hash.slice(1)).get("access") || ""),
    [choice, setChoice] = useState<"new" | string>(""),
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
          modules: import("./api").SupportModule[];
        }>("/public/config")
      ).data,
  });
  const access = useQuery({
    queryKey: ["public-access", accessToken],
    enabled: !!accessToken && !confirm,
    queryFn: async () => (await api<{name: string; email: string; tickets: {id: string; number: string; subject: string; status: string}[]}>("/public/access/tickets", {
      method: "POST", body: JSON.stringify({ token: accessToken }), cache: "no-store",
    })).data,
    retry: false,
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
      } else if (!accessToken) {
        const result = await api<{message: string}>("/public/access/start", {
          method: "POST", body: JSON.stringify({ name, email }),
        });
        setSuccess(result.data.message);
      } else {
        if (!access.data || !choice) throw new Error("Verifica tu correo y elige un ticket o una nueva solicitud.");
        if (choice === "new" && !moduleId) throw new Error("Selecciona un módulo.");
        const form = new FormData();
        form.set("token", accessToken);
        form.set("ticketId", choice === "new" ? "" : choice);
        form.set("subject", subject);
        form.set("body", body);
        form.set("category", category);
        form.set("moduleId", moduleId);
        form.set("companyName", companyName);
        files.forEach((file) => form.append("files", file, file.name));
        const result = await api<{number: string; existing: boolean}>("/public/access/send", {
          method: "POST", body: form,
        });
        setSuccess(result.data.existing
          ? "Tu mensaje se agregó al ticket " + result.data.number + ". El equipo podrá revisar tu información y archivos."
          : "Tu ticket " + result.data.number + " está registrado. Recibirás la confirmación por correo.");
        setFiles([]);
        history.replaceState(null, "", location.pathname);
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
          {confirm ? "Confirma tu solicitud." : accessToken ? "Continuemos tu solicitud." : "Verifica tu correo para comenzar."}
        </h1>
        <p className="form-note">
          No necesitas crear una cuenta. Verifica tu correo para consultar tickets pendientes o crear uno nuevo.
        </p>
        {success ? (
          <div className="guest-success">
            <CheckCircle2 size={40} />
            <h2>{confirm || accessToken ? "Solicitud registrada" : "Revisa tu correo"}</h2>
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
            ) : !accessToken ? (
              <>
                {config.data && !config.data.available && <Alert severity="warning">Este canal aún no está habilitado.</Alert>}
                <TextField label="Nombre completo" required value={name} onChange={e => setName(e.target.value)} inputProps={{ minLength: 2, maxLength: 120 }}/>
                <TextField label="Correo electrónico" type="email" required value={email} onChange={e => setEmail(e.target.value)} inputProps={{ maxLength: 200 }}/>
                <p className="small-note">Te enviaremos un enlace válido por una hora. Solo después de abrirlo mostraremos tus tickets pendientes.</p>
                <Button variant="contained" type="submit" disabled={busy || !config.data?.available}>{busy ? "Enviando…" : "Enviar enlace de verificación"}</Button>
              </>
            ) : !access.data ? (
              <>{access.isPending ? <Loading/> : <><Alert severity="error">El enlace no es válido o ya venció.</Alert><Button href="/solicitar">Solicitar otro enlace</Button></>}</>
            ) : !choice ? (
              <>
                <Alert severity="success">Correo verificado: {access.data.email}</Alert>
                <h2>¿Tu consulta corresponde a uno de estos tickets?</h2>
                {access.data.tickets.length === 0 && <p>No tienes tickets pendientes.</p>}
                {access.data.tickets.map(t => <Button key={t.id} onClick={() => setChoice(t.id)}>{t.number} · {t.subject} · {statuses[t.status as Status] || t.status}</Button>)}
                <Button variant="contained" onClick={() => setChoice("new")}>Crear una nueva solicitud</Button>
              </>
            ) : (
              <>
                <Alert severity="info">{choice === "new" ? "Nueva solicitud" : "Agregar información a " + access.data.tickets.find(t => t.id === choice)?.number}</Alert>
                {choice === "new" && <>
                <TextField
                  label="Asunto"
                  required
                  value={subject}
                  onChange={(e) => setSubject(e.target.value)}
                  inputProps={{ minLength: 5, maxLength: 180 }}
                />
                <SupportFields
                  categories={config.data?.categories || []}
                  modules={config.data?.modules || []}
                  category={category}
                  moduleId={moduleId}
                  companyName={companyName}
                  disabled={busy}
                  onChange={(v) => {
                    setCategory(v.category);
                    setModuleId(v.moduleId);
                    setCompanyName(v.companyName);
                    setOrganizationId(v.organizationId || "");
                  }}
                />
                </>}
                <TextField
                  required
                  multiline
                  minRows={5}
                  label="Describe tu solicitud"
                  value={body}
                  onChange={(e) => setBody(e.target.value)}
                  inputProps={{ minLength: 10, maxLength: 12000 }}
                />
                <section aria-label="Archivos de soporte">
                  <h3>Archivos de soporte</h3>
                  <p>Hasta 3 archivos de 5 MB: PNG, JPG, PDF o TXT.</p>
                  {files.map((file, index) => (
                    <div className="chat-file" key={index}>
                      <Paperclip size={16} />
                      <span>
                        {file.name}
                        <small>{(file.size / 1024).toFixed(1)} KB</small>
                      </span>
                      <Button
                        aria-label={"Eliminar " + file.name}
                        disabled={busy}
                        onClick={() =>
                          setFiles(files.filter((_, i) => i !== index))
                        }
                      >
                        <Trash2 size={17} />
                      </Button>
                    </div>
                  ))}
                  <Button
                    component="label"
                    disabled={busy || files.length >= 3}
                    startIcon={<Paperclip size={16} />}
                  >
                    Adjuntar archivos
                    <input
                      type="file"
                      hidden
                      multiple
                      accept=".png,.jpg,.jpeg,.pdf,.txt"
                      aria-label="Seleccionar archivos"
                      onChange={(e) => {
                        const selected = Array.from(e.target.files || []);
                        e.target.value = "";
                        if (files.length + selected.length > 3) {
                          setError(
                            new Error("Puedes adjuntar hasta 3 archivos."),
                          );
                          return;
                        }
                        if (
                          selected.some(
                            (f) => f.size === 0 || f.size > 5 * 1024 * 1024,
                          )
                        ) {
                          setError(
                            new Error(
                              "Cada archivo debe pesar entre 1 byte y 5 MB.",
                            ),
                          );
                          return;
                        }
                        if (
                          selected.some(
                            (f) => !/\.(png|jpe?g|pdf|txt)$/i.test(f.name),
                          )
                        ) {
                          setError(
                            new Error("Adjunta archivos PNG, JPG, PDF o TXT."),
                          );
                          return;
                        }
                        setFiles([...files, ...selected]);
                        setError(undefined);
                      }}
                    />
                  </Button>
                </section>
                <p className="small-note">
                  Tu correo ya está verificado. Tu mensaje y archivos se guardarán al enviar.
                </p>
                <Button
                  variant="contained"
                  type="submit"
                  disabled={busy || !config.data?.available}
                  endIcon={<Send size={16} />}
                >
                  {busy ? "Enviando…" : choice === "new" ? "Crear ticket" : "Enviar al ticket"}
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
